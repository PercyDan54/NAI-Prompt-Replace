using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NAIPromptReplace.Converters;

public class JsonVector2Converter : JsonConverter<Vector2>
{
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Vector2 result = default(Vector2);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string? propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "X":
                    case "x":
                        result.X = reader.GetSingle();
                        break;
                    case "Y":
                    case "y":
                        result.Y = reader.GetSingle();
                        break;
                }
            }
            else if (reader.TokenType == JsonTokenType.EndObject)
                break;
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteEndObject();
    }
}
