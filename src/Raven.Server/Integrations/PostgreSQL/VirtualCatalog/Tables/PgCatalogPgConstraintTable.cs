using System.Collections.Generic;
using Raven.Server.Integrations.PostgreSQL.Messages;
using Raven.Server.Integrations.PostgreSQL.Types;

namespace Raven.Server.Integrations.PostgreSQL.VirtualCatalog.Tables
{
    // pg_catalog.pg_constraint: table constraints. Some driver/tool versions (Tableau's included) discover
    // primary keys through pg_constraint (contype 'p', conkey = the key columns) rather than pg_index. Each
    // RavenDB collection is keyed by `id`, so this emits one primary-key constraint per collection over the
    // id column (attnum 1). Mirrors PgCatalogPgIndexTable for the other getPrimaryKeys query shape.
    internal sealed class PgCatalogPgConstraintTable : PgVirtualTable
    {
        private const string PrimaryKeyConType = "p";

        // The key-columns vector as a PG array literal; conkey references the id column (attnum 1).
        private const string PkConKey = "{1}";

        public override string SchemaName => "pg_catalog";
        public override string TableName => "pg_constraint";

        public override IReadOnlyList<PgVirtualColumn> Columns { get; } = new PgVirtualColumn[]
        {
            new("conrelid", PgOid.Default,  PgFormat.Text),
            new("conname",  PgName.Default, PgFormat.Text),
            new("contype",  PgChar.Default, PgFormat.Text),
            new("conkey",   PgText.Default, PgFormat.Text),
        };

        public override IEnumerable<object[]> EnumerateRows(VirtualQueryContext ctx)
        {
            foreach (var relation in PgCatalogRelations.Enumerate(ctx))
            {
                yield return new object[]
                {
                    relation.Oid,                                // conrelid -> the collection
                    PgCatalogRelations.PkIndexName(relation.Name), // conname (e.g. "Orders_pkey")
                    PrimaryKeyConType,                           // contype 'p'
                    PkConKey,                                    // conkey {1} -> the id column
                };
            }
        }
    }
}
