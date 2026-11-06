
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GeopoliticsGameAPI.Services
{
    public class BattleCommentaryService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BattleCommentaryService> _logger;

        public BattleCommentaryService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<BattleCommentaryService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> GenerateCommentaryAsync(
            string attacker,
            string defender,
            List<int> attackerDice,
            List<int> defenderDice,
            int attackerLosses,
            int defenderLosses,
            bool conquered)
        {
            string atmosphere = string.Empty;

            string apiKey = _configuration["Groq:ApiKey"] ?? "";

            try
            {
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException(
                        "Groq API key is missing.");
                }

                string battleDetails =
                    $"Attacker: {attacker}\n" +
                    $"Defender: {defender}\n" +
                    $"Attacker dice: {string.Join(", ", attackerDice)}\n" +
                    $"Defender dice: {string.Join(", ", defenderDice)}\n" +
                    $"Attacker losses: {attackerLosses}\n" +
                    $"Defender losses: {defenderLosses}\n" +
                    $"Conquered: {conquered}";

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.groq.com/openai/v1/chat/completions");

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);

                request.Content = JsonContent.Create(new
                {
                    model = "openai/gpt-oss-20b",
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content =
                                "You are the battle narrator for a geopolitical " +
                                "strategy game. Write 1 or 2 short, vivid, " +
                                "dramatic sentences describing the battle as " +
                                "it unfolds. Use the supplied dice rolls and " +
                                "casualties to shape the narration. Be varied " +
                                "and atmospheric. Never invent dice results, " +
                                "casualties, victories or events. Do not repeat " +
                                "the supplied facts word for word."
                        },
                        new
                        {
                            role = "user",
                            content = battleDetails
                        }
                    }
                });

                using var response = await _httpClient.SendAsync(request);

                response.EnsureSuccessStatusCode();

                using var result = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync());

                atmosphere = result.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()?.Trim() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Groq battle commentary generation failed.");

                atmosphere =
                    "The battlefield remains engulfed in tension.";
            }

            string battleFacts =
                $"{attacker} lost {attackerLosses} soldier(s), while " +
                $"{defender} lost {defenderLosses} soldier(s).";

            string diceResults =
                $"{attacker} rolled [{string.Join(", ", attackerDice)}], " +
                $"while {defender} rolled [{string.Join(", ", defenderDice)}].";

            string outcome = conquered
                ? $"{attacker} conquered {defender}."
                : $"{defender} remains unconquered.";

            return $"{atmosphere} {diceResults} {battleFacts} {outcome}";
        }
    }
}
