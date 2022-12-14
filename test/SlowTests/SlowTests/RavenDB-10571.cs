using System.Linq;
using FastTests;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.SlowTests
{
    public class RavenDB_10571 : RavenTestBase
    {
        public RavenDB_10571(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void CanIterateOverBlittableProperties()
        {
            using (var store = GetDocumentStore())
            {
                using (var s = store.OpenSession())
                {
                    s.Store(new { A = 1, B = 2 });
                    s.SaveChanges();
                }
                using (var s = store.OpenSession())
                {
                    var item = s.Advanced.RawQuery<dynamic>(@"
from @all_docs as e
select {
    Properties: Object.keys(e)
}
").First();
                    Assert.Equal(new[] {"@metadata", "A", "B" }, ((JArray)item.Properties).Values<string>().ToArray());

                }
            }
        }
    }
}
