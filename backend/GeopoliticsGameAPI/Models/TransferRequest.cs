namespace GeopoliticsGameAPI.Models
{
    public class TransferRequest
    {
        public string SourceCountryId { get; set; } = string.Empty;
        public string DestinationCountryId { get; set; } = string.Empty;
        public int soldiers {  get; set; }
    }
}
