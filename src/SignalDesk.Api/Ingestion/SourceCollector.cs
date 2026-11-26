using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Ingestion;

public sealed class SourceCollector(
    IDbContextFactory<DeskDb> factory, ISearchService search, IIntelligenceModel model,
    SafeFetcher fetcher, IntelligenceOrchestrator orchestrator, PublicationService publication, TimeProvider clock)
{
    public async Task CollectAsync(Guid runId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var run = await db.Runs.SingleAsync(x => x.Id == runId, ct);
        var source = await db.Sources.Include(x => x.Publisher).SingleAsync(x => x.Id == run.SourceId, ct);
        if (!source.Approved) throw new ApiException(400, "Source requires rights and access approval.");
        run.State = "running";
        source.LastAttemptUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        var pending = await db.Documents.Where(x => x.SourceId == source.Id && x.State == "captured").ToArrayAsync(ct);
        foreach (var doc in pending) await EvaluateAsync(doc, ct);
        var results = await DiscoverAsync(runId, source, db, ct);
        foreach (var hit in results.Results.Take(50))
        {
            ct.ThrowIfCancellationRequested();
            await ProcessHitAsync(hit, results.Id, run, source, db, ct);
        }
        run.Published = await db.Documents.CountAsync(x => x.SourceId == source.Id && x.State == "published", ct);
        run.Reviewed = await db.Documents.CountAsync(x => x.SourceId == source.Id && x.State == "review", ct);
        run.State = "succeeded";
        run.FinishedUtc = clock.GetUtcNow().UtcDateTime;
        source.LastSuccessUtc = run.FinishedUtc;
        source.NextDueUtc = run.FinishedUtc.Value.AddHours(string.Equals(source.Kind, "rss") ? 1 : 24);
        source.Status = results.Results.Length == 0 ? "searched-no-results" : source.AllowArticleCapture ? "collected" : "indexed-only";
        await db.SaveChangesAsync(ct);
    }

    private async Task<SearchResult> DiscoverAsync(Guid runId, SourceRow source, DeskDb db, CancellationToken ct)
    {
        if (string.Equals(source.Kind, "rss"))
        {
            var page = await fetcher.FetchAsync(source.Url, source.Publisher.Website, ct);
            return new($"feed-{runId}", SafeFetcher.ParseFeed(page.Text));
        }
        var companies = await db.Competitors.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync(ct);
        var queries = await model.PlanAsync(source.Publisher.Name, Wire.Decode<string[]>(source.Publisher.TopicsJson), companies, ct);
        var since = (source.LastSuccessUtc ?? clock.GetUtcNow().UtcDateTime.AddDays(-5)).AddDays(-2);
        return await search.SearchAsync(queries.Queries, [new Uri(source.Publisher.Website).IdnHost], since, ct);
    }

    private async Task ProcessHitAsync(SearchHit hit, string requestId, CollectionRun run, SourceRow source,
        DeskDb db, CancellationToken ct)
    {
        Uri uri;
        try { uri = SafeFetcher.Validate(hit.Url, source.Publisher.Website); }
        catch (ApiException) { return; }
        var canonical = new UriBuilder(uri) { Fragment = "" }.Uri.AbsoluteUri;
        var hitId = CatalogStore.Hash(source.Id + canonical);
        var stored = await db.SearchHits.FindAsync([hitId], ct);
        if (stored is not null && string.Equals(stored.State, "processed") &&
            stored.LastCheckedUtc >= clock.GetUtcNow().UtcDateTime.AddHours(-6)) return;
        if (stored is null)
        {
            stored = new()
            {
                Id = hitId,
                SourceId = source.Id,
                Url = canonical,
                Title = hit.Title,
                Snippet = hit.Snippet,
                Date = hit.Date,
                RequestId = requestId,
                DiscoveredUtc = clock.GetUtcNow().UtcDateTime
            };
            db.SearchHits.Add(stored);
        }
        run.Discovered++;
        await db.SaveChangesAsync(ct);
        if (!source.AllowArticleCapture)
        {
            stored.State = "indexed-only";
            await db.SaveChangesAsync(ct);
            return;
        }
        FetchedPage content;
        try { content = await fetcher.FetchAsync(canonical, source.Publisher.Website, ct); }
        catch (ApiException error) when (error.Status is 400 or 422)
        {
            stored.State = "quarantined";
            stored.LastCheckedUtc = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            return;
        }
        if (!content.MediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            stored.State = "unsupported-content";
            await db.SaveChangesAsync(ct);
            return;
        }
        var document = SafeFetcher.ParseArticle(content.Text, source.Id, canonical, hit.Title, clock.GetUtcNow().UtcDateTime);
        var existing = await db.Documents.FindAsync([document.Id], ct);
        if (existing is null)
        {
            db.Documents.Add(document);
            await db.SaveChangesAsync(ct);
            await EvaluateAsync(document, ct);
        }
        else if (string.Equals(existing.State, "captured")) await EvaluateAsync(existing, ct);
        stored.State = "processed";
        stored.LastCheckedUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    private async Task EvaluateAsync(DocumentRow document, CancellationToken ct)
    {
        try { await orchestrator.ProcessAsync(document, ct); }
        catch (ApiException error) when (error.Status is 400 or 409 or 422)
        {
            await publication.ReviewAsync(document, error.Status == 409
                ? "A provider execution needs reconciliation before this evidence can be retried."
                : "Evidence exceeds validation/context limits; narrow or correct it before retry.", null, ct);
        }
    }
}

public sealed class RunQueue(IDbContextFactory<DeskDb> factory, IOptions<DeskOptions> options, TimeProvider clock)
{
    private readonly SemaphoreSlim gate = new(1);

    public async Task<CollectionRun> EnqueueAsync(string sourceId, CancellationToken ct)
    {
        if (!options.Value.EnableCollectors || !options.Value.EnableLiveAi || !string.Equals(options.Value.Mode, "live"))
            throw new ApiException(503, "Enable the approved live collection workflow before queueing work.");
        await gate.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var source = await db.Sources.SingleOrDefaultAsync(x => x.Id == sourceId, ct);
            if (source?.Approved != true)
                throw new ApiException(400, "Choose an approved source.");
            var active = await db.Runs.FirstOrDefaultAsync(x => x.SourceId == sourceId &&
                (x.State == "queued" || x.State == "running" || x.State == "retry" || x.State == "deferred"), ct);
            if (active is not null)
            {
                if (active.JobTrackingId is null)
                {
                    var payload = Wire.Encode(new CollectSource(active.Id));
                    var persisted = await db.Jobs.FirstOrDefaultAsync(x => x.PayloadJson == payload, ct);
                    active.JobTrackingId = persisted?.TrackingID ??
                        await new CollectSource(active.Id).QueueJobAsync(expireOn: DateTime.UtcNow.AddDays(7));
                    await db.SaveChangesAsync(ct);
                }
                return active;
            }
            var run = new CollectionRun { SourceId = sourceId, CreatedUtc = clock.GetUtcNow().UtcDateTime };
            source.NextDueUtc = clock.GetUtcNow().UtcDateTime.AddHours(string.Equals(source.Kind, "rss") ? 1 : 24);
            db.Runs.Add(run);
            await db.SaveChangesAsync(ct);
            try
            {
                run.JobTrackingId = await new CollectSource(run.Id).QueueJobAsync(expireOn: DateTime.UtcNow.AddDays(7));
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                run.State = "enqueue-failed";
                await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            return run;
        }
        finally { gate.Release(); }
    }
}

public sealed class CollectionScheduler(
    IDbContextFactory<DeskDb> factory, RunQueue queue, IOptions<DeskOptions> options, ILogger<CollectionScheduler> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EnableCollectors) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync(stoppingToken);
                var due = await db.Sources.Where(x => x.Approved && x.NextDueUtc <= DateTime.UtcNow)
                    .OrderBy(x => x.NextDueUtc).Take(3).Select(x => x.Id).ToArrayAsync(stoppingToken);
                foreach (var id in due) await queue.EnqueueAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Collection scheduling deferred: {Type}", error.GetType().Name); }
        }
    }
}
