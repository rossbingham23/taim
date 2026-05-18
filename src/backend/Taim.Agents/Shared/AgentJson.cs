using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Taim.Agents.Shared;

internal static class AgentJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static string StripFences(string text)
    {
        var m = Regex.Match(text, @"```(?:json)?\s*([\s\S]+?)\s*```");
        return m.Success ? m.Groups[1].Value : text.Trim();
    }

    // Normalize all JSON object keys from snake_case to camelCase so deserialization works
    // regardless of whether the model outputs executiveTeam or executive_team.
    private static string NormalizeKeys(string json)
    {
        using var doc = JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);
        WriteNormalized(doc.RootElement, writer);
        writer.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteNormalized(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject())
                {
                    writer.WritePropertyName(SnakeToCamel(prop.Name));
                    WriteNormalized(prop.Value, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteNormalized(item, writer);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string SnakeToCamel(string name) =>
        Regex.Replace(name, @"_([a-zA-Z])", m => m.Groups[1].Value.ToUpperInvariant());

    internal static T Deserialize<T>(string? json, string agentName)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException($"{agentName} returned an empty response.");

        var normalized = NormalizeKeys(StripFences(json));
        return JsonSerializer.Deserialize<T>(normalized, Options)
               ?? throw new InvalidOperationException($"{agentName} returned invalid JSON for type {typeof(T).Name}.");
    }
}
