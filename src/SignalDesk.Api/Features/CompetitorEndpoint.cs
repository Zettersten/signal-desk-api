using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class CompetitorEndpoint(CatalogStore store) : Endpoint<IdRequest, Competitor>
{
    public override void Configure()
    {
        Get("/v1/competitors/{id}");
        AllowAnonymous();
        Description(b => b.WithTags("Catalog").Produces<Competitor>(200).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Get one competitor";
            s.Description = "Returns a competitor profile by `id` (for example `blacksky`). Activity is also available separately at `GET /v1/competitors/{id}/activity`.";
        });
    }
    public override async Task HandleAsync(IdRequest req, CancellationToken ct) =>
        await Send.OkAsync((await store.LoadAsync(ct)).Competitors.FirstOrDefault(x => string.Equals(x.Id, req.Id))
            ?? throw new ApiException(404, "Competitor not found."), ct);
}
