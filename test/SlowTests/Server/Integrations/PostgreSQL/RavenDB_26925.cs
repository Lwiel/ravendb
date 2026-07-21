using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FastTests;
using Raven.Server.Integrations.PostgreSQL.Messages;
using Raven.Server.Integrations.PostgreSQL.VirtualCatalog;
using Tests.Infrastructure;
using Xunit;

namespace SlowTests.Server.Integrations.PostgreSQL;

// pg_class must expose one ordinary-table row per RavenDB collection (relkind 'r', relnamespace = the
// public namespace oid) so JDBC/ODBC drivers - and Tableau's native PostgreSQL connector through them -
// discover tables via pg_catalog. Previously pg_class was an empty CSV snapshot and getTables() returned
// nothing.
public class RavenDB_26925 : RavenTestBase
{
    public RavenDB_26925(ITestOutputHelper output) : base(output)
    {
    }

    // Property order matters: it's the column order RqlQuery/pg_attribute report (id first, json last).
    private class Order
    {
        public string Company { get; set; }
        public System.DateTime OrderedAt { get; set; }
        public double Freight { get; set; }
        public object[] Lines { get; set; }
    }
    private class Company { public string Name { get; set; } }

    [RavenFact(RavenTestCategory.PostgreSql)]
    public async Task Pg_class_lists_collections_as_ordinary_tables()
    {
        using var store = GetDocumentStore();
        await Seed(store);
        var database = await Databases.GetDocumentDatabaseInstanceFor(store);
        var ctx = new VirtualQueryContext { Database = database };

        Assert.True(PgVirtualInterpreter.TryExecute(
            "select relname, relkind, relnamespace from pg_catalog.pg_class order by relname", ctx, out var table));

        var rows = Rows(table);
        Assert.Contains(rows, r => r["relname"] == "Companies" && r["relkind"] == "r" && r["relnamespace"] == "2200");
        Assert.Contains(rows, r => r["relname"] == "Orders" && r["relkind"] == "r" && r["relnamespace"] == "2200");
    }

    [RavenFact(RavenTestCategory.PostgreSql)]
    public async Task GetTables_join_pg_class_to_pg_namespace_returns_collections()
    {
        using var store = GetDocumentStore();
        await Seed(store);
        var database = await Databases.GetDocumentDatabaseInstanceFor(store);
        var ctx = new VirtualQueryContext { Database = database };

        // The shape a JDBC/ODBC driver uses for getTables(): join pg_class to pg_namespace on
        // relnamespace, filter to ordinary tables in the public schema.
        const string sql = """
            select c.relname
            from pg_catalog.pg_class c
            join pg_catalog.pg_namespace n on c.relnamespace = n.oid
            where c.relkind = 'r' and n.nspname = 'public'
            order by c.relname
            """;

        Assert.True(PgVirtualInterpreter.TryExecute(sql, ctx, out var table));

        var names = Rows(table).Select(r => r["relname"]).ToList();
        Assert.Contains("Companies", names);
        Assert.Contains("Orders", names);
    }

    [RavenFact(RavenTestCategory.PostgreSql)]
    public async Task Pg_attribute_lists_collection_columns_joined_to_pg_class()
    {
        using var store = GetDocumentStore();
        await Seed(store);
        var database = await Databases.GetDocumentDatabaseInstanceFor(store);
        var ctx = new VirtualQueryContext { Database = database };

        // The catalog half of getColumns(): pg_attribute joined to pg_class on attrelid = oid. (The full
        // pgJDBC getColumns adds a window function + nullif + pg_get_expr, tracked separately.)
        const string sql = """
            select a.attname, a.atttypid, a.attnum
            from pg_catalog.pg_class c
            join pg_catalog.pg_attribute a on a.attrelid = c.oid
            where c.relname = 'Orders' and a.attnum > 0 and not a.attisdropped
            order by a.attnum
            """;

        Assert.True(PgVirtualInterpreter.TryExecute(sql, ctx, out var table));

        var rows = Rows(table);
        var names = rows.Select(r => r["attname"]).ToList();
        // id (synthetic) first, user fields in document order, then json (synthetic) last - RqlQuery's order.
        Assert.Equal(new[] { "id", "Company", "OrderedAt", "Freight", "Lines", "json" }, names);
        // id is exposed as text (oid 25), matching RqlQuery's RowDescription.
        Assert.Equal("25", rows[0]["atttypid"]);
    }

