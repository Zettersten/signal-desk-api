using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalDesk.Api.Contracts;

public sealed record Financial(string Label, string Detail, string? Value = null, string? Period = null);
