
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GeopoliticsGameAPI.Models
{
    public class Player
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        // Email verification
        public bool IsEmailVerified { get; set; } = false;

        public string? EmailVerificationTokenHash { get; set; }

        public DateTime? EmailVerificationTokenExpiresAt { get; set; }

        // Password reset
        public string? PasswordResetTokenHash { get; set; }

        public DateTime? PasswordResetTokenExpiresAt { get; set; }

        // Account creation
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
