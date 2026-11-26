using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalDesk.Api.Contracts;

public sealed record Catalog(Signal[] Records, Competitor[] Competitors, Publisher[] Publishers, string Mode);
