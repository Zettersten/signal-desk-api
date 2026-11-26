using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

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
