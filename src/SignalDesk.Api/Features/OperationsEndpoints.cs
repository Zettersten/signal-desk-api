using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

public sealed class AiServicesEndpoint(AiServiceCatalog catalog) : EndpointWithoutRequest<AiServiceDescriptor[]>
{
    public override void Configure() { Get("/v1/ai/services"); AllowAnonymous(); }
    public override Task HandleAsync(CancellationToken ct) => Send.OkAsync(catalog.List(), ct);
}

public sealed record SourceInfo(string Id, string PublisherId, string Url, string Kind, bool Approved,
    bool AllowArticleCapture, string Status, DateTime? LastAttemptUtc, DateTime? LastSuccessUtc, DateTime NextDueUtc);

public sealed class SourcesEndpoint(IDbContextFactory<DeskDb> factory) : EndpointWithoutRequest<SourceInfo[]>
{
    public override void Configure() { Get("/v1/sources"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await Send.OkAsync(await db.Sources.OrderBy(x => x.Id).Select(x => new SourceInfo(
            x.Id, x.PublisherId, x.Url, x.Kind, x.Approved, x.AllowArticleCapture, x.Status,
            x.LastAttemptUtc, x.LastSuccessUtc, x.NextDueUtc)).ToArrayAsync(ct), ct);
    }
}

public sealed class ApproveSourceRequest
{
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
    public string Kind { get; set; } = "search";
    public bool Approved { get; set; }
    public bool AllowArticleCapture { get; set; }
    public string RightsAcknowledgement { get; set; } = "";
}
public sealed class ApproveSourceEndpoint(IDbContextFactory<DeskDb> factory) : Endpoint<ApproveSourceRequest>
{
    public override void Configure() { Patch("/v1/sources/{id}"); AllowAnonymous(); }
    public override async Task HandleAsync(ApproveSourceRequest req, CancellationToken ct)
    {
        if (req.Kind is not ("search" or "rss") || req.RightsAcknowledgement.Trim().Length < 20)
            throw new ApiException(400, "Choose search/rss and record the source access/redistribution approval.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var source = await db.Sources.Include(x => x.Publisher).SingleOrDefaultAsync(x => x.Id == req.Id, ct)
            ?? throw new ApiException(404, "Source not found.");
        SafeFetcher.Validate(req.Url, source.Publisher.Website);
        source.Url = req.Url; source.Kind = req.Kind; source.Approved = req.Approved;
        source.AllowArticleCapture = req.AllowArticleCapture && req.Approved;
        source.RightsAcknowledgement = req.RightsAcknowledgement;
        source.Status = req.Approved ? "approved" : "disabled";
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
public sealed class CreateRunRequest { public string SourceId { get; set; } = ""; }
public sealed class CreateRunEndpoint(RunQueue queue) : Endpoint<CreateRunRequest, CollectionRun>
{
    public override void Configure()
    {
        Post("/v1/ingestion/runs");
        AllowAnonymous();
        Description(b => b.Produces<CollectionRun>(202).ProducesProblem(400).ProducesProblem(503));
    }
    public override async Task HandleAsync(CreateRunRequest req, CancellationToken ct)
    {
        var run = await queue.EnqueueAsync(req.SourceId, ct);
        HttpContext.Response.Headers.Location = $"/v1/ingestion/runs/{run.Id}";
        await Send.ResponseAsync(run, 202, ct);
    }
}
public sealed class RunIdRequest { public Guid Id { get; set; } }
public sealed class RunEndpoint(IDbContextFactory<DeskDb> factory) : Endpoint<RunIdRequest, CollectionRun>
{
    public override void Configure() { Get("/v1/ingestion/runs/{id}"); AllowAnonymous(); }
    public override async Task HandleAsync(RunIdRequest req, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await Send.OkAsync(await db.Runs.FindAsync([req.Id], ct) ?? throw new ApiException(404, "Run not found."), ct);
    }
}
public sealed class RetryRunEndpoint(IDbContextFactory<DeskDb> factory, RunQueue queue) : Endpoint<RunIdRequest, CollectionRun>
{
    public override void Configure() { Post("/v1/ingestion/runs/{id}/retry"); AllowAnonymous(); }
    public override async Task HandleAsync(RunIdRequest req, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var run = await db.Runs.FindAsync([req.Id], ct) ?? throw new ApiException(404, "Run not found.");
        if (run.State is not ("failed" or "enqueue-failed")) throw new ApiException(409, "Only a terminal failed run can be retried.");
        await Send.ResponseAsync(await queue.EnqueueAsync(run.SourceId, ct), 202, ct);
    }
}
public sealed record ReviewInfo(string Id, string DocumentId, string Reason, string State,
    string? ProposalJson, string EvidenceJson, string SourceUrl, DateTime? PublishedUtc);
public sealed class ReviewsEndpoint(IDbContextFactory<DeskDb> factory) : EndpointWithoutRequest<ReviewInfo[]>
{
    public override void Configure() { Get("/v1/review/items"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await Send.OkAsync(await db.Reviews.Where(x => x.State == "pending").OrderBy(x => x.CreatedUtc).Take(50)
            .Select(x => new ReviewInfo(x.Id, x.DocumentId, x.Reason, x.State, x.ProposalJson, x.Document.ParagraphsJson,
                x.Document.CanonicalUrl, x.Document.PublishedUtc))
            .ToArrayAsync(ct), ct);
    }
}
public sealed class ReviewDecisionRequest
{
    public string Id { get; set; } = "";
    public bool Accept { get; set; }
    public string Note { get; set; } = "";
}
public sealed class ReviewDecisionEndpoint(IDbContextFactory<DeskDb> factory, PublicationService publication)
    : Endpoint<ReviewDecisionRequest>
{
    public override void Configure() { Post("/v1/review/items/{id}/decision"); AllowAnonymous(); }
    public override async Task HandleAsync(ReviewDecisionRequest req, CancellationToken ct)
    {
        if (req.Note.Trim().Length < 10) throw new ApiException(400, "Record a review rationale.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var review = await db.Reviews.FindAsync([req.Id], ct) ?? throw new ApiException(404, "Review not found.");
        if (!string.Equals(review.State, "pending")) throw new ApiException(409, "Review has already been decided.");
        if (req.Accept)
        {
            if (review.ProposalJson is null) throw new ApiException(400, "There is no valid proposal to accept.");
            await publication.PublishAsync(review.DocumentId, Wire.Decode<PublicationProposal>(review.ProposalJson), true, ct,
                review.Id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, req.Note);
            await Send.NoContentAsync(ct);
            return;
        }
        var reviewer = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        var changed = await db.Reviews.Where(x => x.Id == review.Id && x.State == "pending").ExecuteUpdateAsync(s =>
            s.SetProperty(x => x.State, "rejected").SetProperty(x => x.DecidedUtc, DateTime.UtcNow)
                .SetProperty(x => x.DecidedBy, reviewer).SetProperty(x => x.DecisionNote, req.Note), ct);
        if (changed != 1) throw new ApiException(409, "Review has already been decided.");
        await Send.NoContentAsync(ct);
    }
}
