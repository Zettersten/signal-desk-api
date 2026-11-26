using Microsoft.Extensions.Options;
using SignalDesk.Api.Configuration;

namespace SignalDesk.Api.AI;

public sealed record AiServiceDescriptor(string Id, string Provider, string Responsibility, string? Model, bool Configured, bool Enabled);
