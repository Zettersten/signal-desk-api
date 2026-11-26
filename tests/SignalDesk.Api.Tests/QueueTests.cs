using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SignalDesk.Api.AI;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Tests;

[Collection("Queue")]
public sealed class QueueTests
{
    [Fact]
    public async Task Accepted_run_is_persisted_and_processed_by_the_real_queue_without_paid_calls()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new DeskFactory("live", collectors: true, customize: services =>
        {
            services.RemoveAll<ISearchService>();
            services.RemoveAll<IIntelligenceModel>();
            services.AddSingleton<ISearchService, EmptySearch>();
            services.AddSingleton<IIntelligenceModel, TestPlanner>();
        });
        using var client = factory.CreateClient();
        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<DeskDb>>();
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var source = await db.Sources.SingleAsync(x => x.Id == "iceye", ct);
            source.Approved = true;
            await db.SaveChangesAsync(ct);
        }
        var accepted = await client.PostAsJsonAsync("/v1/ingestion/runs", new { sourceId = "iceye" }, ct);
        Assert.Equal(System.Net.HttpStatusCode.Accepted, accepted.StatusCode);
        var run = (await accepted.Content.ReadFromJsonAsync<CollectionRun>(Wire.Json, ct))!;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await Task.Delay(50, ct);
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var saved = await db.Runs.SingleAsync(x => x.Id == run.Id, ct);
            if (!string.Equals(saved.State, "succeeded")) continue;
            Assert.NotNull(saved.JobTrackingId);
            Assert.Single(await db.Jobs.ToArrayAsync(ct));
            Assert.Empty(await db.AiExecutions.ToArrayAsync(ct));
            return;
        }
        Assert.Fail("The real durable queue did not complete the offline run.");
    }

    private sealed class EmptySearch : ISearchService
    {
        public Task<SearchResult> SearchAsync(string[] queries, string[] domains, DateTime since, CancellationToken ct) =>
            Task.FromResult(new SearchResult("offline-queue-test", []));
    }
    private sealed class TestPlanner : IIntelligenceModel
    {
        public Task<ResearchQueries> PlanAsync(string publisher, string[] topics, string[] companies, CancellationToken ct) =>
            Task.FromResult(new ResearchQueries(["ICEYE commercial imagery"]));
        public Task<SignalDraft> TransformAsync(DocumentRow document, Competitor[] companies, CancellationToken ct) =>
            throw new InvalidOperationException("No document should reach a provider in this test.");
        public Task<BriefDraft> BriefAsync(BriefInput input, CancellationToken ct) =>
            throw new InvalidOperationException("No brief should reach a provider in this test.");
    }
}
