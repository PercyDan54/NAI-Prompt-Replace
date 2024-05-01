using System.Text.Json;
using System.Text.Json.Serialization;
using NAIPromptReplace.Models;

namespace NAIPromptReplace.Converters;

public class JsonSamplerInfoConverter : JsonConverter<SamplerInfo>
{
    public override SamplerInfo? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return SamplerInfo.Euler;
        }

        string? str = reader.GetString();
        return SamplerInfo.Samplers.FirstOrDefault(m => m.Id == str, SamplerInfo.Euler);
    }

    public override void Write(Utf8JsonWriter writer, SamplerInfo value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Id);
    }
}
