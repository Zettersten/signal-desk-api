using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalDesk.Api.Contracts;

public sealed record ProfileActivity(string Date, string Title, string Details, string? RecordId = null);
