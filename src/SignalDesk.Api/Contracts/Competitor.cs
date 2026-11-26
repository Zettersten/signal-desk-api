using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalDesk.Api.Contracts;

public sealed record Competitor(
    string Id, string Name, string Mark, string Tone, string Overview, ProfileImage Image,
    Capability[] Capabilities, Financial[] Financials, ProfileActivity[] Activity,
    string? Website = null, string? Logo = null, string? LogoKind = null,
    string? FoundedYear = null, Incorporation? Incorporated = null);
