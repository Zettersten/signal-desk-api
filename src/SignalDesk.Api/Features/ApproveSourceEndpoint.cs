using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

public sealed class ApproveSourceEndpoint(IDbContextFactory<DeskDb> factory) : Endpoint<ApproveSourceRequest>
{
    public override void Configure() { Patch("/v1/sources/{id}"); AllowAnonymous(); }
    public override async Task HandleAsync(ApproveSourceRequest req, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var source = await db.Sources.Include(x => x.Publisher).SingleOrDefaultAsync(x => x.Id == req.Id, ct);
        if (source is null) { await Send.NotFoundAsync(ct); return; }
        SafeFetcher.Validate(req.Url, source.Publisher.Website);
        source.Url = req.Url; source.Kind = req.Kind; source.Approved = req.Approved;
        source.AllowArticleCapture = req.AllowArticleCapture && req.Approved;
        source.RightsAcknowledgement = req.RightsAcknowledgement;
        source.Status = req.Approved ? "approved" : "disabled";
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
