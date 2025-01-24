#pragma warning disable SKEXP0001, SKEXP0010
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Raven.Client.Documents.Attachments;
using Raven.Client.Documents.Commands.Batches;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Indexes.Vector;
using Raven.Client.Documents.Operations.Counters;
using Raven.Client.Documents.Operations.ETL;
using Raven.Client.Documents.Operations.ETL.AI;
using Raven.Client.Http;
using Raven.Client.Util;
using Raven.Server.Documents.ETL.Providers.AI.Enumerators;
using Raven.Server.Documents.ETL.Stats;
using Raven.Server.Documents.Handlers;
using Raven.Server.Documents.Handlers.Processors.TimeSeries;
using Raven.Server.Documents.Indexes.VectorSearch;
using Raven.Server.Documents.Replication.ReplicationItems;
using Raven.Server.Documents.TimeSeries;
using Raven.Server.ServerWide;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.Documents.ETL.Providers.AI;

public sealed class AiEtl : EtlProcess<AiEtlItem, EmbeddingRepresentation, AiEtlConfiguration, AiEtlConnectionString, EtlStatsScope, EtlPerformanceOperation>
{
    private readonly AiEtlConfiguration _configuration;
    private readonly ServerStore _serverStore;
    private ITextEmbeddingGenerationService _service;
    
    public const string AiEtlTag = "AI ETL";
    
    public AiEtl(Transformation transformation, AiEtlConfiguration configuration, DocumentDatabase database, ServerStore serverStore) : base(transformation, configuration, database, serverStore, AiEtlTag)
    {
        _configuration = configuration;
        _serverStore = serverStore;
    }

    public override EtlType EtlType => EtlType.Ai;
    public override bool ShouldTrackCounters() => false;
    public override bool ShouldTrackTimeSeries() => false;
    
    protected override IEnumerator<AiEtlItem> ConvertDocsEnumerator(DocumentsOperationContext context, IEnumerator<Document> docs, string collection)
    {
        return new DocumentsToAiEtlItems(docs, collection);
    }

    protected override IEnumerator<AiEtlItem> ConvertTombstonesEnumerator(DocumentsOperationContext context, IEnumerator<Tombstone> tombstones, string collection, bool trackAttachments)
    {
        return new TombstonesToAiEtlItems(context, tombstones, collection, trackAttachments);
    }

    protected override IEnumerator<AiEtlItem> ConvertAttachmentTombstonesEnumerator(DocumentsOperationContext context, IEnumerator<Tombstone> tombstones, List<string> collections)
    {
        throw new System.NotImplementedException();
    }

    protected override IEnumerator<AiEtlItem> ConvertCountersEnumerator(DocumentsOperationContext context, IEnumerator<CounterGroupDetail> counters, string collection)
    {
        throw new System.NotImplementedException();
    }

    protected override IEnumerator<AiEtlItem> ConvertTimeSeriesEnumerator(DocumentsOperationContext context, IEnumerator<TimeSeriesSegmentEntry> timeSeries, string collection)
    {
        throw new System.NotImplementedException();
    }

    protected override IEnumerator<AiEtlItem> ConvertTimeSeriesDeletedRangeEnumerator(DocumentsOperationContext context, IEnumerator<TimeSeriesDeletedRangeItem> timeSeries, string collection)
    {
        throw new System.NotImplementedException();
    }

    protected override bool ShouldTrackAttachmentTombstones()
    {
        return false;
    }
    
    protected override EtlTransformer<AiEtlItem, EmbeddingRepresentation, EtlStatsScope, EtlPerformanceOperation> GetTransformer(DocumentsOperationContext context)
    {
        return new AiEtlDocumentTransformer(Database, context, null, null, _configuration);
    }
    
