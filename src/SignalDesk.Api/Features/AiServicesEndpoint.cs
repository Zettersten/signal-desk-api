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
    public override void Configure() { Get("/v1/ai/services"); AllowAnonymous(); }
    public override Task HandleAsync(CancellationToken ct) => Send.OkAsync(catalog.List(), ct);
}
