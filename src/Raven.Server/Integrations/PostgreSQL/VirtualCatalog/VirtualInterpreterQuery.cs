using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Raven.Server.Documents;
using Raven.Server.Integrations.PostgreSQL.Messages;

namespace Raven.Server.Integrations.PostgreSQL.VirtualCatalog
{
    // Wraps a PgTable produced by PgVirtualInterpreter and streams it over the wire.
    //
    // The table is first computed at Parse time (before Bind), which fixes the column shape but leaves
    // any $N parameter references as NULL. For a parameterized catalog probe (e.g. pgJDBC's getTables,
    // `nspname LIKE $1`) that would filter out every row. So when parameters have been bound, Execute
    // re-runs the interpreter with the real values threaded through VirtualQueryContext.Parameters.
    internal sealed class VirtualInterpreterQuery : PgQuery
    {
        private readonly PgTable _result;
        private readonly DocumentDatabase _database;
        private readonly string _username;

        public VirtualInterpreterQuery(string queryString, int[] parametersDataTypes, PgTable result, DocumentDatabase database = null, string username = null)
            : base(queryString, parametersDataTypes)
        {
            _result = result;
            _database = database;
            _username = username;
        }

        public override Task<ICollection<PgColumn>> Init()
        {
            if (IsEmptyQuery)
                return Task.FromResult<ICollection<PgColumn>>(null);

            // The column shape is parameter-independent, so the Parse-time result is authoritative here.
            return Task.FromResult<ICollection<PgColumn>>(_result?.Columns);
        }

        public override async Task Execute(MessageBuilder builder, PipeWriter writer, CancellationToken token)
        {
            var table = _result;

            // Re-interpret with the bound parameter values so $N references resolve to real values
            // instead of the Parse-time NULL. Only when there actually are parameters and a database.
            if (Parameters is { Count: > 0 } && _database != null)
            {
                var ctx = new VirtualQueryContext { Database = _database, Username = _username, Parameters = Parameters };
                if (PgVirtualInterpreter.TryExecute(QueryString, ctx, out var recomputed))
                    table = recomputed;
            }

            if (table?.Data != null)
            {
                var columns = _result?.Columns;
                var hasBinaryColumns = columns != null && columns.Exists(c => c.FormatCode == PgFormat.Binary);

                foreach (var dataRow in table.Data)
                {
                    var cells = hasBinaryColumns ? ToResultFormats(dataRow.ColumnData, columns) : dataRow.ColumnData;
                    await writer.WriteAsync(builder.DataRow(cells.Span), token);
                }
            }

            var tag = table?.CommandTag ?? $"SELECT {table?.Data?.Count ?? 0}";
            await writer.WriteAsync(builder.CommandComplete(tag), token);
        }

        protected override IEnumerable<PgColumn> ResultColumns => _result?.Columns ?? Enumerable.Empty<PgColumn>();

        private static Memory<ReadOnlyMemory<byte>?> ToResultFormats(Memory<ReadOnlyMemory<byte>?> textCells, List<PgColumn> columns)
        {
            var cells = textCells.ToArray();
            for (var i = 0; i < cells.Length && i < columns.Count; i++)
            {
                if (columns[i].FormatCode == PgFormat.Binary && cells[i] is { } text)
                    cells[i] = columns[i].PgType.ToBytes(columns[i].PgType.FromString(Encoding.UTF8.GetString(text.Span)), PgFormat.Binary);
            }

            return cells;
        }

        public override void Dispose()
        {
        }
    }
}
