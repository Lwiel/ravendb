using System;
using System.Linq;
using System.Threading;
using Raven.Server.Json;
using Raven.Server.NotificationCenter;
using Raven.Server.NotificationCenter.Notifications;
using Sparrow.Json;

namespace Raven.Server.Dashboard.Cluster.Notifications;

public class NotificationsSummaryNotificationSender : AbstractClusterDashboardNotificationSender
{
    private readonly DatabasesInfoRetriever _databasesInfoRetriever;
    private NotificationsSummaryRequestConfig _notificationsSummaryRequestConfig;
    
    public NotificationsSummaryNotificationSender(int widgetId, DatabasesInfoRetriever databasesInfoRetriever, ConnectedWatcher watcher, BlittableJsonReaderObject configuration, CancellationToken shutdown) : base(widgetId, watcher, shutdown)
    {
        _databasesInfoRetriever = databasesInfoRetriever;
        _notificationsSummaryRequestConfig = JsonDeserializationServer.NotificationsSummaryRequestConfig(configuration);
    }

    protected override TimeSpan NotificationInterval { get; } = TimeSpan.FromSeconds(5);
    
    protected override AbstractClusterDashboardNotification CreateNotification()
    {
        var databasesNotificationsSummary = _databasesInfoRetriever.GetDatabaseNotificationsSummary();

        var notificationsSummaryPayload = new NotificationsSummaryPayload();

        foreach (var item in databasesNotificationsSummary.Items)
        {
            var databaseNotificationsSummary = new DatabaseNotificationsSummary()
            {
                DatabaseName = item.Database
            };

            foreach (var notificationCountsKvp in item.NotificationsCounts)
            {
                var notificationType = Enum.Parse<NotificationType>(notificationCountsKvp.Key);
                
                if (_notificationsSummaryRequestConfig.CategoryNamesForNotificationType.Any(x => x.NotificationType == notificationType) == false)
                    continue;
                
                var categoriesForNotificationType = _notificationsSummaryRequestConfig.CategoryNamesForNotificationType.Single(x => x.NotificationType == notificationType);
                
                foreach (var notificationCategoryCount in notificationCountsKvp.Value)
                {
                    if (categoriesForNotificationType.CategoryNames.Count != 0 && categoriesForNotificationType.CategoryNames.Contains(notificationCategoryCount.Key) == false)
                        continue;
                    
                    var notificationSummaryItem = new NotificationSummaryItem
                    {
                        Category = notificationCategoryCount.Key,
                        Count = notificationCategoryCount.Value
                    };

                    switch (notificationType)
                    {
                        case NotificationType.AlertRaised:
                            databaseNotificationsSummary.AlertItems.Add(notificationSummaryItem);
                            break;
                        case NotificationType.PerformanceHint:
                            databaseNotificationsSummary.PerformanceHintItems.Add(notificationSummaryItem);
                            break;
                        default:
                            throw new Exception($"Unsupported {nameof(NotificationType)}: {notificationType}");
                    }
                }
            }
            
            notificationsSummaryPayload.NotificationsSummary.Add(databaseNotificationsSummary);
        }

        return notificationsSummaryPayload;
    }

    internal override void UpdateConfiguration(BlittableJsonReaderObject configuration)
    {
        _notificationsSummaryRequestConfig = JsonDeserializationServer.NotificationsSummaryRequestConfig(configuration);
    }
}
