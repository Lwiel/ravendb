using System;
using System.Collections.Generic;
using System.Linq;
using FastTests;
using Raven.Client.Documents.Operations.CdcSink;
using Tests.Infrastructure;
using Xunit;

namespace SlowTests.Server.Documents.CdcSink
{
    // Regression tests for RavenDB-26926: an embedded table's initial load is paginated by a keyset key
    // that must be unique across the whole source table. PrimaryKeyColumns is only unique within a single
    // parent's array, so pagination on it silently drops rows. InitialLoadKeyColumns glues the join FKs in.
    public class CdcSinkInitialLoadKeyTests : RavenTestBase
    {
        public CdcSinkInitialLoadKeyTests(ITestOutputHelper output) : base(output)
        {
        }

        private static List<string> InitialLoadKey(CdcSinkConfiguration config, string tableName)
        {
            return config.CollectAllTablesFlat("public")
                .Single(t => string.Equals(t.TableName, tableName, StringComparison.OrdinalIgnoreCase))
                .InitialLoadKeyColumns;
        }

        [RavenFact(RavenTestCategory.Sinks)]
        public void RootTable_InitialLoadKey_IsItsPrimaryKey()
        {
            var config = new CdcSinkConfiguration
            {
                Name = "t",
                Tables = new List<CdcSinkTableConfig>
                {
                    new CdcSinkTableConfig
                    {
                        CollectionName = "Orders",
                        SourceTableSchema = "public",
                        SourceTableName = "orders",
                        Columns = new List<CdcColumnMapping> { new CdcColumnMapping { Column = "order_id", Name = "OrderId" } },
                        PrimaryKeyColumns = new List<string> { "order_id" }
                    }
                }
            };

            Assert.Equal(new[] { "order_id" }, InitialLoadKey(config, "orders"));
        }

        [RavenFact(RavenTestCategory.Sinks)]
        public void EmbeddedTable_InitialLoadKey_GluesJoinColumnAndPrimaryKey()
        {
            // The reported scenario: order_details (real PK = (order_id, product_id)) embedded as Lines with
            // PrimaryKeyColumns = [product_id] (unique within one order, but repeats across the table).
            var config = new CdcSinkConfiguration
            {
                Name = "t",
                Tables = new List<CdcSinkTableConfig>
                {
                    new CdcSinkTableConfig
                    {
                        CollectionName = "Orders",
                        SourceTableSchema = "public",
                        SourceTableName = "orders",
                        Columns = new List<CdcColumnMapping> { new CdcColumnMapping { Column = "order_id", Name = "OrderId" } },
                        PrimaryKeyColumns = new List<string> { "order_id" },
                        EmbeddedTables = new List<CdcSinkEmbeddedTableConfig>
                        {
                            new CdcSinkEmbeddedTableConfig
                            {
                                SourceTableSchema = "public",
                                SourceTableName = "order_details",
                                PropertyName = "Lines",
                                Columns = new List<CdcColumnMapping> { new CdcColumnMapping { Column = "product_id", Name = "ProductId" } },
                                PrimaryKeyColumns = new List<string> { "product_id" },
                                JoinColumns = new List<string> { "order_id" },
                                Type = CdcSinkRelationType.Array
                            }
                        }
                    }
                }
            };

            // Glued join FK + PK = the real composite PK; unique across the whole table so pagination is safe.
            Assert.Equal(new[] { "order_id", "product_id" }, InitialLoadKey(config, "order_details"));
        }

        [RavenFact(RavenTestCategory.Sinks)]
        public void EmbeddedTable_InitialLoadKey_DoesNotDuplicateSharedColumns()
        {
            // A user who already set PrimaryKeyColumns to the full composite key must not get a duplicate column.
            var config = new CdcSinkConfiguration
            {
                Name = "t",
                Tables = new List<CdcSinkTableConfig>
                {
                    new CdcSinkTableConfig
                    {
                        CollectionName = "Orders",
                        SourceTableSchema = "public",
                        SourceTableName = "orders",
                        Columns = new List<CdcColumnMapping> { new CdcColumnMapping { Column = "order_id", Name = "OrderId" } },
                        PrimaryKeyColumns = new List<string> { "order_id" },
                        EmbeddedTables = new List<CdcSinkEmbeddedTableConfig>
                        {
                            new CdcSinkEmbeddedTableConfig
                            {
                                SourceTableSchema = "public",
                                SourceTableName = "order_details",
                                PropertyName = "Lines",
                                Columns = new List<CdcColumnMapping> { new CdcColumnMapping { Column = "product_id", Name = "ProductId" } },
                                PrimaryKeyColumns = new List<string> { "order_id", "product_id" },
                                JoinColumns = new List<string> { "order_id" },
                                Type = CdcSinkRelationType.Array
                            }
                        }
                    }
                }
            };

            Assert.Equal(new[] { "order_id", "product_id" }, InitialLoadKey(config, "order_details"));
        }

        [RavenFact(RavenTestCategory.Sinks)]
        public void TwoLevelNesting_InitialLoadKey_IncludesRootAndImmediateParentFks()
        {
            // company -> department -> employee. The employee row carries the root FK (company_id, denormalized)
            // and its immediate-parent FK (dept_id); glued with emp_id it uniquely identifies the source row.
            var config = new CdcSinkConfiguration
            {
                Name = "t",
                Tables = new List<CdcSinkTableConfig>
                {
                    new CdcSinkTableConfig
                    {
                        CollectionName = "Companies",
                        SourceTableSchema = "public",
                        SourceTableName = "companies",
                        Columns = new List<CdcColumnMapping> { new CdcColumnMapping { Column = "company_id", Name = "CompanyId" } },
                        PrimaryKeyColumns = new List<string> { "company_id" },
                        EmbeddedTables = new List<CdcSinkEmbeddedTableConfig>
                        {
                            new CdcSinkEmbeddedTableConfig
                            {
                                SourceTableSchema = "public",
                                SourceTableName = "departments",
                                PropertyName = "Departments",
                                Columns = new List<CdcColumnMapping> { new CdcColumnMapping { Column = "dept_id", Name = "DeptId" } },
                                PrimaryKeyColumns = new List<string> { "dept_id" },
                                JoinColumns = new List<string> { "company_id" },
                                Type = CdcSinkRelationType.Array,
                                EmbeddedTables = new List<CdcSinkEmbeddedTableConfig>
                                {
                                    new CdcSinkEmbeddedTableConfig
                                    {
                                        SourceTableSchema = "public",
                                        SourceTableName = "employees",
                                        PropertyName = "Employees",
                                        Columns = new List<CdcColumnMapping> { new CdcColumnMapping { Column = "emp_id", Name = "EmpId" } },
                                        PrimaryKeyColumns = new List<string> { "emp_id" },
                                        JoinColumns = new List<string> { "dept_id" },
                                        Type = CdcSinkRelationType.Array
                                    }
                                }
                            }
                        }
                    }
                }
            };

            // rootJoin (company_id) + immediate-parent join (dept_id) + PK (emp_id).
            Assert.Equal(new[] { "company_id", "dept_id", "emp_id" }, InitialLoadKey(config, "employees"));
        }
    }
}
