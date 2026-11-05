using System.Collections.Generic;
using Raven.Server.Integrations.PostgreSQL.Messages;
using Raven.Server.Integrations.PostgreSQL.Types;

namespace Raven.Server.Integrations.PostgreSQL.VirtualCatalog.Tables
{
    internal sealed class PgCatalogPgSettingsTable : PgVirtualTable
    {
        public override string SchemaName => "pg_catalog";
        public override string TableName => "pg_settings";

        public override IReadOnlyList<PgVirtualColumn> Columns { get; } = new PgVirtualColumn[]
        {
            new("name",    PgText.Default, PgFormat.Text),
            new("setting", PgText.Default, PgFormat.Text),
        };

        public override IEnumerable<object[]> EnumerateRows(VirtualQueryContext ctx)
        {
            foreach (var (name, setting) in PgSettings.All)
                yield return new object[] { name, setting };
        }
    }
}
