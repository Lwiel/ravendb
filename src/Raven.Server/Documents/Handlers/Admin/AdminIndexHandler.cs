﻿using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Exceptions.Documents.Indexes;
using Raven.Client.ServerWide.Operations;
using Raven.Server.Documents.Handlers.Admin.Processors.Indexes;
using Raven.Server.Documents.Indexes;
using Raven.Server.Documents.Indexes.MapReduce.Static;
using Raven.Server.Documents.Indexes.Static;
using Raven.Server.Documents.Indexes.Static.Counters;
using Raven.Server.Documents.Indexes.Static.TimeSeries;
using Raven.Server.Documents.Indexes.Test;
using Raven.Server.Documents.Operations;
using Raven.Server.Documents.Queries;
using Raven.Server.Json;
using Raven.Server.NotificationCenter;
using Raven.Server.Routing;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using Sparrow.Utils;
using Index = Raven.Server.Documents.Indexes.Index;

namespace Raven.Server.Documents.Handlers.Admin
{
    public class AdminIndexHandler : DatabaseRequestHandler
    {
        [RavenAction("/databases/*/admin/indexes", "PUT", AuthorizationStatus.DatabaseAdmin, DisableOnCpuCreditsExhaustion = true)]
        public async Task Put()
        {
            using (var processor = new AdminIndexHandlerProcessorForStaticPut(this))
                await processor.ExecuteAsync();
        }

        [RavenAction("/databases/*/indexes", "PUT", AuthorizationStatus.ValidUser, EndpointType.Write, DisableOnCpuCreditsExhaustion = true)]
        public async Task PutJavaScript()
        {
            using (var processor = new AdminIndexHandlerProcessorForJavaScriptPut(this))
                await processor.ExecuteAsync();
        }
        
