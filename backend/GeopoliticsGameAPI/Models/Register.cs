
using System.ComponentModel.DataAnnotations;

namespace GeopoliticsGameAPI.Models
{
    public class Register
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(20, MinimumLength = 3,
            ErrorMessage = "Username must be between 3 and 20 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$",
            ErrorMessage = "Username can only contain letters, numbers and underscores.")]
        public string Username { get; set; } = string.Empty;


        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(80, MinimumLength = 2,
            ErrorMessage = "Full name must be between 2 and 80 characters.")]
        [RegularExpression(@"^[\p{L}\p{M}][\p{L}\p{M}\s.'’-]*$",
            ErrorMessage = "Full name contains invalid characters.")]
        public string FullName { get; set; } = string.Empty;


        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(254,
            ErrorMessage = "Email address cannot exceed 254 characters.")]
        public string Email { get; set; } = string.Empty;


        [Required(ErrorMessage = "Password is required.")]
        [StringLength(128, MinimumLength = 8,
            ErrorMessage = "Password must be between 8 and 128 characters.")]
        [RegularExpression(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9\s])\S+$",
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number and one special character. Spaces are not allowed.")]
        public string Password { get; set; } = string.Empty;
    }
}
