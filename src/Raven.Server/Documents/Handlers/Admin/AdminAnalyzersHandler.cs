using System.Net;
using System.Threading.Tasks;
using Raven.Server.Documents.Indexes.Analysis;
using Raven.Server.Json;
using Raven.Server.Routing;
using Raven.Server.ServerWide.Commands.Analyzers;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;
using Sparrow.Logging;

namespace Raven.Server.Documents.Handlers.Admin
{
    public class AdminAnalyzersHandler : DatabaseRequestHandler
    {
        [RavenAction("/databases/*/admin/analyzers", "PUT", AuthorizationStatus.DatabaseAdmin)]
        public async Task Put()
        {
            using (ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            {
                var input = await context.ReadForMemoryAsync(RequestBodyStream(), "Analyzers");
                if (input.TryGet("Analyzers", out BlittableJsonReaderArray analyzers) == false)
                    ThrowRequiredPropertyNameInRequest("Analyzers");

                var command = new PutAnalyzersCommand(Database.Name, GetRaftRequestIdFromQuery());
                foreach (var analyzerToAdd in analyzers)
                {
                    var analyzerDefinition = JsonDeserializationServer.AnalyzerDefinition((BlittableJsonReaderObject)analyzerToAdd);
                    analyzerDefinition.Name = analyzerDefinition.Name?.Trim();

                    if (LoggingSource.AuditLog.IsInfoEnabled)
                    {
                        var clientCert = GetCurrentCertificate();

                        var auditLog = LoggingSource.AuditLog.GetLogger(Database.Name, "Audit");
                        auditLog.Info($"Analyzer {analyzerDefinition.Name} PUT by {clientCert?.Subject} {clientCert?.Thumbprint} with definition: {analyzerToAdd}");
                    }

                    analyzerDefinition.Validate();

                    // check if analyzer is compilable
                    AnalyzerCompiler.Compile(analyzerDefinition.Name, analyzerDefinition.Code);

                    command.Analyzers.Add(analyzerDefinition);
                }

                var index = (await ServerStore.SendToLeaderAsync(command)).Index;

                await Database.RachisLogIndexNotifications.WaitForIndexNotification(index, ServerStore.Engine.OperationTimeout);

                NoContentStatus(HttpStatusCode.Created);
            }
        }

        [RavenAction("/databases/*/admin/analyzers", "DELETE", AuthorizationStatus.DatabaseAdmin)]
        public async Task Delete()
        {
            var name = GetQueryStringValueAndAssertIfSingleAndNotEmpty("name");

            if (LoggingSource.AuditLog.IsInfoEnabled)
            {
                var clientCert = GetCurrentCertificate();

                var auditLog = LoggingSource.AuditLog.GetLogger(Database.Name, "Audit");
                auditLog.Info($"Analyzer {name} DELETE by {clientCert?.Subject} {clientCert?.Thumbprint}");
            }

            var command = new DeleteAnalyzerCommand(name, Database.Name, GetRaftRequestIdFromQuery());
            var index = (await ServerStore.SendToLeaderAsync(command)).Index;

            await Database.RachisLogIndexNotifications.WaitForIndexNotification(index, ServerStore.Engine.OperationTimeout);

            NoContentStatus();
        }
    }
}
