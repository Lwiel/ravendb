using System.Collections.Generic;
using System.Linq;
using Sparrow.Json.Parsing;

namespace Raven.Server.Dashboard.Cluster.Notifications;

public class NotificationsSummaryPayload : AbstractClusterDashboardNotification
{
    public List<DatabaseNotificationsSummary> NotificationsSummary { get; set; } = [];
    
    public override ClusterDashboardNotificationType Type => ClusterDashboardNotificationType.NotificationsSummary;
    
    public override DynamicJsonValue ToJson()
    {
        var json = base.ToJson();
        json[nameof(NotificationsSummary)] = new DynamicJsonArray(NotificationsSummary.Select(x => x.ToJson()));
        return json;
    }
    
    public override DynamicJsonValue ToJsonWithFilter(CanAccessDatabase filter)
    {
        return ToJson();
    }
}
