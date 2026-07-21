using System.Collections.Generic;
using Raven.Server.Integrations.PostgreSQL.Messages;
using Raven.Server.Integrations.PostgreSQL.Types;

namespace Raven.Server.Integrations.PostgreSQL.VirtualCatalog.Tables
{
    // pg_catalog.pg_index: one row per index. RavenDB has no relational indexes, but every collection is
    // keyed by its document `id`, which is the natural primary key. So this emits one primary-key index
    // per collection (over the `id` column, attnum 1). pgJDBC's getPrimaryKeys reads this (joined to
    // pg_class for the index name and pg_attribute for the column name) to report `id` as the PK, which is
    // what lets Tableau offer relationships between collections (e.g. Orders.Company -> Companies.id).
    internal sealed class PgCatalogPgIndexTable : PgVirtualTable
    {
        public override string SchemaName => "pg_catalog";
        public override string TableName => "pg_index";

        public override IReadOnlyList<PgVirtualColumn> Columns { get; } = new PgVirtualColumn[]
        {
            new("indexrelid",  PgOid.Default,  PgFormat.Text),
            new("indrelid",    PgOid.Default,  PgFormat.Text),
            new("indisprimary", PgBool.Default, PgFormat.Text),
            new("indkey",      PgText.Default, PgFormat.Text),
            new("indnkeyatts", PgInt2.Default, PgFormat.Text),
        };

        public override IEnumerable<object[]> EnumerateRows(VirtualQueryContext ctx)
        {
            foreach (var relation in PgCatalogRelations.Enumerate(ctx))
            {
                yield return new object[]
                {
                    PgCatalogRelations.PkIndexOid(relation.Oid), // indexrelid -> the pg_class index row
                    relation.Oid,                                // indrelid   -> the collection
                    true,                                        // indisprimary
                    PgCatalogRelations.PkIndKey,                 // indkey "1" -> the id column (attnum 1)
                    (short)1,                                    // indnkeyatts (single-column key)
                };
            }
        }
    }
}
