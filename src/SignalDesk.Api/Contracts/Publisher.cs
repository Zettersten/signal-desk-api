using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalDesk.Api.Contracts;

public sealed record Publisher(
    string Id, string Name, string Mark, string Tone, string Website,
    string? Logo = null, string? LogoKind = null);
