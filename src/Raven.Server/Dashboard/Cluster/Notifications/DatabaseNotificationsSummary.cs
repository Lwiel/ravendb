using System.Collections.Generic;
using System.Linq;
using Sparrow.Json.Parsing;

namespace Raven.Server.Dashboard.Cluster.Notifications;

public class DatabaseNotificationsSummary
{
    public string DatabaseName { get; set; }
    public List<NotificationSummaryItem> PerformanceHintItems { get; set; } = [];
    public List<NotificationSummaryItem> AlertItems { get; set; } = [];
    
    public DynamicJsonValue ToJson()
    {
        return new DynamicJsonValue
        {
            [nameof(DatabaseName)] = DatabaseName,
            [nameof(PerformanceHintItems)] = new DynamicJsonArray(PerformanceHintItems.Select(x => x.ToJson())),
            [nameof(AlertItems)] = new DynamicJsonArray(AlertItems.Select(x => x.ToJson())),
        };
    }
}

public class NotificationSummaryItem
{
    public string Category { get; set; }
    public long Count { get; set; }
    
    public DynamicJsonValue ToJson()
    {
        return new DynamicJsonValue
        {
            [nameof(Category)] = Category,
            [nameof(Count)] = Count
        };
    }
}
