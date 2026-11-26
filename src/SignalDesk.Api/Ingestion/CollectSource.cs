using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SignalDesk.Api.Data;
using SignalDesk.Api.Configuration;

namespace SignalDesk.Api.Ingestion;

public sealed record CollectSource(Guid RunId) : ICommand;