        [RavenAction("/databases/*/indexes/test", "POST", AuthorizationStatus.ValidUser, EndpointType.Write, DisableOnCpuCreditsExhaustion = true)]
        public async Task TestIndex()
        {
            using (ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            {
                var input = await context.ReadForMemoryAsync(RequestBodyStream(), "Input");

                var testIndexParameters = JsonDeserializationServer.TestIndexParameters(input);
                var testIndexDefinition = testIndexParameters.IndexDefinition;
                var query = testIndexParameters.Query;
                
                if (testIndexParameters.IndexDefinition is null)
                    throw new ArgumentException("Index must have an 'IndexDefinition' field");
                
                if (testIndexDefinition.Type.IsJavaScript() == false)
                {
                    // C# index without admin authorization
                    if (HttpContext.Features.Get<IHttpAuthenticationFeature>() is RavenServer.AuthenticateConnection feature && feature.CanAccess(Database.Name, requireAdmin: true, requireWrite: true) == false)
                        throw new UnauthorizedAccessException($"Index {testIndexDefinition.Name} is a C# index but was sent without admin authorization, this is not allowed.");
                }

                if (testIndexDefinition.Name is null)
                    testIndexDefinition.Name = Guid.NewGuid().ToString("N");

                if (query is null)
                {
                    var queryString = $"from index '{testIndexDefinition.Name}'";
                    var queryObject = new { Query = queryString };
                    
                    query = DocumentConventions.DefaultForServer.Serialization.DefaultConverter.ToBlittable(queryObject, context);
                }

                using (var index = Database.IndexStore.CreateTestIndexFromDefinition(testIndexDefinition, context.DocumentDatabase, context))
                {
                    index.Start();

                using (var tracker = new RequestTimeTracker(HttpContext, Logger, Database.NotificationCenter, Database.Configuration, "Query"))
                using (var token = CreateTimeLimitedQueryToken())
                using (var queryContext = QueryOperationContext.Allocate(Database))
                {
                    var indexQueryServerSide = IndexQueryServerSide.Create(HttpContext, query, Database.QueryMetadataCache, tracker);

                    indexQueryServerSide.WaitForNonStaleResults = true;
                    
                    var entries = await index.IndexEntries(indexQueryServerSide, queryContext, true, token);
                    var queryResults = await index.Query(indexQueryServerSide, queryContext, token);
                    var mapResults = index.TestRun.MapResults;
                    var reduceResults = index.TestRun.ReduceResults;

                    var hasDynamicFields = index.Definition.HasDynamicFields;

                        var result = new TestIndexResult()
                        {
                            IndexEntries = entries.Results,
                            QueryResults = queryResults.Results,
                            MapResults = mapResults,
                            HasDynamicFields = hasDynamicFields,
                            ReduceResults = reduceResults
                        };

                        await result.WriteTestIndexResult(ResponseBodyStream(), context);
                    }
                }
            }
        }
        
        [RavenAction("/databases/*/admin/indexes/stop", "POST", AuthorizationStatus.DatabaseAdmin)]
        public async Task Stop()
        {
            using (var processor = new AdminIndexHandlerProcessorForStop(this))
                await processor.ExecuteAsync();
        }

        [RavenAction("/databases/*/admin/indexes/start", "POST", AuthorizationStatus.DatabaseAdmin)]
        public async Task Start()
        {
            using (var processor = new AdminIndexHandlerProcessorForStart(this))
                await processor.ExecuteAsync();
        }

        [RavenAction("/databases/*/admin/indexes/enable", "POST", AuthorizationStatus.DatabaseAdmin)]
        public async Task Enable()
        {
            using (var processor = new AdminIndexHandlerProcessorForState(IndexState.Normal, this))
                await processor.ExecuteAsync();
        }

        [RavenAction("/databases/*/admin/indexes/disable", "POST", AuthorizationStatus.DatabaseAdmin)]
        public async Task Disable()
        {
            using (var processor = new AdminIndexHandlerProcessorForState(IndexState.Disabled, this))
                await processor.ExecuteAsync();
        }

        [RavenAction("/databases/*/admin/indexes/dump", "POST", AuthorizationStatus.DatabaseAdmin)]
        public async Task Dump()
        {
            using (var processor = new AdminIndexHandlerProcessorForDump(this))
                await processor.ExecuteAsync();
        }

        public class DumpIndexResult : IOperationResult
        {
            public string Message { get; set; }

            public DynamicJsonValue ToJson()
            {
                return new DynamicJsonValue(GetType())
                {
                    [nameof(Message)] = Message,
                };
            }

            public bool ShouldPersist => false;

            bool IOperationResult.CanMerge => false;

            void IOperationResult.MergeWith(IOperationResult result)
            {
                throw new System.NotImplementedException();
            }
        }

        public class DumpIndexProgress : IOperationProgress
        {
            public int ProcessedFiles { get; set; }
            public int TotalFiles { get; set; }
            public string Message { get; set; }
            public long CurrentFileSizeInBytes { get; internal set; }
            public long CurrentFileCopiedBytes { get; internal set; }

            public virtual DynamicJsonValue ToJson()
            {
                return new DynamicJsonValue(GetType())
                {
                    [nameof(ProcessedFiles)] = ProcessedFiles,
                    [nameof(TotalFiles)] = TotalFiles,
                    [nameof(Message)] = Message,
                    [nameof(CurrentFileSizeInBytes)] = CurrentFileSizeInBytes,
                    [nameof(CurrentFileCopiedBytes)] = CurrentFileCopiedBytes
                };
            }

            IOperationProgress IOperationProgress.Clone()
            {
                throw new System.NotImplementedException();
            }

            bool IOperationProgress.CanMerge => false;

            void IOperationProgress.MergeWith(IOperationProgress progress)
            {
                throw new System.NotImplementedException();
            }
        }

        [RavenAction("/databases/*/admin/indexes/optimize", "POST", AuthorizationStatus.DatabaseAdmin, DisableOnCpuCreditsExhaustion = true)]
        public async Task OptimizeIndex()
        {
            DevelopmentHelper.ShardingToDo(DevelopmentHelper.TeamMember.Pawel, DevelopmentHelper.Severity.Major, "Implement for sharding");

            using (ServerStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
            {
                var name = GetQueryStringValueAndAssertIfSingleAndNotEmpty("name");
                var index = Database.IndexStore.GetIndex(name);
                if (index == null)
                    IndexDoesNotExistException.ThrowFor(name);

                var token = CreateOperationToken();
                var result = new IndexOptimizeResult(index.Name);
                var operationId = Database.Operations.GetNextOperationId();
                var t = Database.Operations.AddLocalOperation(
                    operationId,
                    OperationType.LuceneOptimizeIndex,
                    "Optimizing index: " + index.Name,
                    detailedDescription: null,
                    taskFactory: _ => Task.Run(() =>
                    {
                        try
                        {
                            using (token)
                            using (Database.PreventFromUnloadingByIdleOperations())
                            using (var indexCts = CancellationTokenSource.CreateLinkedTokenSource(token.Token, Database.DatabaseShutdown))
                            {
                                index.Optimize(result, indexCts.Token);
                                return Task.FromResult((IOperationResult)result);
                            }
                        }
                        catch (Exception e)
                        {
                            if (Logger.IsOperationsEnabled)
                                Logger.Operations("Optimize process failed", e);

                            throw;
                        }
                    }, token.Token),
                    token: token);

                await using (var writer = new AsyncBlittableJsonTextWriter(context, ResponseBodyStream()))
                {
                    writer.WriteOperationIdAndNodeTag(context, operationId, ServerStore.NodeTag);
                }
            }
        }
    }
}
