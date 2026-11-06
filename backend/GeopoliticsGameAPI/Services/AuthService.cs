
using GeopoliticsGameAPI.Data;
using GeopoliticsGameAPI.Models;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace GeopoliticsGameAPI.Services
{
    public class AuthService
    {
        private readonly MongoDbContext _context;
        private readonly PasswordHasher<Player> _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;

        public AuthService(
            MongoDbContext context,
            IConfiguration configuration,
            EmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
            _passwordHasher = new PasswordHasher<Player>();
        }

        // Validate passwords on the backend
        private bool IsPasswordValid(string password)
        {
            if (string.IsNullOrWhiteSpace(password) ||
                password.Length < 8 ||
                password.Length > 128)
            {
                return false;
            }

            bool hasUppercase = password.Any(char.IsUpper);
            bool hasLowercase = password.Any(char.IsLower);
            bool hasNumber = password.Any(char.IsDigit);

            bool hasSpecialCharacter = password.Any(
                character =>
                    !char.IsLetterOrDigit(character) &&
                    !char.IsWhiteSpace(character));

            bool hasNoWhitespace = !password.Any(char.IsWhiteSpace);

            return hasUppercase &&
                   hasLowercase &&
                   hasNumber &&
                   hasSpecialCharacter &&
                   hasNoWhitespace;
        }

        // Generate a secure random token
        private string GenerateSecureToken()
        {
            byte[] randomBytes = RandomNumberGenerator.GetBytes(32);

            return Convert.ToBase64String(randomBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        // Hash tokens before storing them
        private string HashToken(string token)
        {
            byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
            byte[] hashBytes = SHA256.HashData(tokenBytes);

            return Convert.ToHexString(hashBytes);
        }

        // REGISTER
        public async Task<Player> RegisterAsync(Register request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.FullName) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                throw new ArgumentException(
                    "Username, full name, email, and password are required.");
            }

            if (!IsPasswordValid(request.Password))
            {
                throw new ArgumentException(
                    "Password must be 8–128 characters and contain an uppercase letter, a lowercase letter, a number and a special character. Spaces are not allowed.");
            }

            string email = request.Email.Trim().ToLowerInvariant();
            string username = request.Username.Trim();
            string fullName = request.FullName.Trim();

            var existingPlayer = await _context.Players
                .Find(player =>
                    player.Email == email ||
                    player.Username == username)
                .FirstOrDefaultAsync();

            if (existingPlayer != null)
            {
                throw new ArgumentException(
                    "An account with the email or username already exists.");
            }

            var player = new Player
            {
                Username = username,
                FullName = fullName,
                Email = email,
                CreatedAt = DateTime.UtcNow,
                IsEmailVerified = false
            };

            player.PasswordHash = _passwordHasher.HashPassword(
                player,
                request.Password);

            string verificationToken = GenerateSecureToken();

            player.EmailVerificationTokenHash =
                HashToken(verificationToken);

            player.EmailVerificationTokenExpiresAt =
                DateTime.UtcNow.AddHours(24);

            await _context.Players.InsertOneAsync(player);

            await _emailService.SendVerificationEmailAsync(
                player.Email,
                player.Username,
                verificationToken);

            return player;
        }

        // VERIFY EMAIL
        public async Task<bool> VerifyEmailAsync(
            string email,
            string token)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalizedEmail = email.Trim().ToLowerInvariant();
            string tokenHash = HashToken(token);

            var player = await _context.Players
                .Find(existingPlayer =>
                    existingPlayer.Email == normalizedEmail)
                .FirstOrDefaultAsync();

            if (player == null ||
                player.IsEmailVerified ||
                string.IsNullOrEmpty(player.EmailVerificationTokenHash) ||
                player.EmailVerificationTokenExpiresAt == null ||
                player.EmailVerificationTokenExpiresAt <= DateTime.UtcNow)
            {
                return false;
            }

            byte[] storedHash = Encoding.UTF8.GetBytes(
                player.EmailVerificationTokenHash);

            byte[] suppliedHash = Encoding.UTF8.GetBytes(tokenHash);

            if (!CryptographicOperations.FixedTimeEquals(
                storedHash,
                suppliedHash))
            {
                return false;
            }

            var filter = Builders<Player>.Filter.And(
                Builders<Player>.Filter.Eq(
                    existingPlayer => existingPlayer.Id,
                    player.Id),
                Builders<Player>.Filter.Eq(
                    existingPlayer => existingPlayer.EmailVerificationTokenHash,
                    tokenHash),
                Builders<Player>.Filter.Eq(
                    existingPlayer => existingPlayer.IsEmailVerified,
                    false),
                Builders<Player>.Filter.Gt(
                    existingPlayer => existingPlayer.EmailVerificationTokenExpiresAt,
                    DateTime.UtcNow)
            );

            var update = Builders<Player>.Update
                .Set(existingPlayer => existingPlayer.IsEmailVerified, true)
                .Unset(existingPlayer => existingPlayer.EmailVerificationTokenHash)
                .Unset(existingPlayer => existingPlayer.EmailVerificationTokenExpiresAt);

            var result = await _context.Players.UpdateOneAsync(
                filter,
                update);

            return result.ModifiedCount == 1;
        }

        // FORGOT PASSWORD
        public async Task ForgotPasswordAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            string normalizedEmail = email.Trim().ToLowerInvariant();

            var player = await _context.Players
                .Find(existingPlayer =>
                    existingPlayer.Email == normalizedEmail)
                .FirstOrDefaultAsync();

            // Do not send reset emails for unverified accounts
            if (player == null || !player.IsEmailVerified)
            {
                return;
            }

            string resetToken = GenerateSecureToken();
            string resetTokenHash = HashToken(resetToken);

            var update = Builders<Player>.Update
                .Set(existingPlayer => existingPlayer.PasswordResetTokenHash,
                    resetTokenHash)
                .Set(existingPlayer => existingPlayer.PasswordResetTokenExpiresAt,
                    DateTime.UtcNow.AddMinutes(30));

            await _context.Players.UpdateOneAsync(
                existingPlayer => existingPlayer.Id == player.Id,
                update);

            await _emailService.SendPasswordResetEmailAsync(
                player.Email,
                player.Username,
                resetToken);
        }

        // RESET PASSWORD
        public async Task<bool> ResetPasswordAsync(
            string email,
            string token,
            string newPassword)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(newPassword))
            {
                return false;
            }

            // Enforce password requirements on the backend
            if (!IsPasswordValid(newPassword))
            {
                return false;
            }

            string normalizedEmail = email.Trim().ToLowerInvariant();
            string tokenHash = HashToken(token);

            var player = await _context.Players
                .Find(existingPlayer =>
                    existingPlayer.Email == normalizedEmail)
                .FirstOrDefaultAsync();

            if (player == null ||
                !player.IsEmailVerified ||
                string.IsNullOrEmpty(player.PasswordResetTokenHash) ||
                player.PasswordResetTokenExpiresAt == null ||
                player.PasswordResetTokenExpiresAt <= DateTime.UtcNow)
            {
                return false;
            }

            byte[] storedHash = Encoding.UTF8.GetBytes(
                player.PasswordResetTokenHash);

            byte[] suppliedHash = Encoding.UTF8.GetBytes(tokenHash);

            if (!CryptographicOperations.FixedTimeEquals(
                storedHash,
                suppliedHash))
            {
                return false;
            }

            string newPasswordHash = _passwordHasher.HashPassword(
                player,
                newPassword);

            var filter = Builders<Player>.Filter.And(
                Builders<Player>.Filter.Eq(
                    existingPlayer => existingPlayer.Id,
                    player.Id),
                Builders<Player>.Filter.Eq(
                    existingPlayer => existingPlayer.PasswordResetTokenHash,
                    tokenHash),
                Builders<Player>.Filter.Gt(
                    existingPlayer => existingPlayer.PasswordResetTokenExpiresAt,
                    DateTime.UtcNow)
            );

            var update = Builders<Player>.Update
                .Set(existingPlayer => existingPlayer.PasswordHash,
                    newPasswordHash)
                .Unset(existingPlayer => existingPlayer.PasswordResetTokenHash)
                .Unset(existingPlayer => existingPlayer.PasswordResetTokenExpiresAt);

            var result = await _context.Players.UpdateOneAsync(
                filter,
                update);

            return result.ModifiedCount == 1;
        }

        // RESEND VERIFICATION EMAIL
        public async Task ResendVerificationEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            string normalizedEmail = email.Trim().ToLowerInvariant();

            var player = await _context.Players
                .Find(existingPlayer =>
                    existingPlayer.Email == normalizedEmail)
                .FirstOrDefaultAsync();

            if (player == null || player.IsEmailVerified)
            {
                return;
            }

            string verificationToken = GenerateSecureToken();
            string tokenHash = HashToken(verificationToken);

            var update = Builders<Player>.Update
                .Set(existingPlayer => existingPlayer.EmailVerificationTokenHash,
                    tokenHash)
                .Set(existingPlayer => existingPlayer.EmailVerificationTokenExpiresAt,
                    DateTime.UtcNow.AddHours(24));

            await _context.Players.UpdateOneAsync(
                existingPlayer => existingPlayer.Id == player.Id,
                update);

            await _emailService.SendVerificationEmailAsync(
                player.Email,
                player.Username,
                verificationToken);
        }

        // GENERATE JWT
        public string GenerateJwtToken(Player player)
        {
            var key = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "JWT signing key is missing.");

            var issuer = _configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException(
                    "JWT issuer is missing.");

            var audience = _configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException(
                    "JWT audience is missing.");

            var expiryMinutes = int.Parse(
                _configuration["Jwt:ExpiryMinutes"] ?? "60");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, player.Id!),
                new Claim(ClaimTypes.Name, player.Username),
                new Claim(ClaimTypes.Email, player.Email)
            };

            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key));

            var credentials = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // LOGIN
        public async Task<AuthResponse?> LoginAsync(Login request)
        {
            if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return null;
            }

            string usernameOrEmail = request.UsernameOrEmail.Trim();
            string normalizedEmail = usernameOrEmail.ToLowerInvariant();

            var player = await _context.Players
                .Find(existingPlayer =>
                    existingPlayer.Email == normalizedEmail ||
                    existingPlayer.Username == usernameOrEmail)
                .FirstOrDefaultAsync();

            if (player == null)
            {
                return null;
            }

            var result = _passwordHasher.VerifyHashedPassword(
                player,
                player.PasswordHash,
                request.Password);

            if (result == PasswordVerificationResult.Failed)
            {
                return null;
            }

            if (!player.IsEmailVerified)
            {
                throw new InvalidOperationException(
                    "Please verify your email address before logging in.");
            }

            string token = GenerateJwtToken(player);

            return new AuthResponse
            {
                Token = token,
                PlayerId = player.Id!,
                Username = player.Username,
                FullName = player.FullName,
                Email = player.Email
            };
        }
    }
}
