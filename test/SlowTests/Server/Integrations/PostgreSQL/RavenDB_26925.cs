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

    private class Order { public string Company { get; set; } }
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
        // id (synthetic) first, the user field, then json (synthetic) last - RqlQuery's column order.
        Assert.Equal(new[] { "id", "Company", "json" }, names);
        // id is exposed as text (oid 25), matching RqlQuery's RowDescription.
        Assert.Equal("25", rows[0]["atttypid"]);
    }

    private static async Task Seed(Raven.Client.Documents.IDocumentStore store)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new Order { Company = "companies/1" });
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
