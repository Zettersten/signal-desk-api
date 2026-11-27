using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SignalDesk.Api.AI;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Tests;

public sealed class RunCountsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Run_counts_only_documents_it_changed_not_pre_existing_source_totals()
    {
        using var factory = new DeskFactory("live", customize: services =>
        {
            services.RemoveAll<ISearchService>();
            services.RemoveAll<IIntelligenceModel>();
            services.AddSingleton<ISearchService, EmptySearch>();
            services.AddSingleton<IIntelligenceModel, TestPlanner>();
        });
        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<DeskDb>>();
        var run = new CollectionRun { SourceId = "iceye", CreatedUtc = DateTime.UtcNow };
        await using (var db = await dbFactory.CreateDbContextAsync(Ct))
        {
            var source = await db.Sources.SingleAsync(x => x.Id == "iceye", Ct);
            source.Approved = true;
            source.AllowArticleCapture = true;
            db.Documents.Add(new DocumentRow
            {
                Id = "doc-old-published",
                SourceId = "iceye",
                CanonicalUrl = "https://iceye.com/news/old",
                Title = "Earlier announcement",
                PublishedUtc = DateTime.UtcNow.AddDays(-3),
                FetchedUtc = DateTime.UtcNow.AddDays(-3),
                ContentHash = "old-hash",
                ParagraphsJson = Wire.Encode(new[] { new Paragraph("p1", "An earlier announcement.") }),
                State = "published"
            });
            db.Runs.Add(run);
            await db.SaveChangesAsync(Ct);
        }

        var collector = factory.Services.GetRequiredService<SourceCollector>();
        await collector.CollectAsync(run.Id, Ct);

        await using var check = await dbFactory.CreateDbContextAsync(Ct);
        var saved = await check.Runs.SingleAsync(x => x.Id == run.Id, Ct);
        Assert.Equal("succeeded", saved.State);
        Assert.Equal(0, saved.Published);
        Assert.Equal(0, saved.Reviewed);
    }

    private sealed class EmptySearch : ISearchService
    {
        public Task<SearchResult> SearchAsync(string[] queries, string[] domains, DateTime since, CancellationToken ct) =>
            Task.FromResult(new SearchResult("offline-run-counts", []));
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
