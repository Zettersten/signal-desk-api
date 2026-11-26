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
