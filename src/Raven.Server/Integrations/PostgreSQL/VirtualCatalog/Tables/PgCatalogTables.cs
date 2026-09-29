using System.Collections.Generic;
using Raven.Server.Integrations.PostgreSQL.Messages;
using Raven.Server.Integrations.PostgreSQL.Types;
using Raven.Server.ServerWide.Context;

namespace Raven.Server.Integrations.PostgreSQL.VirtualCatalog.Tables
{
    // CSV-backed pg_catalog tables — let Npgsql startup type-loading queries run through
    // PgVirtualInterpreter.
    internal abstract class CsvBackedCatalogTable : PgVirtualTable
    {
        private readonly object _gate = new();
        // volatile: the lock-free fast path reads _rows outside the lock, so the reference must not
        // publish before the list contents are visible (matters on weak memory models like ARM64).
        private volatile List<object[]> _rows;

        protected abstract string CsvFileName { get; }

        public override bool IsAlwaysEmpty => false;

        public override IEnumerable<object[]> EnumerateRows(VirtualQueryContext ctx)
        {
            if (_rows != null)
                return _rows;

            lock (_gate)
            {
                _rows ??= CatalogCsvLoader.Load(CsvFileName, Columns);
            }
            return _rows;
        }
    }

    internal sealed class PgCatalogPgTypeTable : CsvBackedCatalogTable
    {
        public override string SchemaName => "pg_catalog";
        public override string TableName => "pg_type";
        protected override string CsvFileName => "pg_type.csv";

        public override IReadOnlyList<PgVirtualColumn> Columns { get; } = new PgVirtualColumn[]
        {
            new("oid",         PgOid.Default,  PgFormat.Text),
            new("typname",     PgName.Default, PgFormat.Text),
            new("typnamespace",PgOid.Default,  PgFormat.Text),
            new("typtype",     PgChar.Default, PgFormat.Text),
            new("typrelid",    PgOid.Default,  PgFormat.Text),
            new("typnotnull",  PgBool.Default, PgFormat.Text),
            new("typbasetype", PgOid.Default,  PgFormat.Text),
            new("typelem",     PgOid.Default,  PgFormat.Text),
            new("typreceive",  PgOid.Default,  PgFormat.Text),
            new("typcategory", PgChar.Default, PgFormat.Text),
            new("typarray",    PgOid.Default,  PgFormat.Text),
            new("typtypmod",   PgInt4.Default, PgFormat.Text),
            new("typdefault",  PgText.Default, PgFormat.Text),
        };
    }

    internal sealed class PgCatalogPgProcTable : CsvBackedCatalogTable
    {
        public override string SchemaName => "pg_catalog";
        public override string TableName => "pg_proc";
        protected override string CsvFileName => "pg_proc.csv";

        public override IReadOnlyList<PgVirtualColumn> Columns { get; } = new PgVirtualColumn[]
        {
            new("oid",     PgOid.Default,  PgFormat.Text),
            new("proname", PgName.Default, PgFormat.Text),
        };
    }

    internal sealed class PgCatalogPgRangeTable : CsvBackedCatalogTable
    {
        public override string SchemaName => "pg_catalog";
        public override string TableName => "pg_range";
        protected override string CsvFileName => "pg_range.csv";

        public override IReadOnlyList<PgVirtualColumn> Columns { get; } = new PgVirtualColumn[]
        {
            new("rngtypid",      PgOid.Default, PgFormat.Text),
            new("rngsubtype",    PgOid.Default, PgFormat.Text),
            new("rngmultitypid", PgOid.Default, PgFormat.Text),
        };
    }

    internal sealed class PgCatalogPgNamespaceTable : CsvBackedCatalogTable
    {
        public override string SchemaName => "pg_catalog";
        public override string TableName => "pg_namespace";
        protected override string CsvFileName => "pg_namespace.csv";

        public override IReadOnlyList<PgVirtualColumn> Columns { get; } = new PgVirtualColumn[]
        {
            new("oid",     PgOid.Default,  PgFormat.Text),
            new("nspname", PgName.Default, PgFormat.Text),
        };
    }

    internal sealed class PgCatalogPgClassTable : PgVirtualTable
    {
        private const int PublicNamespaceOid = 2200;
        private const string OrdinaryTable = "r";
        private const string Index = "i";
        private const int NotACompositeType = 0;

        private const object NoStorageParameters = null;
        private const object NoAccessMethod = null;

        public override string SchemaName => "pg_catalog";
        public override string TableName => "pg_class";

        public override IReadOnlyList<PgVirtualColumn> Columns { get; } = new PgVirtualColumn[]
        {
            new("oid",          PgOid.Default,  PgFormat.Text),
            new("relname",      PgName.Default, PgFormat.Text),
            new("relnamespace", PgOid.Default,  PgFormat.Text),
            new("relkind",      PgChar.Default, PgFormat.Text),
            new("typrelid",     PgOid.Default,  PgFormat.Text),
            // Appended so the columns above keep their positions.
            new("reloptions",   PgText.Default, PgFormat.Text),
            new("relam",        PgOid.Default,  PgFormat.Text),
        };

        public override IEnumerable<object[]> EnumerateRows(VirtualQueryContext ctx)
        {
            foreach (var relation in CollectionCatalog.Relations(ctx))
            {
                yield return new object[]
                {
                    relation.Oid, relation.Name, PublicNamespaceOid, OrdinaryTable, NotACompositeType,
                    NoStorageParameters, NoAccessMethod
                };

                yield return new object[]
                {
                    relation.PkIndexOid, relation.PkName, PublicNamespaceOid, Index, NotACompositeType,
                    NoStorageParameters, NoAccessMethod
                };
            }
        }
    }

