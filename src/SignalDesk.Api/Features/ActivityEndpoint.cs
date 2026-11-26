using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class ActivityEndpoint(CatalogStore store) : Endpoint<IdRequest, ProfileActivity[]>
{
    public override void Configure() { Get("/v1/competitors/{id}/activity"); AllowAnonymous(); }
    public override async Task HandleAsync(IdRequest req, CancellationToken ct) =>
        await Send.OkAsync(((await store.LoadAsync(ct)).Competitors.FirstOrDefault(x => string.Equals(x.Id, req.Id))
            ?? throw new ApiException(404, "Competitor not found.")).Activity, ct);
}
