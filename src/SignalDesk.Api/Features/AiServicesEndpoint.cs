using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;
using SignalDesk.Api.Ingestion;

namespace SignalDesk.Api.Features;

public sealed class AiServicesEndpoint(AiServiceCatalog catalog) : EndpointWithoutRequest<AiServiceDescriptor[]>
{
    public override void Configure()
    {
        Get("/v1/ai/services");
        AllowAnonymous();
        Description(b => b.WithTags("AI").Produces<AiServiceDescriptor[]>(200));
        Summary(s =>
        {
            s.Summary = "List AI services";
            s.Description = """
                Lists the AI services the pipeline uses, with their model and status.

                - `configured`: the API key or model needed by this service is set.
                - `enabled`: live AI is turned on (`Desk:EnableLiveAi`).

                No provider calls are made by this endpoint. Keys are never returned.
                """;
            s.ResponseExamples[200] = new AiServiceDescriptor[]
            {
                new("perplexity-search", "perplexity", "Ranked domain-filtered source discovery", null, true, false)
            };
        });
    }
    public override Task HandleAsync(CancellationToken ct) => Send.OkAsync(catalog.List(), ct);
}
