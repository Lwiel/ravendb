﻿using System;
using System.Linq;
using System.Threading.Tasks;
using FastTests.Server.Replication;
using FastTests.Utils;
using Raven.Client;
using Raven.Client.Documents.Session;
using Raven.Tests.Core.Utils.Entities;
using Sparrow.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Client.TimeSeries.Issues
{
    public class RavenDB_13484 : ReplicationTestBase
    {
        public RavenDB_13484(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task CanGetTimeSeriesSnapshotInRevisions()
        {
            using (var store = GetDocumentStore())
            {
                await RevisionsHelper.SetupRevisions(Server.ServerStore, store.Database);
                using (var session = store.OpenAsyncSession())
                {
                    // revision 1
                    await session.StoreAsync(new Company(), "companies/1-A");
                    await session.SaveChangesAsync();
                }

                var baseline = DateTime.Today;

                using (var session = store.OpenAsyncSession())
                {
                    var company = await session.LoadAsync<Company>("companies/1-A");

                    // revision 2
                    company.Name = "HR";
                    await session.SaveChangesAsync();

                    var tsf = session.TimeSeriesFor(company);

                    // revision 3
                    tsf.Append("temperature", baseline.AddMinutes(10), null, new []{ 17.5d });
                    tsf.Append("temperature", baseline.AddMinutes(20), null, new[] { 17.4d });

                    await session.SaveChangesAsync();

                    // no revision for this one
                    tsf.Append("temperature", baseline.AddMinutes(30), null, new[] { 17.2d });

                    await session.SaveChangesAsync();
                }

                using (var session = store.OpenAsyncSession())
                {
                    var companiesRevisions = await session.Advanced.Revisions.GetForAsync<Company>("companies/1-A");
                    Assert.Equal(3, companiesRevisions.Count);
                    var metadatas = companiesRevisions.Select(c => session.Advanced.GetMetadataFor(c)).ToList();

                    Assert.Equal("HR", companiesRevisions[0].Name);

                    var tsRevisions = (IMetadataDictionary)metadatas[0][Constants.Documents.Metadata.RevisionTimeSeries];
                    Assert.Equal(1, tsRevisions.Count);

                    var tsStats = (IMetadataDictionary)tsRevisions["temperature"];
                    Assert.Equal(2L, tsStats["Count"]);
                    Assert.Equal(baseline.AddMinutes(10).GetDefaultRavenFormat(), tsStats["Start"]);
                    Assert.Equal(baseline.AddMinutes(20).GetDefaultRavenFormat(), tsStats["End"]);

                    Assert.Equal("HR", companiesRevisions[1].Name);
                    Assert.False(metadatas[1].TryGetValue(Constants.Documents.Metadata.RevisionTimeSeries, out _));

                    Assert.Null(companiesRevisions[2].Name);
                    Assert.False(metadatas[1].TryGetValue(Constants.Documents.Metadata.RevisionTimeSeries, out _));
                }

                using (var session = store.OpenAsyncSession())
                {
                    var company = await session.LoadAsync<Company>("companies/1-A");

                    // revision 4
                    company.Name = "Hibernating Rhinos";
                    await session.SaveChangesAsync();

                    // revision 5
                    var tsf = session.TimeSeriesFor(company);
                    tsf.Append("temperature", baseline.AddMonths(6), null, new[] { 27.6d }); // will create a new segment

                    tsf.Append("heartrate", baseline.AddHours(1), null, new[] { 92.8d });
                    tsf.Append("heartrate", baseline.AddHours(2), null, new[] { 89d });

                    await session.SaveChangesAsync();
                }

                using (var session = store.OpenAsyncSession())
                {
                    var companiesRevisions = await session.Advanced.Revisions.GetForAsync<Company>("companies/1-A");
                    Assert.Equal(5, companiesRevisions.Count);
                    var metadatas = companiesRevisions.Select(c => session.Advanced.GetMetadataFor(c)).ToList();

                    Assert.Equal("Hibernating Rhinos", companiesRevisions[0].Name);
                    var tsRevisions = (IMetadataDictionary)metadatas[0][Constants.Documents.Metadata.RevisionTimeSeries];
                    Assert.Equal(2, tsRevisions.Count);

                    var tsStats = (IMetadataDictionary)tsRevisions["temperature"];
                    Assert.Equal(4L, tsStats["Count"]);
                    Assert.Equal(baseline.AddMinutes(10).GetDefaultRavenFormat(), tsStats["Start"]);
                    Assert.Equal(baseline.AddMonths(6).GetDefaultRavenFormat(), tsStats["End"]);

                    tsStats = (IMetadataDictionary)tsRevisions["heartrate"];
                    Assert.Equal(2L, tsStats["Count"]);
                    Assert.Equal(baseline.AddHours(1).GetDefaultRavenFormat(), tsStats["Start"]);
                    Assert.Equal(baseline.AddHours(2).GetDefaultRavenFormat(), tsStats["End"]);

                    Assert.Equal("Hibernating Rhinos", companiesRevisions[1].Name);
                    tsRevisions = (IMetadataDictionary)metadatas[1][Constants.Documents.Metadata.RevisionTimeSeries];
                    Assert.Equal(1, tsRevisions.Count);
                    tsStats = (IMetadataDictionary)tsRevisions["temperature"];

                    Assert.Equal(3L, tsStats["Count"]);
                    Assert.Equal(baseline.AddMinutes(10).GetDefaultRavenFormat(), tsStats["Start"]);
                    Assert.Equal(baseline.AddMinutes(30).GetDefaultRavenFormat(), tsStats["End"]);

                    Assert.Equal("HR", companiesRevisions[2].Name);
                    tsRevisions = (IMetadataDictionary)metadatas[2][Constants.Documents.Metadata.RevisionTimeSeries];
                    Assert.Equal(1, tsRevisions.Count);
                    tsStats = (IMetadataDictionary)tsRevisions["temperature"];

                    Assert.Equal(2L, tsStats["Count"]);
                    Assert.Equal(baseline.AddMinutes(10).GetDefaultRavenFormat(), tsStats["Start"]);
                    Assert.Equal(baseline.AddMinutes(20).GetDefaultRavenFormat(), tsStats["End"]);

                    Assert.Equal("HR", companiesRevisions[3].Name);
                    Assert.False(metadatas[3].TryGetValue(Constants.Documents.Metadata.RevisionCounters, out _));

                    Assert.Null(companiesRevisions[4].Name);
                    Assert.False(metadatas[4].TryGetValue(Constants.Documents.Metadata.RevisionCounters, out _));
                }
            }
        }

    }
}
