using System.Text.Json;
using System.Text.Json.Serialization;
using NAIPromptReplace.Models;

namespace NAIPromptReplace.Converters;

public class JsonModelInfoConverter : JsonConverter<GenerationModelInfo>
{
    public override GenerationModelInfo? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return GenerationModelInfo.NaiDiffusion3;
        }

        string? str = reader.GetString();
        return GenerationModelInfo.Models.FirstOrDefault(m => m.Id == str, GenerationModelInfo.NaiDiffusion3);
    }

    public override void Write(Utf8JsonWriter writer, GenerationModelInfo value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Id);
    }
}
