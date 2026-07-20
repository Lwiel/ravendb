using System.Collections.Generic;
using Raven.Server.Integrations.PostgreSQL.Messages;
using Raven.Server.Integrations.PostgreSQL.Types;

namespace Raven.Server.Integrations.PostgreSQL.VirtualCatalog.Tables
{
    // pg_class: PostgreSQL's catalog of relations (tables, indexes, views, ...). JDBC/ODBC drivers
    // (and Tableau's native PostgreSQL connector through them) enumerate tables by joining pg_class to
    // pg_namespace on relnamespace, filtering relkind, rather than reading information_schema. This
    // table therefore emits one ordinary-table row (relkind 'r') per RavenDB collection, in the public
    // namespace. Previously it was an empty CSV snapshot with no relnamespace column, so getTables()
    // came back empty.
    internal sealed class PgCatalogPgClassTable : PgVirtualTable
    {
        private const string OrdinaryTableRelKind = "r";

        public override string SchemaName => "pg_catalog";
        public override string TableName => "pg_class";

        public override IReadOnlyList<PgVirtualColumn> Columns { get; } = new PgVirtualColumn[]
        {
            new("oid",          PgOid.Default,  PgFormat.Text),
            new("relname",      PgName.Default, PgFormat.Text),
            new("relnamespace", PgOid.Default,  PgFormat.Text),
            new("relkind",      PgChar.Default, PgFormat.Text),
            new("typrelid",     PgOid.Default,  PgFormat.Text),
        };

        public override IEnumerable<object[]> EnumerateRows(VirtualQueryContext ctx)
        {
            foreach (var relation in PgCatalogRelations.Enumerate(ctx))
            {
                // typrelid 0: a collection has no associated composite type row (the Npgsql composite-type
                // loader filters relkind='c', so an ordinary-table row is never picked up there).
                yield return new object[]
                {
                    relation.Oid,
                    relation.Name,
                    PgCatalogRelations.PublicNamespaceOid,
                    OrdinaryTableRelKind,
                    0,
                };
            }
        }
    }
}
