using System.Collections.Generic;
using Raven.Server.NotificationCenter.Notifications;

namespace Raven.Server.Dashboard.Cluster;

public class NotificationsSummaryRequestConfig : WidgetRequestConfig
{
    public List<CategoryNamesForNotificationType> CategoryNamesForNotificationType { get; set; }
}

public class CategoryNamesForNotificationType
{
    public NotificationType NotificationType { get; set; }
    public List<string> CategoryNames { get; set; }
}
