using DampCode_API.Authentication;
using Microsoft.Extensions.Options;

namespace DampCode_API.Tests;

internal static class TestJwt
{
    public const string Secret = "segredo-de-teste-da-dampcode-com-mais-de-32-bytes";

    public static IOptions<JwtOptions> Options(int expirationMinutes = 1440, string? secret = null) =>
        Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "Api",
            Audience = "Front",
            Secret = secret ?? Secret,
            ExpirationMinutes = expirationMinutes
        });
}
