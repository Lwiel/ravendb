using System.Collections.Generic;
using Sparrow.Json.Parsing;

namespace Raven.Server.Dashboard;

public sealed class DatabaseNotificationsSummary : AbstractDashboardNotification
{
    public List<DatabaseNotificationsSummaryItem> Items { get; set; }

    public DatabaseNotificationsSummary()
    {
        Items = new List<DatabaseNotificationsSummaryItem>();
    }
}

public sealed class DatabaseNotificationsSummaryItem : IDynamicJson
{
    public string Database { get; set; }
    public Dictionary<string, Dictionary<string, long>> NotificationsCounts { get; set; }
        
    public DynamicJsonValue ToJson()
    {
        var notificationsCountsJson = new DynamicJsonValue();
        if (NotificationsCounts != null)
        {
            foreach (var typeKvp in NotificationsCounts)
            {
                var categoryDict = new DynamicJsonValue();
                foreach (var categoryKvp in typeKvp.Value)
                {
                    categoryDict[categoryKvp.Key] = categoryKvp.Value;
                }
                notificationsCountsJson[typeKvp.Key] = categoryDict;
            }
        }
        
        return new DynamicJsonValue
        {
            [nameof(Database)] = Database,
            [nameof(NotificationsCounts)] = notificationsCountsJson
        };
    }
}
