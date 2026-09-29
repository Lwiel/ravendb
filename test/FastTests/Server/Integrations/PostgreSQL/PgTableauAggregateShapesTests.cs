using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Pipelines;
using System.Linq;
using System.Threading.Tasks;
using Raven.Client.Documents;
using Raven.Server.Documents;
using Raven.Server.Integrations.PostgreSQL;
using Raven.Server.Integrations.PostgreSQL.Exceptions;
using Raven.Server.Integrations.PostgreSQL.Messages;
using Raven.Server.Integrations.PostgreSQL.Translation;
using Tests.Infrastructure;
using Xunit;
using static Tests.Infrastructure.PostgreSqlHelper;

namespace FastTests.Server.Integrations.PostgreSQL
{
    public sealed class PgTableauAggregateShapesTests(ITestOutputHelper output) : RavenTestBase(output)
    {
        [RavenFact(RavenTestCategory.PostgreSql)]
        public void Group_by_and_order_by_ordinals_translate_like_the_named_keys()
        {
            Assert.Equal(
                Translate("SELECT \"Orders\".\"Company\" AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY \"Orders\".\"Company\" ORDER BY SUM(\"Orders\".\"Freight\") DESC"),
                Translate("SELECT \"Orders\".\"Company\" AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 ORDER BY 2 DESC"));
        }

        [RavenFact(RavenTestCategory.PostgreSql)]
        public void Order_by_ordinal_without_group_by_sorts_by_that_column()
        {
            Assert.Equal(
                Translate("SELECT \"Company\", \"Freight\" FROM \"public\".\"Orders\" ORDER BY \"Freight\" DESC"),
                Translate("SELECT \"Company\", \"Freight\" FROM \"public\".\"Orders\" ORDER BY 2 DESC"));
        }

        [RavenTheory(RavenTestCategory.PostgreSql)]
        [InlineData("SELECT \"Company\", SUM(\"Freight\") FROM \"public\".\"Orders\" GROUP BY 3")]
        [InlineData("SELECT \"Company\", SUM(\"Freight\") FROM \"public\".\"Orders\" GROUP BY 0")]
        [InlineData("SELECT \"Company\" FROM \"public\".\"Orders\" ORDER BY 2")]
        [InlineData("SELECT * FROM \"public\".\"Orders\" ORDER BY 1")]
        public void Ordinal_outside_the_select_list_does_not_translate(string sql)
        {
            Assert.False(PgSqlToRqlTranslator.TryParse(sql, Array.Empty<int>(), out _));
        }

        [RavenFact(RavenTestCategory.PostgreSql)]
        public void Translating_an_ordinal_query_leaves_the_cached_ast_untouched()
        {
            const string sql = "SELECT \"Company\", SUM(\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" GROUP BY 1 ORDER BY 2 DESC";

            Assert.Equal(Translate(sql), Translate(sql));
        }

