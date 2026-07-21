using System;
using System.IO;

namespace Raven.Server.Integrations.PostgreSQL
{
    // TEMPORARY diagnostic (RavenDB-26925): appends every SQL statement a PG client sends to a file so we
    // can capture the exact getColumns()/getSchemas()/getTables() queries a real driver (Tableau's pgJDBC)
    // emits. Enabled only when the RAVEN_PG_CAPTURE env var points at a file; otherwise a no-op. Sits at
    // PgQuery.CreateInstance, the single choke point for BOTH the Simple and Extended query protocols
    // (TrafficWatch only sees Simple). REMOVE before merge.
    internal static class PgQueryCapture
    {
        private static readonly string CaptureFile = Environment.GetEnvironmentVariable("RAVEN_PG_CAPTURE");
        private static readonly object Gate = new();

        public static void TryLog(string queryText)
        {
            if (string.IsNullOrEmpty(CaptureFile) || string.IsNullOrWhiteSpace(queryText))
                return;

            try
            {
                lock (Gate)
                {
                    File.AppendAllText(CaptureFile,
                        "-- " + DateTime.UtcNow.ToString("O") + Environment.NewLine +
                        queryText + Environment.NewLine +
                        ";;" + Environment.NewLine + Environment.NewLine);
                }
            }
            catch
            {
                // Diagnostic only - never let capture affect the query path.
            }
        }
    }
}
