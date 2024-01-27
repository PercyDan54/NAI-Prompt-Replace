using System.Net.Http.Headers;
using System.Text.Json;

namespace NAI_Prompt_Replace;

public class NovelAIApi
{
    public string AccessToken;
    private const string novelai_api = "https://api.novelai.net/";
    
    private readonly HttpClient httpClient = new HttpClient();
    private readonly JsonSerializerOptions apiSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<HttpResponseMessage> Generate(GenerationConfig generationConfig)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, novelai_api + "ai/generate-image");
        req.Headers.Add("Authorization", "Bearer " + AccessToken);

        var data = new Dictionary<string, object>
        {
            { "input", generationConfig.Prompt },
            { "model", generationConfig.Model },
            { "action", "generate" },
            { "parameters", generationConfig.GenerationParameter }
        };

        req.Content = new StringContent(JsonSerializer.Serialize(data, apiSerializerOptions));
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        return await httpClient.SendAsync(req);
    }
}
