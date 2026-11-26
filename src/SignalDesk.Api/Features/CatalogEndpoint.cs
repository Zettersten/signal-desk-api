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
        Summary(s => s.Summary = "Complete frontend-compatible intelligence catalog; never triggers collection.");
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
