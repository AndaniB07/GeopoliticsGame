namespace GeopoliticsGameAPI.Models
{
    public class BattleResult
    {
        public string AttackerCountryId { get; set; } = string.Empty;
        public string DefenderCountryId { get; set; } = string.Empty;
        public string AttackerCountryName { get; set; } = string.Empty;
        public string DefenderCountryName { get; set; } = string.Empty;
        public List<int> AttackerDice { get; set; } = new();
        public List<int> DefenderDice { get; set; } = new();
        public int AttackerLosses { get; set; }
        public int DefenderLosses { get; set; }
        public bool DefenderConquered { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
