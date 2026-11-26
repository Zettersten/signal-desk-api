using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalDesk.Api.Contracts;

public sealed record SourceSeed(string Publisher, string Website, string[] Categories);
