using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;

namespace KrissJourney.Kriss.Services;

public class NodeJsonConverter : JsonConverter<NodeBase>
{
    public override bool CanConvert(Type typeToConvert) => typeof(NodeBase).IsAssignableFrom(typeToConvert);

    public override NodeBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // First deserialize to a temporary object to extract the Type property
        using JsonDocument document = JsonDocument.ParseValue(ref reader);

        // Check if the Type property exists and read its value
        if (!document.RootElement.TryGetProperty("type", out JsonElement typeProperty))
            throw new JsonException("JSON object does not contain a Type property");

        string nodeType = typeProperty.GetString().ToLowerInvariant();
        string json = document.RootElement.GetRawText();

        // Directly deserialize to the final implementation class
        return nodeType switch
        {
            "story" => JsonSerializer.Deserialize<StoryNode>(json, options),
            "choice" => JsonSerializer.Deserialize<ChoiceNode>(json, options),
            "dialogue" => JsonSerializer.Deserialize<DialogueNode>(json, options),
            "action" => JsonSerializer.Deserialize<ActionNode>(json, options),
            "fight" => JsonSerializer.Deserialize<FightNode>(json, options),
            "surge" => JsonSerializer.Deserialize<SurgeNode>(json, options),
            "minigame01" => JsonSerializer.Deserialize<MiniGame01>(json, options),
            _ => throw new JsonException($"Unknown node type: {nodeType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, NodeBase value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}

public class EnCharacterJsonConverter : JsonConverter<EnCharacter>
{
    public override EnCharacter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string value = reader.GetString();

        // Try to parse as enum name first
        if (Enum.TryParse(value, true, out EnCharacter result))
            return result;

        // Default fallback
        return EnCharacter.Narrator;
    }

    public override void Write(Utf8JsonWriter writer, EnCharacter value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public static class JsonHelper
{
    public static JsonSerializerOptions Options =>
        new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Converters = { new EnCharacterJsonConverter() }
        };
}