    // The exact getColumns query pgJDBC 42.7.8 (Tableau's driver) sends, run with its bound parameters.
    // Exercises window function (row_number OVER), nullif, pg_get_expr, subquery-in-FROM, and $N binding.
    [RavenFact(RavenTestCategory.PostgreSql)]
    public async Task GetColumns_pgjdbc_query_returns_columns_with_ordinals()
    {
        using var store = GetDocumentStore();
        await Seed(store);
        var database = await Databases.GetDocumentDatabaseInstanceFor(store);
        var ctx = new VirtualQueryContext
        {
            Database = database,
            Parameters = new Dictionary<string, object> { ["1"] = "public", ["2"] = "Orders", ["3"] = "%" }
        };

        const string sql = """
            SELECT * FROM (SELECT current_database() AS current_database, n.nspname,c.relname,a.attname,a.atttypid,a.attnotnull  OR (t.typtype = 'd' AND t.typnotnull) AS attnotnull,a.atttypmod,a.attlen,t.typtypmod,row_number() OVER (PARTITION BY a.attrelid ORDER BY a.attnum) AS attnum, nullif(a.attidentity, '') as attidentity,nullif(a.attgenerated, '') as attgenerated,pg_catalog.pg_get_expr(def.adbin, def.adrelid) AS adsrc,dsc.description,t.typbasetype,t.typtype  FROM pg_catalog.pg_namespace n  JOIN pg_catalog.pg_class c ON (c.relnamespace = n.oid)  JOIN pg_catalog.pg_attribute a ON (a.attrelid=c.oid)  JOIN pg_catalog.pg_type t ON (a.atttypid = t.oid)  LEFT JOIN pg_catalog.pg_attrdef def ON (a.attrelid=def.adrelid AND a.attnum = def.adnum)  LEFT JOIN pg_catalog.pg_description dsc ON (c.oid=dsc.objoid AND a.attnum = dsc.objsubid)  LEFT JOIN pg_catalog.pg_class dc ON (dc.oid=dsc.classoid AND dc.relname='pg_class')  LEFT JOIN pg_catalog.pg_namespace dn ON (dc.relnamespace=dn.oid AND dn.nspname='pg_catalog')  WHERE c.relkind in ('r','p','v','f','m') and a.attnum > 0 AND NOT a.attisdropped  AND n.nspname LIKE $1 AND c.relname LIKE $2) c WHERE true  AND attname LIKE $3 ORDER BY nspname,c.relname,attnum
            """;

        Assert.True(PgVirtualInterpreter.TryExecute(sql, ctx, out var table));

        var rows = Rows(table);
        var names = rows.Select(r => r["attname"]).ToList();
        // id (synthetic) first, the user fields in document order, json (synthetic) last.
        Assert.Equal(new[] { "id", "Company", "OrderedAt", "Freight", "Lines", "json" }, names);
        // attnum is the window-derived ordinal, 1..N.
        Assert.Equal(new[] { "1", "2", "3", "4", "5", "6" }, rows.Select(r => r["attnum"]).ToList());
    }

    // pgJDBC's getPrimaryKeys query INNER JOINs pg_index. RavenDB has no PKs, so the always-empty pg_index
    // short-circuits the join and the query returns empty instead of hitting the JOIN-not-supported error.
    [RavenFact(RavenTestCategory.PostgreSql)]
    public async Task GetPrimaryKeys_pgjdbc_query_returns_empty()
    {
        using var store = GetDocumentStore();
        await Seed(store);
        var database = await Databases.GetDocumentDatabaseInstanceFor(store);
        var ctx = new VirtualQueryContext
        {
            Database = database,
            Parameters = new Dictionary<string, object> { ["1"] = "public", ["2"] = "Orders" }
        };

        const string sql = """
            SELECT result.TABLE_CAT AS "TABLE_CAT", result.TABLE_SCHEM AS "TABLE_SCHEM", result.TABLE_NAME AS "TABLE_NAME", result.COLUMN_NAME AS "COLUMN_NAME", result.KEY_SEQ AS "KEY_SEQ", result.PK_NAME AS "PK_NAME" FROM (SELECT current_database() AS TABLE_CAT, n.nspname AS TABLE_SCHEM, ct.relname AS TABLE_NAME, a.attname AS COLUMN_NAME, (information_schema._pg_expandarray(i.indkey)).n AS KEY_SEQ, ci.relname AS PK_NAME, information_schema._pg_expandarray(i.indkey) AS KEYS, a.attnum AS A_ATTNUM, i.indnkeyatts as KEY_COUNT FROM pg_catalog.pg_class ct JOIN pg_catalog.pg_attribute a ON (ct.oid = a.attrelid) JOIN pg_catalog.pg_namespace n ON (ct.relnamespace = n.oid) JOIN pg_catalog.pg_index i ON ( a.attrelid = i.indrelid) JOIN pg_catalog.pg_class ci ON (ci.oid = i.indexrelid) WHERE true AND n.nspname = $1 AND ct.relname = $2 AND i.indisprimary) result where result.A_ATTNUM = (result.KEYS).x AND result.KEY_SEQ <= KEY_COUNT ORDER BY result.table_name, result.pk_name, result.key_seq
            """;

        Assert.True(PgVirtualInterpreter.TryExecute(sql, ctx, out var table));
        Assert.Empty(table.Data);
    }

    private static async Task Seed(Raven.Client.Documents.IDocumentStore store)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new Order
        {
            Company = "companies/1",
            OrderedAt = new System.DateTime(2026, 3, 1, 10, 30, 0, System.DateTimeKind.Utc),
            Freight = 12.5,
            Lines = new object[] { new { Product = "products/1", Qty = 3 } }
        });
        await session.StoreAsync(new Company { Name = "RavenDB" });
        await session.SaveChangesAsync();
    }

    private static List<Dictionary<string, string>> Rows(PgTable table)
    {
        var result = new List<Dictionary<string, string>>();
        foreach (var row in table.Data)
        {
            var map = new Dictionary<string, string>();
            for (var c = 0; c < table.Columns.Count; c++)
            {
                var cell = row.ColumnData.Span[c];
                map[table.Columns[c].Name] = cell.HasValue ? Encoding.UTF8.GetString(cell.Value.Span) : null;
            }
            result.Add(map);
        }
        return result;
    }
}
