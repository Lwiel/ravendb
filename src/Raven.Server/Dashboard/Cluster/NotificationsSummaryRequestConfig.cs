using System.Collections.Generic;
using Raven.Server.NotificationCenter.Notifications;

namespace Raven.Server.Dashboard.Cluster;

public class NotificationsSummaryRequestConfig
{
    public List<CategoryNamesForNotificationType> CategoryNamesForNotificationType { get; set; }
}

public class CategoryNamesForNotificationType
{
    public NotificationType NotificationType { get; set; }
    public List<string> CategoryNames { get; set; }
}
