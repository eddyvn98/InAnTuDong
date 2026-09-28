namespace PrintAI.Planning;

internal static class AntigravityPlannerSchema
{
    public static string Resolve(string instruction)
    {
        if (IsGeneralPlan(instruction))
            return Envelope("plan");

        if (IsJob(instruction))
            return Envelope("job");

        return """{"type":"object"}""";
    }

    public static bool IsGeneralPlan(string instruction) =>
        instruction.Contains(
            "plan.schemaVersion must be \"2.0\"",
            StringComparison.Ordinal);

    public static bool IsJob(string instruction) =>
        instruction.Contains(
            "job.schemaVersion must be \"1.0\"",
            StringComparison.Ordinal);

    private static string Envelope(string payloadProperty) =>
        $$"""
        {
          "type": "object",
          "properties": {
            "{{payloadProperty}}": { "type": "object" },
            "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
            "questions": { "type": "array", "items": { "type": "string" } },
            "warnings": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["{{payloadProperty}}", "confidence", "questions", "warnings"],
          "additionalProperties": false
        }
        """;
}