        [RavenFact(RavenTestCategory.PostgreSql, LicenseRequired = true)]
        public async Task Sum_by_ordinal_group_returns_one_row_per_key()
        {
            using var store = GetDocumentStore();
            await Seed(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (columns, rows) = await RunWarm(
                "SELECT \"Orders\".\"Company\" AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"sum:Freight:ok\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1",
                store, database);

            Assert.Equal(new[] { "Company", "sum:Freight:ok" }, columns);
            Assert.Equal(new Dictionary<string, double> { ["Alpha"] = 30, ["Beta"] = 9, ["Gamma"] = 4 }, ToSums(rows));
        }

        [RavenFact(RavenTestCategory.PostgreSql, LicenseRequired = true)]
        public async Task Top_n_by_ordinal_orders_by_the_aggregate()
        {
            using var store = GetDocumentStore();
            await Seed(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (_, rows) = await RunWarm(
                "SELECT \"Orders\".\"Company\" AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"sum:Freight:ok\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 ORDER BY 2 DESC LIMIT 2",
                store, database);

            Assert.Equal(new[] { "Alpha", "Beta" }, rows.Select(r => r[0]));
        }

        [RavenFact(RavenTestCategory.PostgreSql, LicenseRequired = true)]
        public async Task Two_group_keys_aggregate_per_key_pair()
        {
            using var store = GetDocumentStore();
            await Seed(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (columns, rows) = await RunWarm(
                "SELECT \"Orders\".\"Company\" AS \"Company\", \"Orders\".\"ShipVia\" AS \"ShipVia\", SUM(\"Orders\".\"Freight\") AS \"sum:Freight:ok\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1, 2",
                store, database);

            Assert.Equal(new[] { "Company", "ShipVia", "sum:Freight:ok" }, columns);
            var sums = rows.ToDictionary(r => (r[0], r[1]), r => double.Parse(r[2], CultureInfo.InvariantCulture));
            Assert.Equal(new Dictionary<(string, string), double>
            {
                [("Alpha", "Air")] = 12,
                [("Alpha", "Sea")] = 18,
                [("Beta", "Air")] = 9,
                [("Gamma", "Sea")] = 4
            }, sums);
        }

        [RavenFact(RavenTestCategory.PostgreSql, LicenseRequired = true)]
        public async Task Two_group_keys_order_by_the_second_key()
        {
            using var store = GetDocumentStore();
            await Seed(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (_, rows) = await RunWarm(
                "SELECT \"Orders\".\"Company\" AS \"Company\", \"Orders\".\"ShipVia\" AS \"ShipVia\", COUNT(1) AS \"cnt\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1, 2 ORDER BY 2 DESC, 1 ASC",
                store, database);

            Assert.Equal(new[] { ("Alpha", "Sea"), ("Gamma", "Sea"), ("Alpha", "Air"), ("Beta", "Air") }, rows.Select(r => (r[0], r[1])));
        }

        [RavenFact(RavenTestCategory.PostgreSql, LicenseRequired = true)]
        public async Task Sum_of_one_counts_the_rows_of_each_group()
        {
            using var store = GetDocumentStore();
            await Seed(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (_, rows) = await RunWarm(
                "SELECT \"Orders\".\"Company\" AS \"Company\", SUM(1) AS \"cnt:Orders_4F2A:ok\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 ORDER BY 2 DESC",
                store, database);

            Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, rows.Select(r => r[0]));
            Assert.Equal(new[] { "10", "9", "2" }, rows.Select(r => r[1]));
        }

        [RavenTheory(RavenTestCategory.PostgreSql)]
        [InlineData("SELECT SUM(1) AS \"cnt:Orders_4F2A:ok\" FROM \"public\".\"Orders\" \"Orders\" HAVING (COUNT(1) > 0)")]
        [InlineData("SELECT COUNT(1) AS \"cnt:Orders_4F2A:ok\" FROM \"public\".\"Orders\" \"Orders\" HAVING (COUNT(1) > 0)")]
        [InlineData("SELECT SUM(1) AS \"cnt:Orders_4F2A:ok\" FROM \"public\".\"Orders\" \"Orders\"")]
        public async Task Row_count_probe_returns_the_collection_count(string sql)
        {
            using var store = GetDocumentStore();
            await Seed(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (columns, rows) = await Run(sql, database);

            Assert.Equal(new[] { "cnt:Orders_4F2A:ok" }, columns);
            Assert.Equal(new[] { "21" }, Assert.Single(rows));
        }

        [RavenFact(RavenTestCategory.PostgreSql)]
        public async Task Row_count_probe_with_having_returns_no_row_when_nothing_matches()
        {
            using var store = GetDocumentStore();
            await Seed(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (_, rows) = await Run(
                "SELECT SUM(1) AS \"cnt\" FROM \"public\".\"Orders\" \"Orders\" WHERE \"Orders\".\"Company\" = 'Nobody' HAVING (COUNT(1) > 0)", database);

            Assert.Empty(rows);
        }

        [RavenFact(RavenTestCategory.PostgreSql)]
        public async Task Row_count_without_having_over_no_rows_is_zero_for_count_and_null_for_sum()
        {
            using var store = GetDocumentStore();
            await Seed(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (_, rows) = await Run(
                "SELECT COUNT(1) AS \"c\", SUM(1) AS \"s\" FROM \"public\".\"Orders\" \"Orders\" WHERE \"Orders\".\"Company\" = 'Nobody'", database);

            var row = Assert.Single(rows);
            Assert.Equal("0", row[0]);
            Assert.Null(row[1]);
        }

        [RavenTheory(RavenTestCategory.PostgreSql)]
        [InlineData("SELECT SUM(2) FROM \"public\".\"Orders\"")]
        [InlineData("SELECT COUNT(1) FROM \"public\".\"Orders\" HAVING (COUNT(1) > 5)")]
        [InlineData("SELECT COUNT(1) FROM \"public\".\"Orders\" HAVING (SUM(\"Freight\") > 0)")]
        [InlineData("SELECT COUNT(NULL) FROM \"public\".\"Orders\"")]
        public async Task Other_scalar_aggregates_still_return_a_pg_error(string sql)
        {
            using var store = GetDocumentStore();
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            Assert.Throws<PgErrorException>(() => PgQuery.CreateInstance(sql, Array.Empty<int>(), database, session: null));
        }

        private static string Translate(string sql)
        {
            Assert.True(PgSqlToRqlTranslator.TryParse(sql, Array.Empty<int>(), out var rql));
            return rql;
        }

        private static Dictionary<string, double> ToSums(List<string[]> rows) =>
            rows.ToDictionary(r => r[0], r => double.Parse(r[1], CultureInfo.InvariantCulture));

        private static async Task Seed(IDocumentStore store)
        {
            using var session = store.OpenAsyncSession();
            for (int i = 0; i < 4; i++)
                await session.StoreAsync(new Order { Company = "Alpha", ShipVia = "Air", Freight = 3 });
            for (int i = 0; i < 6; i++)
                await session.StoreAsync(new Order { Company = "Alpha", ShipVia = "Sea", Freight = 3 });
            for (int i = 0; i < 9; i++)
                await session.StoreAsync(new Order { Company = "Beta", ShipVia = "Air", Freight = 1 });
            for (int i = 0; i < 2; i++)
                await session.StoreAsync(new Order { Company = "Gamma", ShipVia = "Sea", Freight = 2 });
            await session.SaveChangesAsync();
        }

        private async Task<(List<string> Columns, List<string[]> Rows)> RunWarm(string sql, IDocumentStore store, DocumentDatabase database)
        {
            await Run(sql, database);
            Indexes.WaitForIndexing(store);
            return await Run(sql, database);
        }

        private static async Task<(List<string> Columns, List<string[]> Rows)> Run(string sql, DocumentDatabase database)
        {
            using var query = PgQuery.CreateInstance(sql, Array.Empty<int>(), database, session: null);
            var columns = await query.Init();

            return (columns.Select(c => c.Name).ToList(), await Drain(query));
        }

        private static async Task<List<string[]>> Drain(PgQuery query)
        {
            var token = TestContext.Current.CancellationToken;
            var pipe = new Pipe();
            var builder = new MessageBuilder();

            var readTask = ReadAllAsync(pipe.Reader, token);
            await query.Execute(builder, pipe.Writer, token);
            await pipe.Writer.CompleteAsync();

            return ParseDataRows(await readTask);
        }

        private sealed class Order
        {
            public string Company { get; set; }
            public string ShipVia { get; set; }
            public double Freight { get; set; }
        }
    }
}
