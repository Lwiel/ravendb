using Raven.Server.Documents.Schemas;
using Sparrow.Json;
using Voron;
using Voron.Data.Tables;

/*
namespace Raven.Server.Storage.Schema.Updates.Configuration;

public sealed class From50000 : ISchemaUpdate
{
    public int From => 50_000;
    public int To => 62_000;
    public SchemaUpgrader.StorageType StorageType => SchemaUpgrader.StorageType.Configuration;

    public unsafe bool Update(UpdateStep step)
    {
        var table = step.WriteTx.OpenTable(Notifications.NotificationsSchemaBase, Notifications.NotificationsTree);

        if (table == null)
            return false;

        foreach (var existingNotification in table.SeekByPrimaryKey(Slices.BeforeAllKeys, 0))
        {
            var reader = existingNotification.Reader;

            var id = reader.Read(Notifications.NotificationsTable.IdIndex, out var idSize);
            var createdAt = reader.Read(Notifications.NotificationsTable.CreatedAtIndex, out var createdAtSize);
            var postponedUntil = reader.Read(Notifications.NotificationsTable.PostponedUntilIndex, out var postponedUntilSize);
            var jsonPtr = reader.Read(Notifications.NotificationsTable.JsonIndex, out var jsonSize);

            using (var context = JsonOperationContext.ShortTermSingleUse())
            {
                var jsonBlittable = new BlittableJsonReaderObject(jsonPtr, jsonSize, context);

                jsonBlittable.TryGet("Type", out LazyStringValue type);

                using (table.Allocate(out TableValueBuilder tvb))
                {
                    tvb.Add(id, idSize);
                    tvb.Add(createdAt, createdAtSize);
                    tvb.Add(postponedUntil, postponedUntilSize);
                    tvb.Add(jsonPtr, jsonSize);
                    //tvb.Add(notificationType);
                    //tvb.Add(categoryName);
                    table.Update(reader.Id, tvb);
                }
            }
        }

        return false;
    }
}
*/
