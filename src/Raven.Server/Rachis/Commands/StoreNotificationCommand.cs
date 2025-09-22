using System;
using Raven.Server.Documents.TransactionMerger.Commands;
using Raven.Server.NotificationCenter;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;

namespace Raven.Server.Rachis.Commands
{
#if NEW_SCHEMA
    public class StoreNotificationCommand(LazyStringValue id, DateTime createdAt, DateTime? postponedUntil, LazyStringValue notificationType, LazyStringValue notificationCategory, BlittableJsonReaderObject bjro, NotificationsStorage storage)
        : MergedTransactionCommand<ClusterOperationContext, ClusterTransaction>
#else
    public class StoreNotificationCommand(LazyStringValue id, DateTime createdAt, DateTime? postponedUntil, BlittableJsonReaderObject bjro, NotificationsStorage storage)
        : MergedTransactionCommand<ClusterOperationContext, ClusterTransaction>
#endif
    {
        private readonly NotificationsStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));

        protected override long ExecuteCmd(ClusterOperationContext context)
        {
#if NEW_SCHEMA
            _storage.Store(id, createdAt, postponedUntil, notificationType, notificationCategory, bjro, context.Transaction);
#else
            _storage.Store(id, createdAt, postponedUntil, bjro, context.Transaction);
#endif
            return 1;
        }

        public override IReplayableCommandDto<ClusterOperationContext, ClusterTransaction, MergedTransactionCommand<ClusterOperationContext, ClusterTransaction>> ToDto(ClusterOperationContext context)
        {
            throw new NotImplementedException();
        }
    }
}
