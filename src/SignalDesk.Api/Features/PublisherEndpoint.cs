using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class PublisherEndpoint(CatalogStore store) : Endpoint<IdRequest, Publisher>
{
    public override void Configure()
    {
        Get("/v1/publishers/{id}");
        AllowAnonymous();
        Description(b => b.WithTags("Catalog").Produces<Publisher>(200).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Get one publisher";
            s.Description = "Returns a publisher by `id`, for example `spacenews`.";
            s.ResponseExamples[200] = new Publisher("spacenews", "SpaceNews", "S", "slate", "https://spacenews.com");
        });
    }
    public override async Task HandleAsync(IdRequest req, CancellationToken ct) =>
        await Send.OkAsync((await store.LoadAsync(ct)).Publishers.FirstOrDefault(x => string.Equals(x.Id, req.Id))
            ?? throw new ApiException(404, "Publisher not found."), ct);
}
