using System;
using System.Linq;
using Xunit.Abstractions;

using FastTests;

using Xunit;

namespace SlowTests.Tests.Queries
{
    public class CanQueryOnLargeXml : RavenTestBase
    {
        public CanQueryOnLargeXml(ITestOutputHelper output) : base(output)
        {
        }

        private class Item
        {
#pragma warning disable 649
            public string SchemaFullName;
            public string ValueBlobString;
#pragma warning restore 649
        }

        [Fact]
        public void Remote()
        {
            using (var store = GetDocumentStore())
            {
                using (var s = store.OpenSession())
                {
                    var xml =
                        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<__Root__>\r\n  <_STD s=\"DateTime\" t=\"System.DateTime, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\" /> <dateTime xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/\">2009-01-14T23:00:57.087Z</dateTime> </__Root__>";
                    var guid = Guid.NewGuid().ToString();

                    var s1 = s.Query<Item>()
                        .Where(x => x.SchemaFullName.Equals(guid) && x.ValueBlobString.Equals(xml))
                        .ToString();

                    var s2 = s.Query<Item>()
                        .Where(x => x.SchemaFullName == guid && x.ValueBlobString == xml)
                        .ToString();

                    Assert.Equal(s1, s2);

                    s.Query<Item>()
                        .Where(x => x.SchemaFullName.Equals(guid) && x.ValueBlobString.Equals(xml))
                        .ToList();
                }
            }
        }
    }
}
