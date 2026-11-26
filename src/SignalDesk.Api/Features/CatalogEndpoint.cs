using System.Globalization;
using FastEndpoints;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Features;

public sealed class CatalogEndpoint(CatalogStore store) : EndpointWithoutRequest<Catalog>
{
    public override void Configure()
    {
        Get("/v1/catalog");
        AllowAnonymous();
        Description(b => b.WithTags("Catalog").Produces<Catalog>(200));
        Summary(s =>
        {
            s.Summary = "Get the full catalog";
            s.Description = """
                Returns every record, competitor, and publisher in one response, plus the current `mode` (`sample` or `live`).

                This endpoint only reads stored data. It never starts collection and never makes paid AI calls.

                **Caching:** the response includes an `ETag`. Send it back in `If-None-Match` to receive `304 Not Modified` when nothing has changed.

                **Typical use:** load this when the app starts, then re-request with `If-None-Match` on refresh.
                """;
            s.ResponseExamples[200] = new Catalog([], [], [new Publisher("blacksky", "BlackSky", "B", "blue", "https://blacksky.com")], "sample");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var catalog = await store.LoadAsync(ct);
        var etag = $"\"{CatalogStore.Hash(Wire.Encode(catalog))}\"";
        HttpContext.Response.Headers.ETag = etag;
        HttpContext.Response.Headers.CacheControl = "private, no-cache";
        if (HttpContext.Request.Headers.IfNoneMatch.Any(v => string.Equals(v, etag)))
        {
            await Send.ResultAsync(Results.StatusCode(304));
            return;
        }
        await Send.OkAsync(catalog, ct);
    }
}
