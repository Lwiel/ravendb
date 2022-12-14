using System.Linq;
using FastTests;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Issues
{
    public class RavenDB1260 : RavenTestBase
    {
        public RavenDB1260(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void IndexDefinitionWithCastingOfTypeNotDefinedServerSideTest()
        {
            using (var store = GetDocumentStore())
            {
                new MapIndex().Execute(store);

                var indexes = store.Maintenance.Send(new GetIndexNamesOperation(0, 128));
                Assert.Contains("MapIndex", indexes);
            }

        }

        private class MapIndex : AbstractMultiMapIndexCreationTask<MapIndex.Result>
        {
            public class Result
            {
                public string BuildingId { get; set; }
                public string Name { get; set; }
                public string Address { get; set; }
            }

            public MapIndex()
            {
                AddMap<Building>(buildings => from b in buildings
                                              let tenant = (TenantBase)b.Tenant ?? b.Owner
                                              select new
                                              {
                                                  BuildingId = b.Id,
                                                  Name = b.BuildingName,
                                                  Address = tenant.Address

                                              });

                AddMap<Unit>(units => from u in units
                                      let tenant = (TenantBase)u.Tenant ?? u.Owner
                                      select new
                                      {
                                          BuildingId = u.BuildingId,
                                          Name = u.UnitName,
                                          Address = tenant.Address
                                      });

                Reduce = results => from r in results
                                    group r by new { r.BuildingId }
                                        into gr
                                    select new Result
                                    {
                                        BuildingId = gr.Key.BuildingId,
                                        Address = gr.Select(x => x.Address).FirstOrDefault(x => x != null),
                                        Name = gr.Select(x => x.Name).FirstOrDefault(x => x != null)
                                    };
            }
        }

        private class Building
        {
            public string Id { get; set; }
            public string BuildingName { get; set; }
            public CompanyLessee Tenant { get; set; }

            public Owner Owner { get; set; }

        }

        private class Unit
        {
            public string BuildingId { get; set; }
            public string UnitName { get; set; }
            public Lessee Tenant { get; set; }

            public Owner Owner { get; set; }
        }

        private abstract class TenantBase
        {
            public string Address { get; set; }
        }

        private class CompanyLessee : TenantBase
        {
        }

        private class Lessee : TenantBase
        {
        }

        private class Owner : TenantBase
        {
        }
    }
}
