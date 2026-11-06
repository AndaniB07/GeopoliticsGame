
using System.Net.Http.Json;
using System.Text.Json;

namespace GeopoliticsGameAPI.Services
{
    public class BattleCommentaryService
    {
        private readonly HttpClient _httpClient;

        public BattleCommentaryService(HttpClient httpClient)
        {
            _httpClient = httpClient;
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

            // Ask AI for atmosphere only, not battle facts.
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "http://localhost:11434/api/generate",
                    new
                    {
                        model = "qwen2.5:3b",
                        prompt =
                            "Write exactly one short, dramatic sentence " +
                            "suitable for a military strategy game's battle report. " +
                            "Describe only the general tension and atmosphere. " +
                            "Do not mention countries, soldiers, casualties, " +
                            "attacks, victories, defeats, territory, tactics, " +
                            "or any specific events. Do not make factual claims.",
                        stream = false
                    });

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<JsonElement>();

                if (result.TryGetProperty("response", out var commentary))
                {
                    atmosphere = commentary.GetString()?.Trim() ?? string.Empty;
                }
            }
            catch
            {
                // The game must still work if local AI is unavailable.
            }

            // C# determines the authoritative battle facts.
            string battleFacts =
                $"{attacker} lost {attackerLosses} soldier(s), while " +
                $"{defender} lost {defenderLosses} soldier(s).";

            string outcome = conquered
                ? $"{attacker} conquered {defender}."
                : $"{defender} remains unconquered.";

            if (string.IsNullOrWhiteSpace(atmosphere))
            {
                atmosphere = "Tensions remain high on the battlefield.";
            }

            return $"{atmosphere} {battleFacts} {outcome}";
        }
    }
}
