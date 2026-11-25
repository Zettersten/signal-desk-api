using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalDesk.Api.Contracts;

public static class Wire
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static T Decode<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)
        ?? throw new InvalidDataException("Invalid stored data.");
    public static readonly string[] Categories =
    [
        "RF Analytics", "Spectrum & GNSS", "Earth Observation", "Maritime Analytics",
        "Space Domain Awareness", "Space Infrastructure", "Market & Policy"
    ];
    public static readonly string[] Tones = ["mint", "plum", "blue", "sky", "gold"];
}

public sealed record Paragraph(string Id, string Text);
public sealed record Signal(
    string Id, string CompetitorId, string PublisherId, string Company, string Publisher,
    string Title, string Summary, Paragraph[] Paragraphs, string PublishedAt,
    string Category, int Importance, string Time, string Mark, string Tone, bool Sample, string? Url = null);
public sealed record ProfileImage(string Src, string Alt, string Caption);
public sealed record Incorporation(string Date, string? Detail = null, string? Source = null, bool? Sample = null);
public sealed record Capability(string Name, string Category, string Status, string Description);
public sealed record Financial(string Label, string Detail, string? Value = null, string? Period = null);
public sealed record ProfileActivity(string Date, string Title, string Details, string? RecordId = null);
public sealed record Competitor(
    string Id, string Name, string Mark, string Tone, string Overview, ProfileImage Image,
    Capability[] Capabilities, Financial[] Financials, ProfileActivity[] Activity,
    string? Website = null, string? Logo = null, string? LogoKind = null,
    string? FoundedYear = null, Incorporation? Incorporated = null);
public sealed record Publisher(
    string Id, string Name, string Mark, string Tone, string Website,
    string? Logo = null, string? LogoKind = null);
public sealed record Catalog(Signal[] Records, Competitor[] Competitors, Publisher[] Publishers, string Mode);
public sealed record Brief(string Title, string Text);
public sealed record BriefInput(Signal[] Records, string Objective, string Audience, string Mode = "Strategic brief");
public sealed record SourceSeed(string Publisher, string Website, string[] Categories);
