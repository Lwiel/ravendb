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

        // Synthetic primary-key index oids live in a disjoint high range so they never collide with the
        // per-collection relation oids (which start at FirstRelationOid and increase by collection count).
        private const int PkIndexOidBase = 1_000_000;

        public readonly record struct Relation(string Name, int Oid);

        // Each collection has a synthetic primary-key index over its `id` column. These helpers give that
        // index a stable oid and name, shared by pg_class (the index row) and pg_index (the PK row) so
        // pgJDBC's getPrimaryKeys join resolves and reports `id` as the primary key.
        public static int PkIndexOid(int relationOid) => PkIndexOidBase + relationOid;
        public static string PkIndexName(string collectionName) => collectionName + "_pkey";

        // The `id` column is always attribute number 1 (pg_attribute emits it first), so a single-column
        // primary key over it has this indkey vector.
        public const string PkIndKey = "1";

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
