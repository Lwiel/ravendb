using System;
using FastTests;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Bugs
{
    public class DateTimeInLocalTime : RavenTestBase
    {
        public DateTimeInLocalTime(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void CanSaveAndLoadSameTimeLocalTurnsIntoUnspecified()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    var serviceExecutionLog = new ServiceExecutionLog
                    {
                        LastDateChecked = new DateTime(2010, 2, 17, 19, 06, 06,DateTimeKind.Local)
                    };

                    session.Store(serviceExecutionLog);

                    session.SaveChanges();

                }

                using (var session = store.OpenSession())
                {
                    var log = session.Load<ServiceExecutionLog>("ServiceExecutionLogs/1-A");
                    Assert.Equal(new DateTime(2010, 2, 17, 19, 06, 06), log.LastDateChecked);
                    Assert.Equal(DateTimeKind.Unspecified, log.LastDateChecked.Kind);
                }
            }
        }

        [Fact]
        public void CanSaveAndLoadSameTimeUnspecified()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    var serviceExecutionLog = new ServiceExecutionLog
                    {
                        LastDateChecked = new DateTime(2010, 2, 17, 19, 06, 06, DateTimeKind.Unspecified)
                    };

                    session.Store(serviceExecutionLog);

                    session.SaveChanges();

                }

                using (var session = store.OpenSession())
                {
                    var log = session.Load<ServiceExecutionLog>("ServiceExecutionLogs/1-A");
                    Assert.Equal(new DateTime(2010, 2, 17, 19, 06, 06), log.LastDateChecked);
                }
            }
        }

        [Fact]
        public void CanSaveAndLoadSameTimeUtc()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    var serviceExecutionLog = new ServiceExecutionLog
                    {
                        LastDateChecked = new DateTime(2010, 2, 17, 19, 06, 06, DateTimeKind.Utc)
                    };

                    session.Store(serviceExecutionLog);

                    session.SaveChanges();

                }

                using (var session = store.OpenSession())
                {
                    var log = session.Load<ServiceExecutionLog>("ServiceExecutionLogs/1-A");
                    Assert.Equal(new DateTime(2010, 2, 17, 19, 06, 06), log.LastDateChecked);
                    Assert.Equal(DateTimeKind.Utc, log.LastDateChecked.Kind);
                }
            }
        }

        private class ServiceExecutionLog
        {
            public DateTime LastDateChecked { get; set; }
            public string Id { get; set; }
        }
    }
}
