using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Tests;

public sealed class IntelligenceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.1.1")]
    [InlineData("192.168.1.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    public void Nonpublic_destinations_are_rejected(string value) => Assert.False(SafeFetcher.IsPublic(IPAddress.Parse(value)));

    [Theory]
    [InlineData("http://spacenews.com/a")]
    [InlineData("https://127.0.0.1/a")]
    [InlineData("https://spacenews.com.attacker.example/a")]
    [InlineData("https://user:password@spacenews.com/a")]
    [InlineData("https://spacenews.com:8080/a")]
    public void Fetch_rejects_unapproved_schemes_hosts_and_ports(string url) =>
        Assert.Throws<ApiException>(() => SafeFetcher.Validate(url, "https://spacenews.com"));

    [Fact]
    public void Article_capture_preserves_source_paragraphs_and_exact_timestamp()
    {
        var article = SafeFetcher.ParseArticle("""
            <html><head><meta property="article:published_time" content="2026-11-20T10:00:00Z"></head>
            <body><nav>unwanted</nav><article><h1>Announcement</h1>
            <p>ICEYE announced a new commercial radar imagery product.</p><script>bad()</script></article></body></html>
            """, "iceye", "https://iceye.com/news/one", "fallback", DateTime.UtcNow);
        var paragraphs = Wire.Decode<Paragraph[]>(article.ParagraphsJson);
        Assert.Single(paragraphs);
        Assert.DoesNotContain("bad", paragraphs[0].Text);
        Assert.Equal("instant", article.DatePrecision);
        Assert.NotNull(article.PublishedUtc);
    }

    [Fact]
    public async Task Unknown_paid_call_outcome_requires_explicit_reconciliation_not_automatic_replay()
    {
        using var factory = new DeskFactory("live");
        _ = factory.CreateClient();
        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<DeskDb>>();
        var ledger = new AiLedger(dbFactory, Options.Create(new DeskOptions
        { Mode = "live", EnableLiveAi = true, DailyBudgetUsd = 10 }), TimeProvider.System);
        var calls = 0;
        Task<int> Call(CancellationToken _) { calls++; throw new TimeoutException("Fixture timeout, not a real provider."); }
        await Assert.ThrowsAsync<TimeoutException>(() => ledger.ExecuteAsync("test", "unknown", "model", new { input = 1 }, .1m, Call, Ct));
        var error = await Assert.ThrowsAsync<ApiException>(() => ledger.ExecuteAsync("test", "unknown", "model", new { input = 1 }, .1m, Call, Ct));
        Assert.Equal(409, error.Status);
        Assert.Equal(1, calls);
        await using var db = await dbFactory.CreateDbContextAsync(Ct);
        Assert.Equal("failed-or-unknown", (await db.AiExecutions.SingleAsync(Ct)).Status);
    }

    [Fact]
    public async Task Dataset_mode_cannot_be_changed_in_place()
    {
        using var factory = new DeskFactory();
        _ = factory.CreateClient();
        await using var db = await factory.Services.GetRequiredService<IDbContextFactory<DeskDb>>().CreateDbContextAsync(Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SeedData.InitializeAsync(db, "live", Ct));
    }

    [Fact]
    public async Task Review_and_publication_commit_together_and_cannot_be_decided_twice()
    {
        using var factory = new DeskFactory("live");
        _ = factory.CreateClient();
        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<DeskDb>>();
        var publication = factory.Services.GetRequiredService<PublicationService>();
        var document = new DocumentRow
        {
            Id = "review-doc",
            SourceId = "iceye",
            CanonicalUrl = "https://iceye.com/news/review",
            Title = "Reviewed financing",
            PublishedUtc = DateTime.UtcNow.AddHours(-1),
            FetchedUtc = DateTime.UtcNow,
            ContentHash = "review-hash",
            ParagraphsJson = Wire.Encode(new[] { new Paragraph("p1", "ICEYE reports funding of USD 100 million.") })
        };
        await using (var db = await dbFactory.CreateDbContextAsync(Ct))
        {
            db.Documents.Add(document);
            await db.SaveChangesAsync(Ct);
        }
        var proposal = new PublicationProposal(
            new("ICEYE financing", "ICEYE reports funding of USD 100 million.", "iceye", ["p1"],
                [new("financial", "Funding", "USD 100 million", "USD", "Reported financing", "p1", "funding of USD 100 million")]),
            new(.99, .99, "Market & Policy", 2, new() { ["iceye"] = .99 }));
        await publication.ReviewAsync(document, "Financial review required.", proposal, Ct);
        await publication.PublishAsync(document.Id, proposal, true, Ct, $"review-{document.Id}", "reviewer", "Source reviewed.");
        await using (var db = await dbFactory.CreateDbContextAsync(Ct))
        {
            Assert.Equal("accepted", (await db.Reviews.SingleAsync(Ct)).State);
            Assert.Single(await db.Signals.ToArrayAsync(Ct));
            Assert.Single(await db.Facts.ToArrayAsync(Ct));
        }
        var repeated = await Assert.ThrowsAsync<ApiException>(() =>
            publication.PublishAsync(document.Id, proposal, true, Ct, $"review-{document.Id}", "reviewer", "Duplicate."));
        Assert.Equal(409, repeated.Status);
    }

    [Fact]
    public void Xml_external_entities_are_prohibited()
    {
        Assert.Throws<System.Xml.XmlException>(() => SafeFetcher.ParseFeed(
            "<!DOCTYPE rss [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]><rss><item><title>&xxe;</title></item></rss>"));
        var feed = SafeFetcher.ParseFeed("<rss><channel><item><title>A</title><link>https://example.com/a</link><description>Excerpt</description></item></channel></rss>");
        Assert.Single(feed);
        Assert.Equal("Excerpt", feed[0].Snippet);
    }

    [Theory]
    [InlineData(.8, .8, .8, "candidate")]
    [InlineData(.5, .8, .8, "review")]
    [InlineData(.1, .8, .8, "irrelevant")]
    [InlineData(.9, .1, .9, "irrelevant")]
    [InlineData(.9, .9, .4, "review")]
    public void Decision_truth_table_defines_uncertain_outcomes(double relevance, double fits, double company, string expected)
    {
        var result = new EvidenceAssessment(relevance, fits, "RF Analytics", 2, new() { ["unseenlabs"] = company });
        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public void Missing_model_judgments_are_errors_not_negative_answers()
    {
        Assert.Throws<ApiException>(() => TypeSafeDecisionService.Probability(new("test", []), "missing"));
        using var json = JsonDocument.Parse("{\"noul\":1.1}");
        Assert.Throws<ApiException>(() => TypeSafeDecisionService.Probability(new("test", new() { ["bad"] = json.RootElement }), "bad"));
    }

    [Fact]
    public async Task Typed_workflow_publishes_grounded_evidence_idempotently_without_network()
    {
        using var factory = new DeskFactory("live");
        _ = factory.CreateClient();
        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<DeskDb>>();
        var catalog = factory.Services.GetRequiredService<CatalogStore>();
        var publication = factory.Services.GetRequiredService<PublicationService>();
        var document = new DocumentRow
        {
            Id = "doc-test",
            SourceId = "iceye",
            CanonicalUrl = "https://iceye.com/news/test",
            Title = "Test announcement",
            PublishedUtc = DateTime.UtcNow.AddHours(-1),
            FetchedUtc = DateTime.UtcNow,
            ContentHash = "test-hash",
            ParagraphsJson = Wire.Encode(new[] { new Paragraph("p1", "ICEYE announced a commercial imagery product.") })
        };
        await using (var db = await dbFactory.CreateDbContextAsync(Ct))
        {
            db.Documents.Add(document);
            await db.SaveChangesAsync(Ct);
        }
        var orchestrator = new IntelligenceOrchestrator(catalog, new FakeDecisions(), new FakeModel(), publication, dbFactory);
        await orchestrator.ProcessAsync(document, Ct);
        await orchestrator.ProcessAsync(document, Ct);
        var result = await catalog.LoadAsync(Ct);
        Assert.Single(result.Records);
        Assert.False(result.Records[0].Sample);
        Assert.Equal("iceye", result.Records[0].CompetitorId);
        Assert.Single(result.Competitors.Single(x => x.Id == "iceye").Activity);
    }

    [Fact]
    public async Task Ledger_reserves_before_dispatch_and_refuses_budget_overrun()
    {
        using var factory = new DeskFactory("live");
        _ = factory.CreateClient();
        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<DeskDb>>();
        var ledger = new AiLedger(dbFactory, Options.Create(new DeskOptions
        { Mode = "live", EnableLiveAi = true, DailyBudgetUsd = .15m }), TimeProvider.System);
        var calls = 0;
        Task<int> Call(CancellationToken _) { calls++; return Task.FromResult(42); }
        Assert.Equal(42, await ledger.ExecuteAsync("test", "operation", "model", new { input = 1 }, .1m, Call, Ct));
        Assert.Equal(42, await ledger.ExecuteAsync("test", "operation", "model", new { input = 1 }, .1m, Call, Ct));
        Assert.Equal(1, calls);
        var error = await Assert.ThrowsAsync<ApiException>(() =>
            ledger.ExecuteAsync("test", "operation", "model", new { input = 2 }, .1m, Call, Ct));
        Assert.Equal(429, error.Status);
        Assert.Equal(1, calls);
    }

    private sealed class FakeDecisions : IDecisionService
    {
        public Task<EvidenceAssessment> AssessAsync(Paragraph[] evidence, Competitor[] companies, CancellationToken ct) =>
            Task.FromResult(new EvidenceAssessment(.99, .99, "Earth Observation", 2,
                companies.ToDictionary(c => c.Id, c => c.Id == "iceye" ? .99 : .01)));
        public Task<bool> SupportsAsync(object evidence, object claims, CancellationToken ct) => Task.FromResult(true);
    }
    private sealed class FakeModel : IIntelligenceModel
    {
        public Task<ResearchQueries> PlanAsync(string publisher, string[] topics, string[] companies, CancellationToken ct) =>
            Task.FromResult(new ResearchQueries(["ICEYE imagery"]));
        public Task<SignalDraft> TransformAsync(DocumentRow document, Competitor[] companies, CancellationToken ct) =>
            Task.FromResult(new SignalDraft("ICEYE announces imagery product", "ICEYE announced a commercial imagery product.", "iceye", ["p1"], []));
        public Task<BriefDraft> BriefAsync(BriefInput input, CancellationToken ct) =>
            Task.FromResult(new BriefDraft("Test", $"Evidence [{input.Records[0].Id}]", [input.Records[0].Id]));
    }
}
