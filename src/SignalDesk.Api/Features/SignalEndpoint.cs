using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class SignalEndpoint(CatalogStore store) : Endpoint<IdRequest, Signal>
{
    public override void Configure() { Get("/v1/signals/{id}"); AllowAnonymous(); }
    public override async Task HandleAsync(IdRequest req, CancellationToken ct) =>
        await Send.OkAsync((await store.LoadAsync(ct)).Records.FirstOrDefault(x => string.Equals(x.Id, req.Id))
            ?? throw new ApiException(404, "Record not found."), ct);
}
