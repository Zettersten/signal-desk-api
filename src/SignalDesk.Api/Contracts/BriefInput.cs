using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalDesk.Api.Contracts;

public sealed record BriefInput(Signal[] Records, string Objective, string Audience, string Mode = "Strategic brief");
