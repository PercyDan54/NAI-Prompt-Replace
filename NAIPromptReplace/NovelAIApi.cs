using System.Net.Http.Headers;
using System.Text.Json;
using NAIPromptReplace.Converters;
using NAIPromptReplace.Models;

namespace NAIPromptReplace;

public class NovelAIApi
{
    private const string novelai_api = "https://api.novelai.net/";
    private const string novelai_image_api = "https://image.novelai.net/";

    private string accessToken = string.Empty;
    private readonly HttpClient httpClient = new HttpClient();
    public static readonly JsonSerializerOptions ApiSerializerOptions = new JsonSerializerOptions
    {
        Converters = { new JsonModelInfoConverter(), new JsonSamplerInfoConverter(), new JsonVector2Converter() },
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    public static readonly JsonSerializerOptions CamelCaseJsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SubscriptionInfo? SubscriptionInfo { get; private set; }
    public string AccessToken => accessToken;

    public async Task<SubscriptionInfo?> UpdateToken(string token) => await getSubscription(token);

    public async Task<SubscriptionInfo?> GetSubscription() => await getSubscription(accessToken);

    private async Task<SubscriptionInfo?> getSubscription(string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, novelai_api + "user/subscription");
        req.Headers.Add("Authorization", "Bearer " + token);
        var resp = await httpClient.SendAsync(req);

        if (resp.IsSuccessStatusCode)
        {
            string str = await resp.Content.ReadAsStringAsync();
            var subscriptionInfo = JsonSerializer.Deserialize<SubscriptionInfo>(str, CamelCaseJsonSerializerOptions);

            if (subscriptionInfo != null)
            {
                accessToken = token;
                SubscriptionInfo = subscriptionInfo;
                return subscriptionInfo;
            }
        }

        return null;
    }

    public async Task<HttpResponseMessage> Generate(GenerationConfig generationConfig, string action)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, novelai_image_api + "ai/generate-image");
        req.Headers.Add("Authorization", "Bearer " + accessToken);

        var data = new Dictionary<string, object>
        {
            { "input", generationConfig.Prompt },
            { "model", generationConfig.Model },
            { "action", action },
            { "parameters", generationConfig.GenerationParameter }
        };

        req.Content = new StringContent(JsonSerializer.Serialize(data, ApiSerializerOptions));
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        return await httpClient.SendAsync(req);
    }
}
