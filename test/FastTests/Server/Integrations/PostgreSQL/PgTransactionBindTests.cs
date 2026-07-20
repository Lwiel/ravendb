using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Raven.Server.Integrations.PostgreSQL;
using Raven.Server.Integrations.PostgreSQL.Exceptions;
using Raven.Server.Integrations.PostgreSQL.Messages;
using Raven.Server.Integrations.PostgreSQL.Types;
using Tests.Infrastructure;
using Xunit;

namespace FastTests.Server.Integrations.PostgreSQL
{
    public sealed class PgTransactionBindTests(ITestOutputHelper output) : NoDisposalNeeded(output)
    {
        [RavenFact(RavenTestCategory.PostgreSql)]
        public void Bind_to_unknown_prepared_statement_is_recoverable_not_fatal()
        {
            var session = new PgSession(client: null, serverCertificateHolder: null, identifier: 0, processId: 0, serverStore: null, token: default);
            using var transaction = new PgTransaction(documentDatabase: null, messageReader: new MessageReader(), username: null, session: session);

            // NamedStatements is empty, so this name resolves to nothing.
            var error = Assert.Throws<PgErrorException>(() => transaction.Bind(
                parameters: Array.Empty<byte[]>(),
                parameterFormatCodes: Array.Empty<short>(),
                resultColumnFormatCodes: Array.Empty<short>(),
                statementName: "statement_the_server_never_prepared"));

            Assert.Equal(PgErrorCodes.InvalidSqlStatementName, error.ErrorCode);
        }

        // Re-binding the same (prepared) statement instance with a parameter must not throw: PgQuery.Bind
        // re-populates the once-allocated Parameters dictionary, so without a Clear the 2nd bind throws
        // ArgumentException on the duplicate "1" key and the connection dies (the Npgsql auto-prepare path).
        // The pre-existing reuse test used a parameterless query, so the bind loop never ran.
        [RavenFact(RavenTestCategory.PostgreSql)]
        public void Rebinding_a_parameterized_statement_does_not_throw()
        {
            using var query = new RqlQuery("from Orders", new[] { PgTypeOIDs.Text }, documentDatabase: null);
            var parameters = new List<byte[]> { Encoding.UTF8.GetBytes("hello") };
            var textFormat = new short[] { 0 };

            query.Bind(parameters, textFormat, Array.Empty<short>());
            query.Bind(parameters, textFormat, Array.Empty<short>()); // 2nd bind must not throw

            Assert.Single(query.Parameters);
        }

        // A named/prepared statement is cached in Session.NamedStatements and only borrowed by the
        // transaction. Sync()/Close() must NOT dispose it (it stays reusable for the next Bind/Execute);
        // session teardown (Dispose) drains and disposes it so it doesn't leak.
        [RavenFact(RavenTestCategory.PostgreSql)]
        public void Named_statement_survives_sync_and_is_disposed_on_teardown()
        {
            var session = new PgSession(client: null, serverCertificateHolder: null, identifier: 0, processId: 0, serverStore: null, token: default);
            var transaction = new PgTransaction(documentDatabase: null, messageReader: new MessageReader(), username: null, session: session);

            var named = new TrackingPgQuery();
            transaction._currentQuery = named;       // the just-Parsed statement
            transaction.RegisterNamedStatement("S");  // cache it under a name

            transaction.Sync();
            Assert.False(named.Disposed);             // borrowed - not disposed on reset
            Assert.True(session.NamedStatements.ContainsKey("S"));

            transaction.Dispose();
            Assert.True(named.Disposed);              // drained on teardown
            Assert.Empty(session.NamedStatements);
        }

        // An unnamed (transient) statement is owned by the transaction and IS disposed on reset.
        [RavenFact(RavenTestCategory.PostgreSql)]
        public void Unnamed_statement_is_disposed_on_sync()
        {
            var session = new PgSession(client: null, serverCertificateHolder: null, identifier: 0, processId: 0, serverStore: null, token: default);
            using var transaction = new PgTransaction(documentDatabase: null, messageReader: new MessageReader(), username: null, session: session);

            var unnamed = new TrackingPgQuery();
            transaction._currentQuery = unnamed;      // never registered → owned transient

            transaction.Sync();
            Assert.True(unnamed.Disposed);
        }

