namespace GeopoliticsGameAPI.Models
{
    public class AttackRequest
    {
        public string AttackerCountryId { get; set; } = string.Empty;
        public string DefenderCountryId { get; set; } = string.Empty;
        public bool ContinueAttack { get; set; } = true;
    }
}
