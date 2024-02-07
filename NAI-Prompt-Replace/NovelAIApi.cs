using System.Net.Http.Headers;
using System.Text.Json;

namespace NAI_Prompt_Replace;

public class NovelAIApi
{
    public string AccessToken;
    private const string novelai_api = "https://api.novelai.net/";
    
    private readonly HttpClient httpClient = new HttpClient();
    public static readonly JsonSerializerOptions ApiSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static string ModelNameFromDescription(string des)
    {
        switch (des)
        {
            case "Stable Diffusion 1D44365E":
            case "Stable Diffusion F4D50568":
                return "safe-diffusion";
            case "Stable Diffusion 81274D13":
            case "Stable Diffusion 3B3287AF":
                return "nai-diffusion";
            case "Stable Diffusion 4CC42576":
            case "Stable Diffusion 1D09C008":
            case "Stable Diffusion 1D09D794":
            case "Stable Diffusion F64BA557":
                return "nai-diffusion-furry";
            case "Stable Diffusion 49BFAF6A":
            case "Stable Diffusion F1022D28":
                return "nai-diffusion-2";
            case "Stable Diffusion XL B0BDF6C1":
            case "Stable Diffusion XL C1E1DE52":
            case "Stable Diffusion XL 8BA2AF87":
            default:
                return "nai-diffusion-3";
        }
    }

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

        req.Content = new StringContent(JsonSerializer.Serialize(data, ApiSerializerOptions));
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        return await httpClient.SendAsync(req);
    }
}
