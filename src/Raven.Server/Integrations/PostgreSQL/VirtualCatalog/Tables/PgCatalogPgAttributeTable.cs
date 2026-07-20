using System.Collections.Generic;
using Raven.Server.Integrations.PostgreSQL.Messages;
using Raven.Server.Integrations.PostgreSQL.Types;
using Raven.Server.Utils;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;

namespace Raven.Server.Integrations.PostgreSQL.VirtualCatalog.Tables
{
    // pg_catalog.pg_attribute: one row per column of every relation. JDBC/ODBC drivers read it (joined to
    // pg_class on attrelid and pg_type on atttypid) to implement getColumns(). RavenDB is schemaless, so a
    // collection's "columns" are inferred by sampling its first document - the same way information_schema.columns
    // and RqlQuery's RowDescription do it; the three MUST agree (a driver joins pg_attribute to the data it
    // then SELECTs). attrelid comes from the shared PgCatalogRelations oid so it matches pg_class.oid.
    internal sealed class PgCatalogPgAttributeTable : PgVirtualTable
    {
        public override string SchemaName => "pg_catalog";
        public override string TableName => "pg_attribute";

        public override IReadOnlyList<PgVirtualColumn> Columns { get; } = new PgVirtualColumn[]
        {
            new("attrelid",     PgOid.Default,  PgFormat.Text),
            new("attname",      PgName.Default, PgFormat.Text),
            new("atttypid",     PgOid.Default,  PgFormat.Text),
            new("attnum",       PgInt2.Default, PgFormat.Text),
            new("attnotnull",   PgBool.Default, PgFormat.Text),
            new("atttypmod",    PgInt4.Default, PgFormat.Text),
            new("attlen",       PgInt2.Default, PgFormat.Text),
            new("attisdropped", PgBool.Default, PgFormat.Text),
            new("attidentity",  PgChar.Default, PgFormat.Text),
            new("attgenerated", PgChar.Default, PgFormat.Text),
        };

        public override IEnumerable<object[]> EnumerateRows(VirtualQueryContext ctx)
        {
            var relations = PgCatalogRelations.Enumerate(ctx);
            if (relations.Count == 0)
                yield break;

            using (ctx.Database.DocumentsStorage.ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            using (context.OpenReadTransaction())
            {
                foreach (var relation in relations)
                {
                    BlittableJsonReaderObject sample = null;
                    foreach (var doc in ctx.Database.DocumentsStorage.GetDocumentsFrom(context, relation.Name, etag: 0, start: 0, take: 1))
                    {
                        sample = doc.Data;
                        break;
                    }
                    if (sample == null)
                        continue;

                    short attnum = 1;

                    // Synthetic id column first, then user columns in document order, then json last -
                    // identical ordering to information_schema.columns and RqlQuery's RowDescription.
                    foreach (var row in Attribute(relation.Oid, PgSyntheticColumns.DocumentId, PgText.Default, ref attnum))
                        yield return row;

                    var prop = default(BlittableJsonReaderObject.PropertyDetails);
                    foreach (var name in sample.GetPropertyNames())
                    {
                        if (string.IsNullOrEmpty(name) || name.StartsWith('@'))
                            continue;
                        var propIdx = sample.GetPropertyIndex(name);
                        if (propIdx == -1)
                            continue;
                        sample.GetPropertyByIndex(propIdx, ref prop);

                        foreach (var row in Attribute(relation.Oid, name, ResolvePgType(prop.Token, prop.Value), ref attnum))
                            yield return row;
                    }

                    foreach (var row in Attribute(relation.Oid, PgSyntheticColumns.Json, PgJson.Default, ref attnum))
                        yield return row;
                }
            }
        }

        // attnotnull false: RavenDB documents are schemaless, so no column is guaranteed present.
        // atttypmod -1 / attidentity '' / attgenerated '': no type modifier, not an identity/generated column.
        private static IEnumerable<object[]> Attribute(int attrelid, string name, PgType type, short attnum)
        {
            yield return new object[]
            {
                attrelid, name, type.Oid, attnum, false, -1, (int)type.Size, false, "", "",
            };
        }

        // A ref-counting overload isn't allowed in an iterator, so bump attnum here and delegate.
        private static IEnumerable<object[]> Attribute(int attrelid, string name, PgType type, ref short attnum)
        {
            var rows = Attribute(attrelid, name, type, attnum);
            attnum++;
            return rows;
        }

        // Mirrors information_schema.columns.MapDataType / RqlQuery's token-to-PgType mapping, but yields the
        // PgType (its Oid feeds atttypid, its Size feeds attlen). Keep in sync with those two.
        private static PgType ResolvePgType(BlittableJsonToken token, object value)
        {
            var bjt = token & BlittableJsonToken.TypesMask;

            if (bjt is BlittableJsonToken.String or BlittableJsonToken.CompressedString)
            {
                var processedString = bjt == BlittableJsonToken.CompressedString
                    ? (string)(LazyCompressedStringValue)value
                    : (string)(LazyStringValue)value;

                if (processedString != null && TypeConverter.TryConvertStringValue(processedString, out var parsed))
                {
                    switch (parsed)
                    {
                        case System.DateTime dt:
                            return dt.Kind == System.DateTimeKind.Utc ? PgTimestampTz.Default : (PgType)PgTimestamp.Default;
                        case System.DateTimeOffset:
                            return PgTimestampTz.Default;
                        case System.TimeSpan:
                            return PgInterval.Default;
                    }
                }

                return PgText.Default;
            }

            return bjt switch
            {
                BlittableJsonToken.Integer    => PgInt8.Default,
                BlittableJsonToken.LazyNumber => PgFloat8.Default,
                BlittableJsonToken.Boolean    => PgBool.Default,
                _                             => PgJson.Default, // objects, arrays, null, unknown
            };
        }
    }
}
