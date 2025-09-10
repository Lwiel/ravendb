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
    public int AlertRaisedNotificationsCount { get; set; }
    public int PerformanceHintNotificationsCount { get; set; }
        
    public DynamicJsonValue ToJson()
    {
        return new DynamicJsonValue
        {
            [nameof(Database)] = Database,
            [nameof(AlertRaisedNotificationsCount)] = AlertRaisedNotificationsCount,
            [nameof(PerformanceHintNotificationsCount)] = PerformanceHintNotificationsCount
        };
    }
}
