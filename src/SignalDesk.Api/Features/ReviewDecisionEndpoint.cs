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
    public override void Configure()
    {
        Post("/v1/review/items/{id}/decision");
        AllowAnonymous();
        Description(b => b.WithTags("Review").ProducesProblem(400).ProducesProblem(404).ProducesProblem(409));
        Summary(s =>
        {
            s.Summary = "Accept or reject a review item";
            s.Description = """
                Records your decision on one pending item.

                - `accept: true` publishes the proposed signal and its facts. Accepting is atomic: the signal, the facts, and the audit record are saved together, or none are.
                - `accept: false` rejects the item. Nothing is published.

                **Rules**
                - `note` is required (at least 10 characters). Write the reason, because it is kept in the audit trail.
                - An item can be decided only once. A second decision returns `409`.
                - Accepting an item with no valid proposal returns `400`.

                **Responses:** `204` on success, `400`, `404` if the item does not exist, `409` if it was already decided.
                """;
            s.ExampleRequest = new ReviewDecisionRequest
            {
                Id = "review-doc-ca5b93c3020e032cd315b5840272fc7a2f7603d06f2c7ec3e28b433d0c4249bf",
                Accept = false,
                Note = "Evidence is site navigation and a paywall message. It does not describe a company development."
            };
        });
    }
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
