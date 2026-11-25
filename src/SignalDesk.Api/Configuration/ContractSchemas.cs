using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Features;

namespace SignalDesk.Api.Configuration;

public static class ContractSchemas
{
    public static Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        if (schema.Properties is null) return Task.CompletedTask;
        void Enum(string property, string[] values)
        {
            if (schema.Properties.ContainsKey(property))
                schema.Properties[property] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = values.Select(v => (JsonNode)JsonValue.Create(v)!).ToList()
                };
        }
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(Signal) || type == typeof(Capability)) Enum("category", Wire.Categories);
        if (type == typeof(Capability)) Enum("status", ["Core offering", "Adjacent offering", "Watch"]);
        if (type == typeof(Catalog)) Enum("mode", ["sample", "live"]);
        if (type == typeof(BriefInput)) { Enum("mode", BriefEndpoint.Modes); Enum("audience", BriefEndpoint.Audiences); }
        if (type == typeof(Competitor) || type == typeof(Publisher)) Enum("tone", Wire.Tones);
        return Task.CompletedTask;
    }
}
