using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

public sealed class ReviewDecisionEndpoint(IDbContextFactory<DeskDb> factory, PublicationService publication)
    : Endpoint<ReviewDecisionRequest>
{
    public override void Configure() { Post("/v1/review/items/{id}/decision"); AllowAnonymous(); }
    public override async Task HandleAsync(ReviewDecisionRequest req, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var review = await db.Reviews.FindAsync([req.Id], ct);
        if (review is null) { await Send.NotFoundAsync(ct); return; }
        if (!string.Equals(review.State, "pending")) { await Send.ResultAsync(TypedResults.Conflict()); return; }
        if (req.Accept)
        {
            if (review.ProposalJson is null) ThrowError("There is no valid proposal to accept.");
            await publication.PublishAsync(review.DocumentId, Wire.Decode<PublicationProposal>(review.ProposalJson), true, ct,
                review.Id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, req.Note);
            await Send.NoContentAsync(ct);
            return;
        }
        var reviewer = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        var changed = await db.Reviews.Where(x => x.Id == review.Id && x.State == "pending").ExecuteUpdateAsync(s =>
            s.SetProperty(x => x.State, "rejected").SetProperty(x => x.DecidedUtc, DateTime.UtcNow)
                .SetProperty(x => x.DecidedBy, reviewer).SetProperty(x => x.DecisionNote, req.Note), ct);
        if (changed != 1) { await Send.ResultAsync(TypedResults.Conflict()); return; }
        await Send.NoContentAsync(ct);
    }
}
