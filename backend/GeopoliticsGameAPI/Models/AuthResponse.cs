namespace GeopoliticsGameAPI.Models
{
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email {  get; set; } = string.Empty;
    }
}
