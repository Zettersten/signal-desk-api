using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class PublishersEndpoint(CatalogStore store) : EndpointWithoutRequest<Publisher[]>
{
    public override void Configure() { Get("/v1/publishers"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync((await store.LoadAsync(ct)).Publishers, ct);
}
