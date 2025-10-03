using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastTests;
using Raven.Server;
using Raven.Server.Config.Settings;
using Raven.Server.Dashboard.Cluster.Notifications;
using Raven.Server.Documents;
using Raven.Server.NotificationCenter.Notifications;
using Raven.Server.NotificationCenter.Notifications.Details;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.SlowTests.Issues;

public class RavenDB_24424 : RavenTestBase
{
    public RavenDB_24424(ITestOutputHelper output) : base(output)
    {
    }
    
    [RavenFact(RavenTestCategory.Monitoring)]
    public void TestBackup()
    {
        var serverOptions = new ServerCreationOptions { RunInMemory = false, DataDirectory = NewDataPath(forceCreateDir: true) };

        using (var server = GetNewServer(serverOptions))
        {
            var storeOptions = new Options() { RunInMemory = false, DeleteDatabaseOnDispose = false, Server = server };
            
            using (var store = GetDocumentStore(storeOptions))
            {
                var database = GetDatabase(store.Database, server).Result;

                CreateNotifications(server, database);

                using (database.NotificationCenter.GetStored(out var databaseActions))
                {
                    Assert.Equal(4, databaseActions.Count());
                }

                using (server.ServerStore.NotificationCenter.GetStored(out var serverActions))
                {
                    Assert.True(serverActions.Count() >= 2);
                }

                WaitForUserToContinueTheTest(store);
            }
        }
    }

    [RavenTheory(RavenTestCategory.Monitoring)]
    [InlineData("SchemaUpgrade/Issues/SystemVersion/RavenDB-24424_pre_schema_upgrade.zip")]
    private void LoadTest(string filePath)
    {
        var folder = NewDataPath(forceCreateDir: true, prefix: Guid.NewGuid().ToString());
            
        var zipPath = new PathSetting(filePath);
        Assert.True(File.Exists(zipPath.FullPath));
            
        ZipFile.ExtractToDirectory(filePath, folder);

        using (var server = GetNewServer(new ServerCreationOptions { DeletePrevious = false, RunInMemory = false, DataDirectory = folder, RegisterForDisposal = false }))
        {
            var storeOptions = new Options() { RunInMemory = false, DeleteDatabaseOnDispose = false, Server = server, ModifyDatabaseName = _ => "TestBackup_1"};

            using (var store = GetDocumentStore(storeOptions))
            {
                var database = GetDatabase(store.Database, server).Result;
                    
                using (database.NotificationCenter.GetStored(out var databaseActions))
                {
                    var notificationTableValues = databaseActions.ToList();
                    Assert.Equal(4, notificationTableValues.Count);

                    Assert.False(string.IsNullOrEmpty(notificationTableValues.ToList()[0].CategoryName));
                    Assert.False(string.IsNullOrEmpty(notificationTableValues.ToList()[1].CategoryName));
                    Assert.False(string.IsNullOrEmpty(notificationTableValues.ToList()[2].CategoryName));
                    Assert.False(string.IsNullOrEmpty(notificationTableValues.ToList()[3].CategoryName));
                        
                    Assert.False(string.IsNullOrEmpty(notificationTableValues.ToList()[0].NotificationType));
                    Assert.False(string.IsNullOrEmpty(notificationTableValues.ToList()[1].NotificationType));
                    Assert.False(string.IsNullOrEmpty(notificationTableValues.ToList()[2].NotificationType));
                    Assert.False(string.IsNullOrEmpty(notificationTableValues.ToList()[3].NotificationType));
                }

                using (server.ServerStore.NotificationCenter.GetStored(out var serverActions))
                {
                    var notificationTableValues = serverActions.ToList();
                    
                    // We may store an AGPL notification before reading from notifications storage
                    Assert.True(notificationTableValues.Count >= 2);

                    foreach (var notificationTableValue in notificationTableValues)
                    {
                        Assert.False(string.IsNullOrEmpty(notificationTableValue.CategoryName));
                        Assert.False(string.IsNullOrEmpty(notificationTableValue.NotificationType));
                    }
                }
            }
        }
    }
        
    private static AlertRaised GetSampleAlert(string databaseName, string title, string message, AlertType type)
    {
        return AlertRaised.Create(
            databaseName,
            title, 
            message,
            type,
            NotificationSeverity.Info,
            key: "Key",
            details: new ExceptionDetails(new Exception("Error message")));
    }

    private static PerformanceHint GetSamplePerformanceHint(string databaseName, string title, string message, PerformanceHintType type, string source)
    {
        return PerformanceHint.Create(databaseName, title, message, type, NotificationSeverity.Info, source);
    }
    
    private static void CreateNotifications(RavenServer server, DocumentDatabase database)
    {
        var serverReplicationAlert = GetSampleAlert(null, "ServerAlert", "This is a server alert", AlertType.Replication);
        var databaseReplicationAlert = GetSampleAlert(database.Name, "DatabaseAlert", "This is a database alert", AlertType.Replication);
        
        var serverPagingHint = GetSamplePerformanceHint(null, "ServerHint", "This is a server performance hint", PerformanceHintType.Paging, "source_1");
        var databasePagingHint = GetSamplePerformanceHint(database.Name, "DatabaseHint", "This is a database performance hint", PerformanceHintType.Paging, "source_1");
        var databaseReplicationHint1 = GetSamplePerformanceHint(database.Name, "DatabaseHint", "This is a database replication hint 1", PerformanceHintType.Replication, "source_1");
        var databaseReplicationHint2 = GetSamplePerformanceHint(database.Name, "DatabaseHint", "This is a database replication hint 2", PerformanceHintType.Replication, "source_2");

        server.ServerStore.NotificationCenter.Add(serverReplicationAlert);
        database.NotificationCenter.Add(databaseReplicationAlert);
        
        server.ServerStore.NotificationCenter.Add(serverPagingHint);
        database.NotificationCenter.Add(databasePagingHint);
        database.NotificationCenter.Add(databaseReplicationHint1);
        database.NotificationCenter.Add(databaseReplicationHint2);
    }

