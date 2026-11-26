using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class ActivityEndpoint(CatalogStore store) : Endpoint<IdRequest, ProfileActivity[]>
{
    public override void Configure()
    {
        Get("/v1/competitors/{id}/activity");
        AllowAnonymous();
        Description(b => b.WithTags("Catalog").Produces<ProfileActivity[]>(200).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "List a competitor's activity";
            s.Description = "Returns a competitor's dated activity timeline, newest first. Each entry can link to a signal through `recordId`.";
            s.ResponseExamples[200] = new ProfileActivity[]
            {
                new("2026-11-24", "Acquisition", "Acquired geospatial data firm EpochGeo.", "live-doc-a03c4459a8270a45d6c165f0a5cfe336b8cbccd3d34a3e77a1becd2ae9af1773")
            };
        });
    }
    public override async Task HandleAsync(IdRequest req, CancellationToken ct) =>
        await Send.OkAsync(((await store.LoadAsync(ct)).Competitors.FirstOrDefault(x => string.Equals(x.Id, req.Id))
            ?? throw new ApiException(404, "Competitor not found.")).Activity, ct);
}
