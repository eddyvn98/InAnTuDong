using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PrintAI.Domain;

namespace PrintAI.Planning;

internal static class AntigravityPlannerSchema
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Resolve(string instruction)
    {
        var payloadName = IsGeneralPlan(instruction) ? "plan" : IsJob(instruction) ? "job" : null;
        if (payloadName is null)
            return "{\"type\":\"object\",\"additionalProperties\":false}";

        var payloadType = payloadName == "plan" ? typeof(PrintPlan) : typeof(PrintJobSpec);
        var payloadSchema = JsonSchemaExporter.GetJsonSchemaAsNode(Options, payloadType);
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                [payloadName] = payloadSchema,
                ["confidence"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                ["questions"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
                ["warnings"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } }
            },
            ["required"] = new JsonArray(payloadName, "confidence", "questions", "warnings"),
            ["additionalProperties"] = false
        };

        return schema.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    public static bool IsGeneralPlan(string instruction) =>
        instruction.Contains("plan.schemaVersion must be \"2.0\"", StringComparison.Ordinal);

    public static bool IsJob(string instruction) =>
        instruction.Contains("job.schemaVersion must be \"1.0\"", StringComparison.Ordinal);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
