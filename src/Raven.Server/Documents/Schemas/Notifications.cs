using Sparrow.Server;
using Voron;
using Voron.Data.Tables;

namespace Raven.Server.Documents.Schemas;

public static class Notifications
{
    public static TableSchema Current => NotificationsSchemaBase;

    public static TableSchema NotificationsSchemaBase = new();

    public static readonly Slice ByCreatedAt;

    public static readonly Slice ByPostponedUntil;

    public static readonly Slice ByNotificationType;
    
    public static readonly Slice ByCategoryName;

    public const string NotificationsTree = "Notifications";

    public static class NotificationsTable
    {
#pragma warning disable 169
        public const int IdIndex = 0;
        public const int CreatedAtIndex = 1;
        public const int PostponedUntilIndex = 2;
        public const int JsonIndex = 3;
        public const int NotificationTypeIndex = 4;
        public const int CategoryNameIndex = 5;
#pragma warning restore 169
    }

    static Notifications()
    {
        using (StorageEnvironment.GetStaticContext(out var ctx))
        {
            Slice.From(ctx, "ByCreatedAt", ByteStringType.Immutable, out ByCreatedAt);
            Slice.From(ctx, "ByPostponedUntil", ByteStringType.Immutable, out ByPostponedUntil);
            Slice.From(ctx, "ByNotificationType", ByteStringType.Immutable, out ByNotificationType);
            Slice.From(ctx, "ByCategoryName", ByteStringType.Immutable, out ByCategoryName);
        }

        NotificationsSchemaBase.DefineKey(new TableSchema.IndexDef
        {
            StartIndex = NotificationsTable.IdIndex,
            Count = 1
        });

        NotificationsSchemaBase.DefineIndex(new TableSchema.IndexDef // might be the same ticks, so duplicates are allowed - cannot use fixed size index
        {
            StartIndex = NotificationsTable.CreatedAtIndex,
            Name = ByCreatedAt
        });

        NotificationsSchemaBase.DefineIndex(new TableSchema.IndexDef // might be the same ticks, so duplicates are allowed - cannot use fixed size index
        {
            StartIndex = NotificationsTable.PostponedUntilIndex,
            Name = ByPostponedUntil
        });
        
        NotificationsSchemaBase.DefineIndex(new TableSchema.IndexDef
        {
            StartIndex = NotificationsTable.NotificationTypeIndex,
            Name = ByNotificationType
        });
        
        NotificationsSchemaBase.DefineIndex(new TableSchema.IndexDef
        {
            StartIndex = NotificationsTable.CategoryNameIndex,
            Name = ByCategoryName
        });
    }
}
