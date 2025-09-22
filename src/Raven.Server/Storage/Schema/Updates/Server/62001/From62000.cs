using System;
using Raven.Server.Documents.Schemas;
using Raven.Server.Json;
using Raven.Server.NotificationCenter.Notifications;
using Sparrow.Json;
using Sparrow.Server;
using Voron;
using Voron.Data.Tables;

namespace Raven.Server.Storage.Schema.Updates.Server
{
    public class From62000 : ISchemaUpdate
    {
        public int From => 62_000;
        public int To => 62_001;
        public SchemaUpgrader.StorageType StorageType => SchemaUpgrader.StorageType.Server;
        
        public static readonly Slice ByCreatedAt;

        public static readonly Slice ByPostponedUntil;

        private static readonly TableSchema LegacyNotificationsSchema = new TableSchema();
        
        private static class LegacyNotificationsTable
        {
            public const int IdIndex = 0;
            public const int CreatedAtIndex = 1;
            public const int PostponedUntilIndex = 2;
            public const int JsonIndex = 3;
        }

        static From62000()
        {
            using (StorageEnvironment.GetStaticContext(out var ctx))
            {
                Slice.From(ctx, "ByCreatedAt", ByteStringType.Immutable, out ByCreatedAt);
                Slice.From(ctx, "ByPostponedUntil", ByteStringType.Immutable, out ByPostponedUntil);
            }
            
            LegacyNotificationsSchema.DefineKey(new TableSchema.IndexDef
            {
                StartIndex = LegacyNotificationsTable.IdIndex,
                Count = 1
            });

            LegacyNotificationsSchema.DefineIndex(new TableSchema.IndexDef // might be the same ticks, so duplicates are allowed - cannot use fixed size index
            {
                StartIndex = LegacyNotificationsTable.CreatedAtIndex,
                Name = ByCreatedAt
            });

            LegacyNotificationsSchema.DefineIndex(new TableSchema.IndexDef // might be the same ticks, so duplicates are allowed - cannot use fixed size index
            {
                StartIndex = LegacyNotificationsTable.PostponedUntilIndex,
                Name = ByPostponedUntil
            });
        }
        
        public unsafe bool Update(UpdateStep step)
        {
            var readTable = step.ReadTx.OpenTable(LegacyNotificationsSchema, Notifications.NotificationsTree);

            if (readTable == null)
                return false;

            var writeTable = step.WriteTx.OpenTable(Notifications.NotificationsSchemaBase, Notifications.NotificationsTree);

            foreach (var existingNotification in readTable.SeekByPrimaryKey(Slices.BeforeAllKeys, 0))
            {
                var readerId = existingNotification.Reader.Id;
                
                using (var jsonContext = JsonOperationContext.ShortTermSingleUse())
                using (TableValueReaderUtil.CloneTableValueReader(jsonContext, existingNotification))
                {
                    var reader = existingNotification.Reader;
                    
                    var id = reader.Read(Notifications.NotificationsTable.IdIndex, out var idSize);
                    var createdAt = reader.Read(Notifications.NotificationsTable.CreatedAtIndex, out var createdAtSize);
                    var postponedUntil = reader.Read(Notifications.NotificationsTable.PostponedUntilIndex, out var postponedUntilSize);
                    var jsonPtr = reader.Read(Notifications.NotificationsTable.JsonIndex, out var jsonSize);

                    var jsonBlittable = new BlittableJsonReaderObject(jsonPtr, jsonSize, jsonContext);

                    jsonBlittable.TryGet("Type", out LazyStringValue notificationTypeLsv);

                    var notificationType = Enum.Parse<NotificationType>(notificationTypeLsv);

                    LazyStringValue notificationCategoryLsv;

                    if (notificationType is NotificationType.AlertRaised)
                        jsonBlittable.TryGet("AlertType", out notificationCategoryLsv);
                    else if (notificationType is NotificationType.PerformanceHint)
                        jsonBlittable.TryGet("HintType", out notificationCategoryLsv);
                    else
                        throw new Exception("Unknown notification type");

                    using (writeTable.Allocate(out TableValueBuilder tvb))
                    {
                        tvb.Add(id, idSize);
                        tvb.Add(createdAt, createdAtSize);
                        tvb.Add(postponedUntil, postponedUntilSize);
                        tvb.Add(jsonPtr, jsonSize);
                        tvb.Add(notificationTypeLsv.Buffer, notificationTypeLsv.Size);
                        tvb.Add(notificationCategoryLsv.Buffer, notificationCategoryLsv.Size);
                        //writeTable.Update(reader.Id, tvb);
                        //writeTable.Set(tvb);
                        writeTable.Delete(readerId);
                        writeTable.Insert(tvb);
                    }
                }
            }

            return false;
        }
    }
}
