
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GeopoliticsGameAPI.Services;

public class OpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public OpenAIService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;

        _apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException(
                "OpenAI API key is missing.");
    }

    public async Task<string> GenerateTextAsync(string prompt)
    {
        var requestBody = new
        {
            model = "gpt-6-luna",
            input = prompt
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.openai.com/v1/responses");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request);

        string responseContent =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenAI request failed with status code {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(responseContent);

        if (document.RootElement.TryGetProperty(
            "output", out var output))
        {
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content))
                    continue;

                foreach (var contentItem in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("type", out var type) &&
                        type.GetString() == "output_text" &&
                        contentItem.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? string.Empty;
                    }
                }
            }
        }

        throw new InvalidOperationException(
            "OpenAI returned no text response.");
    }
}
