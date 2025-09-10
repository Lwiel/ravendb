using System.Collections.Generic;
using System.Linq;
using Raven.Server.NotificationCenter.Notifications;
using Sparrow.Json.Parsing;

namespace Raven.Server.Dashboard.Cluster.Notifications;

public class DatabaseNotificationsSummary
{
    public string DatabaseName { get; set; }
    
    public List<NotificationSummaryItem> PerformanceHintItems { get; set; }
    public List<NotificationSummaryItem> AlertItems { get; set; }
    
    private Dictionary<PerformanceHintType, int> PerformanceHints { get; set; } = new();
    private Dictionary<AlertType, int> Alerts { get; set; } = new();

    public void AddPerformanceHint(PerformanceHintType hintType)
    {
        if (PerformanceHints.TryAdd(hintType, 1) == false)
            PerformanceHints[hintType]++;
    }
    
    public void AddAlert(AlertType alertType)
    {
        if (Alerts.TryAdd(alertType, 1) == false)
            Alerts[alertType]++;
    }
    
    public DynamicJsonValue ToJson()
    {
        return new DynamicJsonValue
        {
            [nameof(DatabaseName)] = DatabaseName,
            [nameof(PerformanceHints)] = new DynamicJsonArray(
                PerformanceHints.Select(kvp => new DynamicJsonValue
                {
                    [nameof(NotificationSummaryItem.Type)] = kvp.Key.ToString(),
                    [nameof(NotificationSummaryItem.Count)] = kvp.Value
                })),
            [nameof(Alerts)] = new DynamicJsonArray(
                Alerts.Select(kvp => new DynamicJsonValue
                {
                    [nameof(NotificationSummaryItem.Type)] = kvp.Key.ToString(),
                    [nameof(NotificationSummaryItem.Count)] = kvp.Value
                }))
        };
    }
}

public class NotificationSummaryItem
{
    public string Type { get; set; }
    public int Count { get; set; }
}
