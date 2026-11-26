using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class CompetitorsEndpoint(CatalogStore store) : EndpointWithoutRequest<Competitor[]>
{
    public override void Configure() { Get("/v1/competitors"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync((await store.LoadAsync(ct)).Competitors, ct);
}