        // ReadBytesAsync must return an array of EXACTLY the requested length. Before the fix it returned
        // the (usually larger) ArrayPool.Rent(length) buffer, so every Bind parameter carried a garbage
        // tail (pgJDBC's getTables() came back empty because the type/table-name params decoded wrong).
        [RavenFact(RavenTestCategory.PostgreSql)]
        public async Task ReadBytesAsync_returns_exactly_the_requested_length()
        {
            var payload = Encoding.UTF8.GetBytes("hello");

            var pipe = new Pipe();
            await pipe.Writer.WriteAsync(payload);
            await pipe.Writer.CompleteAsync();

            using var messageReader = new MessageReader();
            var result = await messageReader.ReadBytesAsync(pipe.Reader, payload.Length, CancellationToken.None);

            Assert.Equal(payload.Length, result.Length);
            Assert.Equal(payload, result);
        }

        // A full Bind message parsed through the real MessageReader must decode a text parameter to its
        // exact bytes - no trailing pool garbage. This is the end-to-end shape that broke JDBC/ODBC.
        [RavenFact(RavenTestCategory.PostgreSql)]
        public async Task Bind_decodes_text_parameter_without_trailing_garbage()
        {
            var value = Encoding.UTF8.GetBytes("Orders");
            var bind = await ParseBindMessage(new List<byte[]> { value });

            Assert.Single(bind.Parameters);
            Assert.NotNull(bind.Parameters[0]);
            Assert.Equal(value.Length, bind.Parameters[0].Length);
            Assert.Equal(value, bind.Parameters[0]);
        }

        // A parameter length of -1 is a SQL NULL argument with no value bytes. It must parse to a null
        // entry (not throw on a Slice(0, -1)) and decode to a null query parameter.
        [RavenFact(RavenTestCategory.PostgreSql)]
        public async Task Bind_handles_null_parameter()
        {
            var bind = await ParseBindMessage(new List<byte[]> { null });

            Assert.Single(bind.Parameters);
            Assert.Null(bind.Parameters[0]);

            using var query = new RqlQuery("from Orders", new[] { PgTypeOIDs.Text }, documentDatabase: null);
            query.Bind(bind.Parameters, bind.ParameterFormatCodes, bind.ResultColumnFormatCodes);

            Assert.Single(query.Parameters);
            Assert.Null(query.Parameters["1"]);
        }

        // Builds a wire Bind message (text-format parameters, no result format codes), feeds it through
        // the real MessageReader, and returns the parsed Bind. A trailing pad keeps the final Int16 read
        // satisfiable (ReadInt16Async buffers up to sizeof(int)) without completing the pipe.
        private static async Task<Bind> ParseBindMessage(List<byte[]> parameters)
        {
            var body = new List<byte>();
            body.Add(0);                                  // portal name (empty, null-terminated)
            body.Add(0);                                  // statement name (empty, null-terminated)
            AppendInt16(body, 0);                         // parameter format code count (0 => all text)
            AppendInt16(body, (short)parameters.Count);   // parameter count
            foreach (var p in parameters)
            {
                if (p == null)
                {
                    AppendInt32(body, -1);                // SQL NULL
                }
                else
                {
                    AppendInt32(body, p.Length);
                    body.AddRange(p);
                }
            }
            AppendInt16(body, 0);                         // result column format code count

            var message = new List<byte>();
            AppendInt32(message, body.Count + sizeof(int)); // length field includes itself
            message.AddRange(body);
            message.AddRange(new byte[sizeof(int)]);        // pad so the last Int16 read has enough buffered

            var pipe = new Pipe();
            await pipe.Writer.WriteAsync(message.ToArray());
            await pipe.Writer.FlushAsync();

            using var messageReader = new MessageReader();
            var bind = new Bind();
            await bind.Init(messageReader, pipe.Reader, CancellationToken.None);
            return bind;
        }

        private static void AppendInt16(List<byte> target, short value)
        {
            Span<byte> tmp = stackalloc byte[sizeof(short)];
            BinaryPrimitives.WriteInt16BigEndian(tmp, value);
            target.AddRange(tmp.ToArray());
        }

        private static void AppendInt32(List<byte> target, int value)
        {
            Span<byte> tmp = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(tmp, value);
            target.AddRange(tmp.ToArray());
        }

        private sealed class TrackingPgQuery() : PgQuery("from Tracking", Array.Empty<int>())
        {
            public bool Disposed { get; private set; }
            public override Task<ICollection<PgColumn>> Init() => Task.FromResult<ICollection<PgColumn>>(Array.Empty<PgColumn>());
            public override Task Execute(MessageBuilder builder, PipeWriter writer, CancellationToken token) => Task.CompletedTask;
            public override void Dispose() => Disposed = true;
        }
    }
}
