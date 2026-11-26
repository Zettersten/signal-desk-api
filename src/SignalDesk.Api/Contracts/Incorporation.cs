using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalDesk.Api.Contracts;

public sealed record Incorporation(string Date, string? Detail = null, string? Source = null, bool? Sample = null);