    protected override int LoadInternal(IEnumerable<EmbeddingRepresentation> items, DocumentsOperationContext context, EtlStatsScope scope)
    {
        var aiEtlScriptRun = items as AiEtlScriptRun;
        List<string> textValues = new List<string>();
        
        int processed = 0;
        
        foreach (var run in aiEtlScriptRun.Runs)
        {
            var documentId = run.Key;
            
            foreach (var fieldData in run.Value)
            {
                // foreach (var fieldValue in fieldData.Value)
                // {
                //     
                //     string attachmentGuid = null;
                //     var idToSearchFor = GetPrivateDocumentId($"hash({fieldValue})");
                //     
                //     var privateDocument = Database.DocumentsStorage.Get(context, idToSearchFor);
                //     
                //     // todo change vector
                //     if (privateDocument == null || privateDocument.Data.TryGet(fieldValue, out attachmentGuid) == false)
                //     {
                //         //CreateNewPrivateDocument(fieldValue, context, out attachmentGuid);
                //         
                //         
                //     }
                //
                //     else
                //     {
                //         
                //     }
                //
                // }
            }
            /*
            var publicDocument = Database.DocumentsStorage.Get(context, documentData.Key);

            if (publicDocument == null)
                CreateNewPublicDocument(documentData, context);

            if (publicDocument.Data.TryGet(_configuration.Name, out object x) == false)
            {
                // create
            }
            */
            
            processed++;
        }

        return processed;
    }

    private void CreateNewPublicDocument(KeyValuePair<string, Dictionary<string, List<string>>> documentData, DocumentsOperationContext context)
    {
        var originalDocumentId = documentData.Key;
        var newDocumentId = GetPublicDocumentId(originalDocumentId);

        // Root object
        var documentDjv = new DynamicJsonValue { ["Id"] = newDocumentId, ["@metadata"] = new DynamicJsonValue() { ["@collection"] = "testembeddings" } };

        // ConfigurationName -> (fieldName, attachmentsGuids[])[]
        var embeddingsObjectDjv = new DynamicJsonValue();
            
        // (attachmentGuid, embeddingByteArray)
        var attachments = new Dictionary<string, byte[]>();

        foreach ((string fieldName, List<string> fieldValues) in documentData.Value)
        {
            var dja = new DynamicJsonArray();
                
            foreach (var fieldValue in fieldValues)
            {
                var embedding = GenerateEmbeddings.FromText(context.Allocator, VectorOptions.DefaultText, fieldValue).GetEmbedding().ToArray();
                    
                var embeddingGuid = Guid.NewGuid().ToString();
                    
                attachments.Add(embeddingGuid, embedding);
                dja.Add(embeddingGuid);
            }

            embeddingsObjectDjv[fieldName] = dja;
        }

        documentDjv[_configuration.Name] = embeddingsObjectDjv;

        using (var ctx = JsonOperationContext.ShortTermSingleUse())
        {
            var bjro = ctx.ReadObject(documentDjv, "doc");

            var cmd = new MergedPutEmbeddingCommand(bjro, newDocumentId, null, attachments, Database);

            Database.TxMerger.EnqueueSync(cmd);
        }
    }

    private void CreateNewPrivateDocument(string textValue, DocumentsOperationContext context, out string attachmentGuid)
    {
        var hash = $"hash({textValue})";
        var newDocumentId = GetPrivateDocumentId(hash);
        
        var documentDjv = new DynamicJsonValue { ["Id"] = newDocumentId, ["@metadata"] = new DynamicJsonValue() { ["@collection"] = "@embeddings" } };
        
        attachmentGuid = Guid.NewGuid().ToString();

        documentDjv[textValue] = attachmentGuid;
        
        var embedding = GenerateEmbeddings.FromText(context.Allocator, VectorOptions.DefaultText, textValue).GetEmbedding().ToArray();
        
        using (var ctx = JsonOperationContext.ShortTermSingleUse())
        {
            var bjro = ctx.ReadObject(documentDjv, "doc");

            var cmd = new MergedPutEmbeddingCommand(bjro, newDocumentId, null, new Dictionary<string, byte[]>() { { attachmentGuid, embedding } }, Database);

            Database.TxMerger.EnqueueSync(cmd);
        }
    }

    private static string GetPublicDocumentId(string originalDocumentId)
    {
        return $"{originalDocumentId}/embeddings";
    }
    
    private string GetPrivateDocumentId(string hash)
    {
        return $"embeddings/{_configuration.Name}/{hash}";
    }

    protected override EtlStatsScope CreateScope(EtlRunStats stats)
    {
        return new EtlStatsScope(stats);
    }

    protected override bool ShouldFilterOutHiLoDocument()
    {
        throw new System.NotImplementedException();
    }

    /*
    private ITextEmbeddingGenerationService CreateService(OpenAiConnectionString connectionString)
    {
        var service = new OpenAITextEmbeddingGenerationService(
            "text-embedding-ada-002",
            "https://{myservice}.openai.azure.com/",
            "apikey");

        return service;
    }
    */
}
#pragma warning restore SKEXP0001, SKEXP0010
