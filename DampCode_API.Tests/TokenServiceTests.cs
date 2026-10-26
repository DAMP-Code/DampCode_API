using DampCode_API.Models;
using DampCode_API.Services;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DampCode_API.Tests;

public sealed class TokenServiceTests
{
    private static Account NewAccount(AccountType accountType = AccountType.Company) => new()
    {
        Email = "ana@exemplo.com",
        NormalizedEmail = "ANA@EXEMPLO.COM",
        PasswordHash = "hash",
        AccountType = accountType
    };

    [Fact]
    public void GenerateToken_IncludesOnlyMinimalClaims()
    {
        var account = NewAccount();

        var result = new TokenService(TestJwt.Options()).GenerateToken(account);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result.Token);

        Assert.Equal("HS256", jwt.Alg);
        Assert.Equal("Api", jwt.Issuer);
        Assert.Contains("Front", jwt.Audiences);
        Assert.Equal(account.Id.ToString(), jwt.Subject);
        Assert.Equal("ana@exemplo.com", jwt.GetClaim("email").Value);
        Assert.Equal("Company", jwt.GetClaim("role").Value);
        Assert.False(string.IsNullOrWhiteSpace(jwt.Id));
        Assert.True(jwt.TryGetPayloadValue<long>("iat", out _));
        Assert.False(jwt.TryGetClaim("name", out _));
    }

    [Fact]
    public void GenerateToken_UsesConfiguredExpiration()
    {
        var before = DateTime.UtcNow;
        var result = new TokenService(TestJwt.Options(expirationMinutes: 60)).GenerateToken(NewAccount());
        var after = DateTime.UtcNow;
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result.Token);

        Assert.InRange(result.ExpiresAt, before.AddMinutes(60), after.AddMinutes(60));
        Assert.True(Math.Abs((jwt.ValidTo - result.ExpiresAt).TotalSeconds) < 1);
    }

    [Fact]
    public void GenerateToken_UsesNewJtiForEachToken()
    {
        var service = new TokenService(TestJwt.Options());
        var account = NewAccount();
        var handler = new JsonWebTokenHandler();

        var first = handler.ReadJsonWebToken(service.GenerateToken(account).Token);
        var second = handler.ReadJsonWebToken(service.GenerateToken(account).Token);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("curta")]
    public void Constructor_RejectsSecretShorterThan32Bytes(string secret) =>
        Assert.Throws<InvalidOperationException>(() => new TokenService(TestJwt.Options(secret: secret)));
}
