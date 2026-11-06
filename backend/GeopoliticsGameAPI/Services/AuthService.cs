using GeopoliticsGameAPI.Data;
using GeopoliticsGameAPI.Models;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GeopoliticsGameAPI.Services
{
    public class AuthService
    {
        private readonly MongoDbContext _context;
        private readonly PasswordHasher<Player> _passwordHasher;
        private readonly IConfiguration _configuration;

        public AuthService(MongoDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = new PasswordHasher<Player>();
        }

        public async Task<Player> RegisterAsync (Register request)
        {
            if (string.IsNullOrWhiteSpace (request.Username) ||
                string.IsNullOrWhiteSpace (request.Email) || 
                string.IsNullOrWhiteSpace (request.Password))
            {
                throw new ArgumentException("Username, email, and password are required");
            }

            string email = request.Email.Trim().ToLowerInvariant ();
            string username = request.Username.Trim ();

            var existingPlayer = await _context.Players
                                .Find(player => 
                                player.Email == email || 
                                player.Username == username)
                                .FirstOrDefaultAsync();

            if (existingPlayer != null)
            {
                throw new ArgumentException("An account with the email or username already exists");
            }

            var player = new Player()
            {
                Username = username,
                FullName = request.FullName.Trim(),
                Email = email,
                CreatedAt = DateTime.UtcNow

            };

            player.PasswordHash = _passwordHasher.HashPassword(player, request.Password);

            await _context.Players.InsertOneAsync(player); 

            return player;
        }

        public string GenerateJwtToken(Player player)
        {
            var key = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT signing key is missing");

            var issuer = _configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException("JWT issuer is missing");

            var audience = _configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException("JWT audience is missing");

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
