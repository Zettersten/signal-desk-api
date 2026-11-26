using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class CompetitorsEndpoint(CatalogStore store) : EndpointWithoutRequest<Competitor[]>
{
    public override void Configure()
    {
        Get("/v1/competitors");
        AllowAnonymous();
        Description(b => b.WithTags("Catalog").Produces<Competitor[]>(200));
        Summary(s =>
        {
            s.Summary = "List competitors";
            s.Description = "Returns every tracked competitor with its profile, capabilities, financials, and activity. Use `GET /v1/competitors/{id}` for one profile.";
        });
    }
    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync((await store.LoadAsync(ct)).Competitors, ct);
}
