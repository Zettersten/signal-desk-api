using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class PublishersEndpoint(CatalogStore store) : EndpointWithoutRequest<Publisher[]>
{
    public override void Configure()
    {
        Get("/v1/publishers");
        AllowAnonymous();
        Description(b => b.WithTags("Catalog").Produces<Publisher[]>(200));
        Summary(s =>
        {
            s.Summary = "List publishers";
            s.Description = "Returns the publishers that signals come from, such as SpaceNews or Reuters. Use a publisher `id` to filter `GET /v1/signals`.";
        });
    }
    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync((await store.LoadAsync(ct)).Publishers, ct);
}
