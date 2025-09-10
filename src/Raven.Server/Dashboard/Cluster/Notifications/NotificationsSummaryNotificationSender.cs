using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Raven.Server.Json;
using Raven.Server.NotificationCenter;
using Raven.Server.NotificationCenter.Notifications;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;

namespace Raven.Server.Dashboard.Cluster.Notifications;

public class NotificationsSummaryNotificationSender : AbstractClusterDashboardNotificationSender
{
    private readonly DatabasesInfoRetriever _databasesInfoRetriever;
    private readonly NotificationsSummaryRequestConfig _notificationsSummaryRequestConfig;
    
    public NotificationsSummaryNotificationSender(int widgetId, DatabasesInfoRetriever databasesInfoRetriever, ConnectedWatcher watcher, BlittableJsonReaderObject configuration, CancellationToken shutdown) : base(widgetId, watcher, shutdown)
    {
        _databasesInfoRetriever = databasesInfoRetriever;
        _notificationsSummaryRequestConfig = JsonDeserializationServer.NotificationsSummaryRequestConfig(configuration);
    }

    protected override TimeSpan NotificationInterval { get; } = TimeSpan.FromSeconds(5);
    
    protected override AbstractClusterDashboardNotification CreateNotification()
    {
        var notificationsAggregation = new List<DatabaseNotificationsSummary>();

        var x = _databasesInfoRetriever.GetDatabaseNotificationsSummary();
        
        var notificationTableValues = new List<NotificationTableValue>();

        foreach (var value in notificationTableValues)
        {
            if (value.Json.TryGet(nameof(Notification.Database), out string databaseName) == false)
                throw new Exception($"Could not find {nameof(Notification.Database)} property in notification.");
                
            if (value.Json.TryGet(nameof(Notification.Type), out NotificationType notificationType) == false)
                throw new Exception($"Could not find {nameof(Notification.Type)} property in notification.");
                
            if (notificationsAggregation.Any(x => x.DatabaseName == databaseName) == false)
                notificationsAggregation.Add(new DatabaseNotificationsSummary() { DatabaseName = databaseName });

            var databaseAggregation = notificationsAggregation.Single(x => x.DatabaseName == databaseName);

            switch (notificationType)
            {
                case NotificationType.AlertRaised:
                    value.Json.TryGet(nameof(AlertType), out AlertType alertType);
                    databaseAggregation.AddAlert(alertType);
                    break;
                case NotificationType.PerformanceHint:
                    value.Json.TryGet(nameof(PerformanceHintType), out PerformanceHintType performanceHintType);
                    databaseAggregation.AddPerformanceHint(performanceHintType);
                    break;
                default:
                    throw new Exception($"Unexpected notification type: {notificationType}");
            }
        }
        
        return new NotificationsSummaryPayload
        {
            NotificationsSummary = notificationsAggregation
        };
    }
}