    [RavenFact(RavenTestCategory.Monitoring)]
    public async Task TestDashboard()
    {
        using (var store = GetDocumentStore())
        {
            var database = GetDatabase(store.Database).Result;
            
            CreateNotifications(Server, database);
            
            var serverUrl = Server.WebUrl;

            using (var ws = new ClientWebSocket())
            {
                var uri = new Uri($"{serverUrl.Replace("http", "ws")}/cluster-dashboard/watch?node=A&fromStudio=true");
                await ws.ConnectAsync(uri, CancellationToken.None);
                Assert.Equal(WebSocketState.Open, ws.State);

                var watchCommandRequest = new WatchCommandRequest
                {
                    Command = "watch",
                    Id = 14,
                    Type = "NotificationsSummary",
                    Config = new WatchCommandRequest.ConfigData()
                    {
                        CategoryNamesForNotificationType = new List<WatchCommandRequest.CategoryNamesForNotificationTypeData>()
                        {
                            new()
                            {
                                NotificationType = "AlertRaised"
                            },
                            new()
                            {
                                NotificationType = "PerformanceHint"
                            }
                        }
                    }
                };
                
                var watchCommandJson = JsonSerializer.Serialize(watchCommandRequest);
                var buffer = Encoding.UTF8.GetBytes(watchCommandJson);
                await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                
                var receiveBuffer = new byte[4096];
                WatchCommandResponse response = null;
                
                Assert.Equal(true, WaitForValue(() =>
                {
                    var result = ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None).GetAwaiter().GetResult();
                    var responseString = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
                    
                    if (responseString.Contains("Id") == false)
                        return false;
                    
                    response = JsonSerializer.Deserialize<WatchCommandResponse>(responseString);

                    if (response.Data?.Type == "NotificationsSummary")
                        return true;
                    
                    return false;
                }, true));

                Assert.Equal(1, response.Data.NotificationsSummary.Count);

                var alerts = response.Data.NotificationsSummary.Single().AlertItems;
                var performanceHints = response.Data.NotificationsSummary.Single().PerformanceHintItems;
                
                Assert.Equal(1, alerts.Count);
                Assert.Equal(2, performanceHints.Count);

                var replicationAlerts = alerts.Single(x => x.Category == AlertType.Replication.ToString());
                var replicationPerformanceHints = performanceHints.Single(x => x.Category == PerformanceHintType.Replication.ToString());
                var pagingPerformanceHints = performanceHints.Single(x => x.Category == PerformanceHintType.Paging.ToString());
                
                Assert.Equal(1, replicationAlerts.Count);
                Assert.Equal(2, replicationPerformanceHints.Count);
                Assert.Equal(1, pagingPerformanceHints.Count);
                
                watchCommandRequest = new WatchCommandRequest
                {
                    Command = "update-config",
                    Id = 14,
                    Config = new WatchCommandRequest.ConfigData()
                    {
                        CategoryNamesForNotificationType = new List<WatchCommandRequest.CategoryNamesForNotificationTypeData>()
                        {
                            new()
                            {
                                NotificationType = "PerformanceHint",
                                CategoryNames = [ "Paging" ]
                            }
                        }
                    }
                };
                
                watchCommandJson = JsonSerializer.Serialize(watchCommandRequest);
                buffer = Encoding.UTF8.GetBytes(watchCommandJson);
                await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                
                Assert.Equal(true, WaitForValue(() =>
                {
                    var result = ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None).GetAwaiter().GetResult();
                    var responseString = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
                    
                    if (responseString.Contains("Id") == false)
                        return false;
                    
                    response = JsonSerializer.Deserialize<WatchCommandResponse>(responseString);

                    if (response.Data?.Type == "NotificationsSummary")
                        return true;
                    
                    return false;
                }, true));
                
                Assert.Equal(1, response.Data.NotificationsSummary.Count);

                alerts = response.Data.NotificationsSummary.Single().AlertItems;
                performanceHints = response.Data.NotificationsSummary.Single().PerformanceHintItems;
                
                Assert.Empty(alerts);
                Assert.Equal(1, performanceHints.Count);
                
                pagingPerformanceHints = performanceHints.Single(x => x.Category == PerformanceHintType.Paging.ToString());
                
                Assert.Equal(1, pagingPerformanceHints.Count);
            }
        }
    }
    
    private class WatchCommandRequest
    {
        public string Command { get; set; }
        public int Id { get; set; }
        public string Type { get; set; }
        public ConfigData Config { get; set; }

        public class ConfigData
        {
            public List<CategoryNamesForNotificationTypeData> CategoryNamesForNotificationType { get; set; }
        }

        public class CategoryNamesForNotificationTypeData
        {
            public string NotificationType { get; set; }
            public List<string> CategoryNames { get; set; }
        }
    }

    private class WatchCommandResponse
    {
        public int Id { get; set; }
        public DashboardData Data { get; set; }

        public class DashboardData
        {
            public string Type { get; set; }
            public DateTime Date { get; set; }
            public List<DatabaseNotificationsSummary> NotificationsSummary { get; set; }
        }
    }
}
