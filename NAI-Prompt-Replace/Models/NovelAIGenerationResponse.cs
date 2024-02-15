using System.Text.Json.Serialization;

namespace NAI_Prompt_Replace;

public class NovelAIGenerationResponse
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}
