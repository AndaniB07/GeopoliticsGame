namespace GeopoliticsGameAPI.Models
{
    public class GameStatistics
    {
        public string GameId { get; set; } = string.Empty;

        public string PlayerCountryName { get; set; } = string.Empty;

        public int Round { get; set; }

        public string Status { get; set; } = string.Empty;

        public string Outcome { get; set; } = string.Empty;

        public int CountriesControlled { get; set; }

        public int TotalSoldiers { get; set; }

        public int CountriesConquered { get; set; }

        public int CountriesLost { get; set; }

        public int BattlesWon { get; set; }

        public int BattlesLost { get; set; }

        public int TotalBattleRounds { get; set; }
    }
}
