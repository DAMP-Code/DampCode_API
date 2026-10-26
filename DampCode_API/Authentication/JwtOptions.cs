using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DampCode_API.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSecretBytes = 32;
    public const string InvalidSecretMessage =
        "Configure Jwt:Secret com pelo menos 32 bytes por User Secrets ou pela variável Jwt__Secret.";

    [Required]
    public string Issuer { get; set; } = string.Empty;
    [Required]
    public string Audience { get; set; } = string.Empty;
    // Validado por HasValidSecret para que a mensagem de erro seja sempre InvalidSecretMessage.
    public string Secret { get; set; } = string.Empty;
    [Range(1, 43200)]
    public int ExpirationMinutes { get; set; } = 1440;

    public bool HasValidSecret() => Encoding.UTF8.GetByteCount(Secret) >= MinimumSecretBytes;
    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(Secret));
}
