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

        [RavenTheory(RavenTestCategory.PostgreSql)]
        [InlineData(
            "SELECT CAST(\"Orders\".\"Company\" AS TEXT) AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 ORDER BY 2 DESC LIMIT 10",
            "SELECT \"Orders\".\"Company\" AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 ORDER BY 2 DESC LIMIT 10")]
        [InlineData(
            "SELECT CAST(\"Orders\".\"Company\" AS TEXT) AS \"Company\", CAST(\"Orders\".\"ShipVia\" AS TEXT) AS \"ShipVia\", SUM(\"Orders\".\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1, 2 ORDER BY 1 ASC",
            "SELECT \"Orders\".\"Company\" AS \"Company\", \"Orders\".\"ShipVia\" AS \"ShipVia\", SUM(\"Orders\".\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1, 2 ORDER BY 1 ASC")]
        [InlineData(
            "SELECT CAST(\"Orders\".\"Company\" AS TEXT) AS \"Company\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 ORDER BY 1 ASC",
            "SELECT \"Orders\".\"Company\" AS \"Company\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 ORDER BY 1 ASC")]
        [InlineData(
            "SELECT CAST(\"Orders\".\"Company\" AS TEXT) AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" WHERE (CAST(\"Orders\".\"Company\" AS TEXT) IN ('Alpha', 'Beta')) GROUP BY 1",
            "SELECT \"Orders\".\"Company\" AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" WHERE (\"Orders\".\"Company\" IN ('Alpha', 'Beta')) GROUP BY 1")]
        [InlineData(
            "SELECT \"Orders\".\"Company\"::varchar AS \"Company\", COUNT(1) AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY \"Orders\".\"Company\"::varchar",
            "SELECT \"Orders\".\"Company\" AS \"Company\", COUNT(1) AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY \"Orders\".\"Company\"")]
        [InlineData(
            "SELECT CAST(\"Company\" AS TEXT) AS \"Company\", \"Freight\" FROM \"public\".\"Orders\" WHERE (CAST(\"Company\" AS TEXT) = 'Alpha') ORDER BY 1",
            "SELECT \"Company\" AS \"Company\", \"Freight\" FROM \"public\".\"Orders\" WHERE (\"Company\" = 'Alpha') ORDER BY 1")]
        public void Text_cast_of_a_column_translates_like_the_bare_column(string castSql, string bareSql)
        {
            Assert.Equal(Translate(bareSql), Translate(castSql));
        }

        [RavenTheory(RavenTestCategory.PostgreSql)]
        [InlineData("SELECT CAST(\"Orders\".\"OrderedAt\" AS DATE) AS \"OrderedAt\", SUM(\"Orders\".\"Freight\") FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1")]
        [InlineData("SELECT CAST(\"Orders\".\"Company\" AS VARCHAR(3)) AS \"Company\", SUM(\"Orders\".\"Freight\") FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1")]
        [InlineData("SELECT CAST(\"Orders\".\"Freight\" AS INTEGER) AS \"Freight\", COUNT(1) FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1")]
        public void Group_key_cast_that_can_merge_values_does_not_translate(string sql)
        {
            Assert.False(PgSqlToRqlTranslator.TryParse(sql, Array.Empty<int>(), out _));
        }

        [RavenFact(RavenTestCategory.PostgreSql)]
        public void Text_cast_filter_on_a_non_grouped_field_does_not_translate()
        {
            Assert.False(PgSqlToRqlTranslator.TryParse(
                "SELECT CAST(\"Orders\".\"Company\" AS TEXT) AS \"Company\", SUM(\"Orders\".\"Freight\") FROM \"public\".\"Orders\" \"Orders\" WHERE (CAST(\"Orders\".\"ShipVia\" AS TEXT) = 'Air') GROUP BY 1",
                Array.Empty<int>(), out _));
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
        public async Task Text_cast_dimension_filtered_on_the_group_key_returns_the_filtered_sums()
        {
            using var store = GetDocumentStore();
            await Seed(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (columns, rows) = await RunWarm(
                "SELECT CAST(\"Orders\".\"Company\" AS TEXT) AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"sum:Freight:ok\" FROM \"public\".\"Orders\" \"Orders\" WHERE (CAST(\"Orders\".\"Company\" AS TEXT) IN ('Alpha', 'Gamma')) GROUP BY 1 ORDER BY 2 DESC",
                store, database);

            Assert.Equal(new[] { "Company", "sum:Freight:ok" }, columns);
            Assert.Equal(new[] { "Alpha", "Gamma" }, rows.Select(r => r[0]));
            Assert.Equal(new Dictionary<string, double> { ["Alpha"] = 30, ["Gamma"] = 4 }, ToSums(rows));
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

        [RavenTheory(RavenTestCategory.PostgreSql)]
        [InlineData(
            "SELECT \"Orders\".\"Company\" AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 HAVING (SUM(\"Orders\".\"Freight\") > 10)",
            "from 'Orders' group by Company where (Freight > 10.0) select Company, sum(Freight) as a0")]
        [InlineData(
            "SELECT \"Orders\".\"Company\" AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 HAVING ((SUM(\"Orders\".\"Freight\") >= 5) AND (SUM(\"Orders\".\"Freight\") <= 30.5))",
            "from 'Orders' group by Company where (Freight >= 5.0 and Freight <= 30.5) select Company, sum(Freight) as a0")]
        [InlineData(
            "SELECT \"Orders\".\"Company\" AS \"Company\", COUNT(1) AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 HAVING (COUNT(1) > 2)",
            "from 'Orders' group by Company where (Count > 2.0) select Company, count() as a0")]
        [InlineData(
            "SELECT \"Orders\".\"Company\" AS \"Company\", SUM(1) AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 HAVING (COUNT(*) BETWEEN 2 AND 9)",
            "from 'Orders' group by Company where (Count between 2.0 and 9.0) select Company, count() as a0")]
        [InlineData(
            "SELECT \"Orders\".\"Company\" AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" WHERE (\"Orders\".\"Company\" = 'Gamma' OR \"Orders\".\"Company\" = 'Alpha') GROUP BY 1 HAVING (SUM(\"Orders\".\"Freight\") > 5)",
            "from 'Orders' group by Company where (Company = 'Gamma' or Company = 'Alpha') and (Freight > 5.0) select Company, sum(Freight) as a0")]
        public void Having_on_a_selected_aggregate_filters_the_reduced_groups(string sql, string expectedRql)
        {
            Assert.Equal(expectedRql, Translate(sql));
        }

        [RavenTheory(RavenTestCategory.PostgreSql)]
        [InlineData("SELECT \"Company\", SUM(\"Freight\") FROM \"public\".\"Orders\" GROUP BY 1 HAVING (COUNT(1) > 5)")]
        [InlineData("SELECT \"Company\", SUM(\"Freight\") FROM \"public\".\"Orders\" GROUP BY 1 HAVING (AVG(\"Freight\") > 5)")]
        [InlineData("SELECT \"Company\", SUM(\"Freight\") FROM \"public\".\"Orders\" GROUP BY 1 HAVING (SUM(\"Freight\") IS NULL)")]
        [InlineData("SELECT \"Company\", SUM(\"Freight\") FROM \"public\".\"Orders\" GROUP BY 1 HAVING (\"ShipVia\" = 'Air')")]
        [InlineData("SELECT \"Company\", COUNT(1) FROM \"public\".\"Orders\" GROUP BY 1 HAVING (COUNT(\"Freight\") > 5)")]
        [InlineData("SELECT \"Freight\", SUM(\"Freight\") FROM \"public\".\"Orders\" GROUP BY 1 HAVING (SUM(\"Freight\") > 5)")]
        [InlineData("SELECT \"Company\" FROM \"public\".\"Orders\" GROUP BY 1 HAVING (COUNT(1) > 5)")]
        public void Having_outside_the_selected_count_and_sum_does_not_translate(string sql)
        {
            Assert.False(PgSqlToRqlTranslator.TryParse(sql, Array.Empty<int>(), out _));
        }

        [RavenFact(RavenTestCategory.PostgreSql)]
        public async Task Having_on_an_unselected_aggregate_explains_the_selected_aggregate_rule()
        {
            using var store = GetDocumentStore();
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var error = Assert.Throws<PgErrorException>(() => PgQuery.CreateInstance(
                "SELECT \"Company\", SUM(\"Freight\") FROM \"public\".\"Orders\" GROUP BY 1 HAVING (COUNT(1) > 5)", Array.Empty<int>(), database, session: null));
            Assert.Contains("SELECT list", error.Message);
        }

        [RavenFact(RavenTestCategory.PostgreSql, LicenseRequired = true)]
        public async Task Having_range_on_a_sum_keeps_the_groups_inside_the_range()
        {
            using var store = GetDocumentStore();
            await Seed(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (_, rows) = await RunWarm(
                "SELECT \"Orders\".\"Company\" AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"sum:Freight:ok\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 HAVING ((SUM(\"Orders\".\"Freight\") >= 5) AND (SUM(\"Orders\".\"Freight\") <= 30))",
                store, database);

            Assert.Equal(new Dictionary<string, double> { ["Alpha"] = 30, ["Beta"] = 9 }, ToSums(rows));
        }

        [RavenFact(RavenTestCategory.PostgreSql, LicenseRequired = true)]
        public async Task Having_on_a_count_keeps_the_groups_with_more_rows()
        {
            using var store = GetDocumentStore();
            await Seed(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (_, rows) = await RunWarm(
                "SELECT \"Orders\".\"Company\" AS \"Company\", COUNT(1) AS \"cnt\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 HAVING (COUNT(1) > 2) ORDER BY 2 DESC",
                store, database);

            Assert.Equal(new[] { ("Alpha", "10"), ("Beta", "9") }, rows.Select(r => (r[0], r[1])));
        }

        [RavenFact(RavenTestCategory.PostgreSql, LicenseRequired = true)]
        public async Task Having_with_an_integer_bound_compares_a_fractional_sum_exactly()
        {
            using var store = GetDocumentStore();
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(new Order { Company = "Delta", Freight = 5.25 });
                await session.StoreAsync(new Order { Company = "Delta", Freight = 5.25 });
                await session.StoreAsync(new Order { Company = "Echo", Freight = 10 });
                await session.SaveChangesAsync();
            }
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (_, rows) = await RunWarm(
                "SELECT \"Orders\".\"Company\" AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 HAVING (SUM(\"Orders\".\"Freight\") > 10)",
                store, database);

            Assert.Equal(new Dictionary<string, double> { ["Delta"] = 10.5 }, ToSums(rows));
        }

        [RavenFact(RavenTestCategory.PostgreSql, LicenseRequired = true)]
        public async Task Having_combined_with_an_or_filter_on_the_group_key_applies_both()
        {
            using var store = GetDocumentStore();
            await Seed(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (_, rows) = await RunWarm(
                "SELECT \"Orders\".\"Company\" AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"a0\" FROM \"public\".\"Orders\" \"Orders\" WHERE (\"Orders\".\"Company\" = 'Gamma' OR \"Orders\".\"Company\" = 'Alpha') GROUP BY 1 HAVING (SUM(\"Orders\".\"Freight\") > 5)",
                store, database);

            Assert.Equal(new Dictionary<string, double> { ["Alpha"] = 30 }, ToSums(rows));
        }

        [RavenTheory(RavenTestCategory.PostgreSql, LicenseRequired = true)]
        [RavenData("\"Freight\" > 10", "C", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Freight\" >= 10", "B,C", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Freight\" <= 10", "A,B,D", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Freight\" < 10", "A,D", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Freight\" > 9", "A,B,C", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Freight\" = 10", "B", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Freight\" <> 10", "A,C,D", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Freight\" IN (10)", "B", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Freight\" BETWEEN 9 AND 10", "A,B", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Freight\" >= 0", "A,B,C", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Freight\" < 0", "D", SearchEngineMode = RavenSearchEngineMode.All)]
        public async Task Integer_bound_on_a_fractional_field_compares_the_exact_value(Options options, string condition, string expected)
        {
            using var store = GetDocumentStore(options);
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(new Order { Company = "A", Freight = 9.5 });
                await session.StoreAsync(new Order { Company = "B", Freight = 10 });
                await session.StoreAsync(new Order { Company = "C", Freight = 10.5 });
                await session.StoreAsync(new Order { Company = "D", Freight = -0.5 });
                await session.SaveChangesAsync();
            }
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (_, rows) = await RunWarm($"SELECT \"Company\", \"Freight\" FROM \"public\".\"Orders\" WHERE {condition}", store, database);

            Assert.Equal(expected, string.Join(",", rows.Select(r => r[0]).Order()));
        }

        [RavenTheory(RavenTestCategory.PostgreSql, LicenseRequired = true)]
        [RavenData("\"Units\" = 10", "B", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Units\" <> 10", "A,C", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Units\" > 9", "B,C", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Units\" <= 10", "A,B", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Units\" BETWEEN 10 AND 11", "B,C", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("\"Units\" IN (10)", "B", SearchEngineMode = RavenSearchEngineMode.All)]
        public async Task Integer_bound_on_an_integer_field_compares_the_exact_value(Options options, string condition, string expected)
        {
            using var store = GetDocumentStore(options);
            await SeedItems(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (_, rows) = await RunWarm($"SELECT \"Company\", \"Units\" FROM \"public\".\"Items\" WHERE {condition}", store, database);

            Assert.Equal(expected, string.Join(",", rows.Select(r => r[0]).Order()));
        }

        [RavenTheory(RavenTestCategory.PostgreSql, LicenseRequired = true)]
        [RavenData(SearchEngineMode = RavenSearchEngineMode.All)]
        public async Task Integer_filter_on_an_integer_group_key_keeps_that_group(Options options)
        {
            using var store = GetDocumentStore(options);
            await SeedItems(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (_, rows) = await RunWarm(
                "SELECT \"Items\".\"Units\" AS \"Units\", COUNT(1) AS \"cnt\" FROM \"public\".\"Items\" \"Items\" WHERE (\"Items\".\"Units\" = 10) GROUP BY 1",
                store, database);

            Assert.Equal(new[] { "10", "1" }, Assert.Single(rows));
        }

        [RavenTheory(RavenTestCategory.PostgreSql, LicenseRequired = true)]
        [RavenData("SELECT \"Company\" FROM \"public\".\"Orders\" ORDER BY \"Company\" ASC NULLS FIRST", "NULL,NULL,a,b,c", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("SELECT \"Company\" FROM \"public\".\"Orders\" ORDER BY \"Company\" DESC NULLS LAST", "c,b,a,NULL,NULL", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("SELECT \"Company\", COUNT(1) AS \"c\" FROM \"public\".\"Orders\" GROUP BY 1 ORDER BY 1 ASC NULLS FIRST", "NULL,a,b,c", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("SELECT \"Company\", COUNT(1) AS \"c\" FROM \"public\".\"Orders\" GROUP BY 1 ORDER BY 1 DESC NULLS LAST", "c,b,a,NULL", SearchEngineMode = RavenSearchEngineMode.All)]
        [RavenData("SELECT CAST(\"Orders\".\"Company\" AS TEXT) AS \"Company\", SUM(\"Orders\".\"Freight\") AS \"s\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 1 ORDER BY 2 DESC NULLS LAST, 1 ASC NULLS FIRST LIMIT 2", "b,NULL", SearchEngineMode = RavenSearchEngineMode.All)]
        public async Task Null_placement_matching_ravendb_sorts_nulls_where_asked(Options options, string sql, string expected)
        {
            using var store = GetDocumentStore(options);
            await SeedWithNulls(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var (_, rows) = await RunWarm(sql, store, database);

            Assert.Equal(expected, string.Join(",", rows.Select(r => r[0] ?? "NULL")));
        }

        [RavenFact(RavenTestCategory.PostgreSql)]
        public async Task Explicit_null_placement_on_a_numeric_column_returns_a_pg_error()
        {
            using var store = GetDocumentStore();
            await SeedWithNulls(store);
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var error = Assert.Throws<PgErrorException>(() => PgQuery.CreateInstance(
                "SELECT \"Freight\" FROM \"public\".\"Orders\" ORDER BY \"Freight\" ASC NULLS FIRST", Array.Empty<int>(), database, session: null));
            Assert.Contains("NULLS", error.Message);
        }

        [RavenFact(RavenTestCategory.PostgreSql)]
        public async Task Year_dimension_returns_a_pg_error_naming_the_computed_group_key()
        {
            using var store = GetDocumentStore();
            var database = await Databases.GetDocumentDatabaseInstanceFor(store);

            var error = Assert.Throws<PgErrorException>(() => PgQuery.CreateInstance(
                "SELECT SUM(\"Orders\".\"Freight\") AS \"sum:Freight:ok\", CAST(TRUNC(EXTRACT(YEAR FROM \"Orders\".\"OrderedAt\")) AS INTEGER) AS \"yr:OrderedAt:ok\" FROM \"public\".\"Orders\" \"Orders\" GROUP BY 2",
                Array.Empty<int>(), database, session: null));

            Assert.Equal(PgErrorCodes.FeatureNotSupported, error.ErrorCode);
            Assert.Contains("GROUP BY on a computed expression over \"OrderedAt\"", error.Message);
        }

        private static async Task SeedWithNulls(IDocumentStore store)
        {
            using (var session = store.OpenAsyncSession())
            {
                var docs = new object[]
                {
                    new NullableOrder { Company = "b", Freight = 2 },
                    new NullableOrder { Company = "a", Freight = -1.5 },
                    new NullableOrder { Company = null, Freight = null },
                    new NullableOrder { Company = "c", Freight = 0 },
                    new EmptyOrder()
                };
                for (int i = 0; i < docs.Length; i++)
                {
                    await session.StoreAsync(docs[i], "orders/" + (i + 1));
                    session.Advanced.GetMetadataFor(docs[i])["@collection"] = "Orders";
                }
                await session.SaveChangesAsync();
            }
        }

        private sealed class NullableOrder
        {
            public string Company { get; set; }
            public double? Freight { get; set; }
        }

        private sealed class EmptyOrder
        {
        }

        private static async Task SeedItems(IDocumentStore store)
        {
            using var session = store.OpenAsyncSession();
            await session.StoreAsync(new Item { Company = "A", Units = 9 });
            await session.StoreAsync(new Item { Company = "B", Units = 10 });
            await session.StoreAsync(new Item { Company = "C", Units = 11 });
            await session.SaveChangesAsync();
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

        private sealed class Item
        {
            public string Company { get; set; }
            public long Units { get; set; }
        }
    }
}