    internal sealed class PgCatalogPgAttributeTable : PgVirtualTable
    {
        // Honored so reflecting one relation costs one document read, not one per collection.
        private const string AttRelIdPredicate = "attrelid";

        private const string NotAnIdentityColumn = "";
        private const string NotAGeneratedColumn = "";

        public override string SchemaName => "pg_catalog";
        public override string TableName => "pg_attribute";

        public override IReadOnlyList<PgVirtualColumn> Columns { get; } = new PgVirtualColumn[]
        {
            // Real pg_attribute has no oid column; declared (always NULL) so projections resolve.
            new("oid",          PgOid.Default,  PgFormat.Text),
            new("attrelid",     PgOid.Default,  PgFormat.Text),
            new("attname",      PgName.Default, PgFormat.Text),
            new("atttypid",     PgOid.Default,  PgFormat.Text),
            new("attnum",       PgInt2.Default, PgFormat.Text),
            new("atttypmod",    PgInt4.Default, PgFormat.Text),
            new("attnotnull",   PgBool.Default, PgFormat.Text),
            new("atthasdef",    PgBool.Default, PgFormat.Text),
            new("attidentity",  PgChar.Default, PgFormat.Text),
            new("attgenerated", PgChar.Default, PgFormat.Text),
            new("attisdropped", PgBool.Default, PgFormat.Text),
            new("attlen",       PgInt2.Default, PgFormat.Text),
        };

        public override IEnumerable<object[]> EnumerateRows(VirtualQueryContext ctx)
        {
            if (ctx?.Database == null)
                yield break;

            long? onlyRelation = null;
            if (ctx.Predicates != null &&
                ctx.Predicates.TryGetValue(AttRelIdPredicate, out var rawOid) &&
                CatalogOid.TryRead(rawOid, out var predicateOid))
                onlyRelation = predicateOid;

            using (ctx.Database.DocumentsStorage.ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            using (context.OpenReadTransaction())
            {
                foreach (var relation in CollectionCatalog.Relations(ctx))
                {
                    if (onlyRelation.HasValue && relation.Oid != onlyRelation.Value)
                        continue;

                    // PG reserves attnum <= 0 for system attributes.
                    short attnum = 1;
                    foreach (var column in CollectionCatalog.Columns(ctx.Database, context, relation.Name))
                    {
                        yield return new object[]
                        {
                            null, relation.Oid, column.Name, column.PgType.Oid,
                            attnum++, column.PgType.TypeModifier,
                            false, false,
                            NotAnIdentityColumn, NotAGeneratedColumn,
                            false, column.PgType.Size
                        };
                    }
                }
            }
        }
    }

    internal sealed class PgCatalogPgIndexTable : PgVirtualTable
    {
        private static readonly string DocumentIdIndKey = CollectionCatalog.DocumentIdAttNum.ToString();
        private const string DefaultIndOption = "0";

        public override string SchemaName => "pg_catalog";
        public override string TableName => "pg_index";

        public override IReadOnlyList<PgVirtualColumn> Columns { get; } = new PgVirtualColumn[]
        {
            new("indrelid",     PgOid.Default,  PgFormat.Text),
            new("indexrelid",   PgOid.Default,  PgFormat.Text),
            new("indisunique",  PgBool.Default, PgFormat.Text),
            new("indisprimary", PgBool.Default, PgFormat.Text),
            new("indexprs",     PgText.Default, PgFormat.Text),
            new("indpred",      PgText.Default, PgFormat.Text),
            new("indkey",       PgText.Default, PgFormat.Text),
            new("indoption",    PgText.Default, PgFormat.Text),
            new("indnkeyatts",  PgInt2.Default, PgFormat.Text),
        };

        public override IEnumerable<object[]> EnumerateRows(VirtualQueryContext ctx)
        {
            foreach (var relation in CollectionCatalog.Relations(ctx))
            {
                yield return new object[]
                {
                    relation.Oid, relation.PkIndexOid, true, true, null, null,
                    DocumentIdIndKey, DefaultIndOption, (short)1
                };
            }
        }
    }

    internal sealed class PgCatalogPgConstraintTable : PgVirtualTable
    {
        private const string PrimaryKey = "p";
        private static readonly string DocumentIdConKey = "{" + CollectionCatalog.DocumentIdAttNum + "}";
        private const int NoReferencedRelation = 0;

        public override string SchemaName => "pg_catalog";
        public override string TableName => "pg_constraint";

        public override IReadOnlyList<PgVirtualColumn> Columns { get; } = new PgVirtualColumn[]
        {
            new("oid",       PgOid.Default,  PgFormat.Text),
            new("conname",   PgName.Default, PgFormat.Text),
            new("conrelid",  PgOid.Default,  PgFormat.Text),
            new("contype",   PgChar.Default, PgFormat.Text),
            new("conkey",    PgText.Default, PgFormat.Text),
            new("confrelid", PgOid.Default,  PgFormat.Text),
            new("conindid",  PgOid.Default,  PgFormat.Text),
        };

        public override IEnumerable<object[]> EnumerateRows(VirtualQueryContext ctx)
        {
            foreach (var relation in CollectionCatalog.Relations(ctx))
            {
                yield return new object[]
                {
                    relation.PkConstraintOid, relation.PkName, relation.Oid, PrimaryKey,
                    DocumentIdConKey, NoReferencedRelation, relation.PkIndexOid
                };
            }
        }
    }
}
