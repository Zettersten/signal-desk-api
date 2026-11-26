using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalDesk.Api.Contracts;

public sealed record Signal(
    string Id, string CompetitorId, string PublisherId, string Company, string Publisher,
    string Title, string Summary, Paragraph[] Paragraphs, string PublishedAt,
    string Category, int Importance, string Time, string Mark, string Tone, bool Sample, string? Url = null);
