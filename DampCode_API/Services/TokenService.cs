using DampCode_API.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DampCode_API.Services
{
    public class TokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string generateToken(User user, DateTime expiresAt)
        {
            ArgumentNullException.ThrowIfNull(user);

            var secret = _configuration["Jwt:Secret"];
            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("JWT Secret não configurada.");

            var keyBytes = Encoding.UTF8.GetBytes(secret);
            if (keyBytes.Length < 32)
                throw new InvalidOperationException("JWT Secret deve ter pelo menos 32 bytes para HS256.");

            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];
            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
                throw new InvalidOperationException("JWT Issuer e Audience devem ser configurados.");

            if (string.IsNullOrWhiteSpace(user.Id) || string.IsNullOrWhiteSpace(user.Name) || string.IsNullOrWhiteSpace(user.Role))
                throw new ArgumentException("Usuário deve possuir ID, nome e papel para gerar um token.", nameof(user));

            if (expiresAt.Kind == DateTimeKind.Unspecified)
                throw new ArgumentException("Informe a expiração com fuso horário definido.", nameof(expiresAt));

            var expiresAtUtc = expiresAt.ToUniversalTime();
            if (expiresAtUtc <= DateTime.UtcNow)
                throw new ArgumentOutOfRangeException(nameof(expiresAt), "A expiração deve estar no futuro.");

            var key = new SymmetricSecurityKey(keyBytes);
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Mantém o papel existente do usuário, sem atribuir novas permissões.
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAtUtc,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
