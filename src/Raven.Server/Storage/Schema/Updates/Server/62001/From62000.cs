using System;
using Raven.Server.Documents.Schemas;
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

        private static readonly TableSchema LegacyNotificationsSchema = new TableSchema();
        private static readonly TableSchema NewNotificationsSchema = new TableSchema();

        private const string OldNotificationsTableName = "Notifications";
        
        private const string TypePropertyName = "Type";
        
        private static class LegacyNotificationsTable
        {
            public const int IdIndex = 0;
            public const int CreatedAtIndex = 1;
            public const int PostponedUntilIndex = 2;
            public const int JsonIndex = 3;
        }

        private static class NewNotificationsTable
        {
            public const int IdIndex = 0;
            public const int CreatedAtIndex = 1;
            public const int PostponedUntilIndex = 2;
            public const int JsonIndex = 3;
            public const int NotificationTypeIndex = 4;
            public const int CategoryNameIndex = 5;
        }

        static From62000()
        {
            Slice byCreatedAt;
            Slice byPostponedUntil;
            Slice byNotificationType;
            Slice byCategoryName;
            
            using (StorageEnvironment.GetStaticContext(out var ctx))
            {
                Slice.From(ctx, "ByCreatedAt", ByteStringType.Immutable, out byCreatedAt);
                Slice.From(ctx, "ByPostponedUntil", ByteStringType.Immutable, out byPostponedUntil);
                Slice.From(ctx, "ByNotificationType", ByteStringType.Immutable, out byNotificationType);
                Slice.From(ctx, "ByCategoryName", ByteStringType.Immutable, out byCategoryName);
            }
            
            LegacyNotificationsSchema.DefineKey(new TableSchema.IndexDef
            {
                StartIndex = LegacyNotificationsTable.IdIndex,
                Count = 1
            });

            LegacyNotificationsSchema.DefineIndex(new TableSchema.IndexDef // might be the same ticks, so duplicates are allowed - cannot use fixed size index
            {
                StartIndex = LegacyNotificationsTable.CreatedAtIndex,
                Name = byCreatedAt
            });

            LegacyNotificationsSchema.DefineIndex(new TableSchema.IndexDef // might be the same ticks, so duplicates are allowed - cannot use fixed size index
            {
                StartIndex = LegacyNotificationsTable.PostponedUntilIndex,
                Name = byPostponedUntil
            });

            NewNotificationsSchema.DefineKey(new TableSchema.IndexDef
            {
                StartIndex = NewNotificationsTable.IdIndex,
                Count = 1
            });

            NewNotificationsSchema.DefineIndex(new TableSchema.IndexDef
            {
                StartIndex = NewNotificationsTable.CreatedAtIndex,
                Name = byCreatedAt
            });

            NewNotificationsSchema.DefineIndex(new TableSchema.IndexDef
            {
                StartIndex = NewNotificationsTable.PostponedUntilIndex,
                Name = byPostponedUntil
            });
            
            NewNotificationsSchema.DefineIndex(new TableSchema.IndexDef
            {
                StartIndex = NewNotificationsTable.NotificationTypeIndex,
                Name = byNotificationType
            });
        
            NewNotificationsSchema.DefineIndex(new TableSchema.IndexDef
            {
                StartIndex = NewNotificationsTable.CategoryNameIndex,
                Name = byCategoryName
            });
        }
        
        private static string GetOldTableName(string resourceName)
        {
            return string.IsNullOrEmpty(resourceName)
                ? OldNotificationsTableName
                : $"{OldNotificationsTableName}.{resourceName.ToLowerInvariant()}";
        }
        
        private static string GetNewTableName(string resourceName)
        {
            return string.IsNullOrEmpty(resourceName)
                ? $"{OldNotificationsTableName}.Server"
                : $"{OldNotificationsTableName}.Database.{resourceName.ToLowerInvariant()}";
        }
        
        public bool Update(UpdateStep step)
        {
            var databaseNames = SchemaUpgradeExtensions.GetDatabases(step);

            foreach (var databaseName in databaseNames)
            {
                if (ProcessResource(step, databaseName) == false)
                    return false;
            }
            
            return ProcessResource(step, resourceName: null);
        }
        
        private static unsafe bool ProcessResource(UpdateStep step, string resourceName)
        {
            var oldTableName = GetOldTableName(resourceName);
            var newTableName = GetNewTableName(resourceName);
            
            var readTable = step.ReadTx.OpenTable(LegacyNotificationsSchema, oldTableName);

            if (readTable == null)
                return false;
            
            Notifications.NotificationsSchemaBase.Create(step.WriteTx, newTableName, 16);
            var writeTable = step.WriteTx.OpenTable(NewNotificationsSchema, newTableName);
            var deleteTable = step.WriteTx.OpenTable(LegacyNotificationsSchema, oldTableName);
            
            foreach (var existingNotification in readTable.SeekByPrimaryKey(Slices.BeforeAllKeys, 0))
            {
                var readerId = existingNotification.Reader.Id;
                
                using (var jsonContext = JsonOperationContext.ShortTermSingleUse())
                {
                    var reader = existingNotification.Reader;
                    
                    var id = reader.Read(LegacyNotificationsTable.IdIndex, out var idSize);
                    var createdAt = reader.Read(LegacyNotificationsTable.CreatedAtIndex, out var createdAtSize);
                    var postponedUntil = reader.Read(LegacyNotificationsTable.PostponedUntilIndex, out var postponedUntilSize);
                    var jsonPtr = reader.Read(LegacyNotificationsTable.JsonIndex, out var jsonSize);

                    var jsonBlittable = new BlittableJsonReaderObject(jsonPtr, jsonSize, jsonContext);

                    if (jsonBlittable.TryGet(TypePropertyName, out LazyStringValue notificationTypeLsv) == false)
                        throw new Exception($"Couldn't find {TypePropertyName} property in notification json.");

                    if (Enum.TryParse<NotificationType>(notificationTypeLsv, out var notificationType) == false)
                        throw new Exception($"Unexpected {nameof(NotificationType)}: {notificationTypeLsv}");

                    LazyStringValue notificationCategoryLsv;

                    switch (notificationType)
                    {
                        case NotificationType.AlertRaised:
                            jsonBlittable.TryGet("AlertType", out notificationCategoryLsv);
                            break;
                        case NotificationType.PerformanceHint:
                            jsonBlittable.TryGet("HintType", out notificationCategoryLsv);
                            break;
                        default:
                            notificationCategoryLsv = null;
                            break;
                    }

                    using (writeTable.Allocate(out TableValueBuilder tvb))
                    {
                        tvb.Add(id, idSize);
                        tvb.Add(createdAt, createdAtSize);
                        tvb.Add(postponedUntil, postponedUntilSize);
                        tvb.Add(jsonPtr, jsonSize);
                        tvb.Add(notificationTypeLsv.Buffer, notificationTypeLsv.Size);
                        
                        if (notificationCategoryLsv != null)
                            tvb.Add(notificationCategoryLsv.Buffer, notificationCategoryLsv.Size);
                        
                        writeTable.Insert(tvb);
                        deleteTable.Delete(readerId);
                    }
                }
            }
            
            step.WriteTx.DeleteTable(oldTableName);
            
            return true;
        }
    }
}
