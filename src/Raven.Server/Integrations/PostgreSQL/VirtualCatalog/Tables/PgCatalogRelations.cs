using System;
using System.Collections.Generic;
using Raven.Server.Documents;
using Raven.Server.ServerWide.Context;

namespace Raven.Server.Integrations.PostgreSQL.VirtualCatalog.Tables
{
    // Shared enumeration of the user-visible RavenDB collections as PostgreSQL "relations" (pg_class
    // rows). Assigns each collection a synthetic, stable oid so the catalog tables that reference a
    // relation by oid (pg_class.oid, pg_attribute.attrelid, ...) all agree on the same value. JDBC/ODBC
    // drivers discover tables through pg_catalog rather than information_schema, so these rows are what
    // make getTables()/getColumns() return anything.
    internal static class PgCatalogRelations
    {
        // Matches the 'public' row in pg_namespace.csv. RavenDB collections all live in this namespace.
        public const int PublicNamespaceOid = 2200;

        // PG's FirstNormalObjectId - user relations start here, above the fixed system-catalog oids.
        private const int FirstRelationOid = 16384;

        public readonly record struct Relation(string Name, int Oid);

        // Collections sorted by name so oid assignment is deterministic across queries (pg_class and
        // pg_attribute must map a given collection to the same oid within one getColumns() join).
        public static IReadOnlyList<Relation> Enumerate(VirtualQueryContext ctx)
        {
            var result = new List<Relation>();
            if (ctx?.Database == null)
                return result;

            var names = new List<string>();
            using (ctx.Database.DocumentsStorage.ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            using (context.OpenReadTransaction())
            {
                foreach (var collection in ctx.Database.DocumentsStorage.GetCollections(context))
                {
                    if (CollectionName.IsHiLoCollection(collection.Name))
                        continue;
                    names.Add(collection.Name);
                }
            }

            names.Sort(StringComparer.Ordinal);
            for (var i = 0; i < names.Count; i++)
                result.Add(new Relation(names[i], FirstRelationOid + i));

            return result;
        }
    }
}
