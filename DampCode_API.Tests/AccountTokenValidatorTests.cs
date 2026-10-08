using System.Security.Claims;
using DampCode_API.Authentication;
using DampCode_API.Models;

namespace DampCode_API.Tests;

public sealed class AccountTokenValidatorTests
{
    private static ClaimsPrincipal Principal(string sub, DateTime issuedAt) => new(new ClaimsIdentity(
    [
        new Claim("sub", sub),
        new Claim("iat", new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString())
    ], "test"));

    private static async Task<(AccountTokenValidator Validator, Account Account)> SeedAsync(Action<Account>? change = null)
    {
        var dbContext = TestDb.Create();
        var account = new Account
        {
            Email = "ana@exemplo.com",
            NormalizedEmail = "ANA@EXEMPLO.COM",
            PasswordHash = "hash",
            AccountType = AccountType.Participant
        };
        change?.Invoke(account);
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();
        return (new AccountTokenValidator(dbContext), account);
    }

    [Fact]
    public async Task ActiveAccount_IsValid()
    {
        var (validator, account) = await SeedAsync();
        Assert.True(await validator.IsValidAsync(Principal(account.Id.ToString(), DateTime.UtcNow), CancellationToken.None));
    }

    [Fact]
    public async Task UnknownAccount_IsInvalid()
    {
        var (validator, _) = await SeedAsync();
        Assert.False(await validator.IsValidAsync(Principal(Guid.NewGuid().ToString(), DateTime.UtcNow), CancellationToken.None));
    }

    [Fact]
    public async Task MalformedSubject_IsInvalid()
    {
        var (validator, _) = await SeedAsync();
        Assert.False(await validator.IsValidAsync(Principal("nao-e-guid", DateTime.UtcNow), CancellationToken.None));
    }

    [Fact]
    public async Task DeletedAccount_IsInvalid()
    {
        var (validator, account) = await SeedAsync(item => item.DeletedAt = DateTime.UtcNow);
        Assert.False(await validator.IsValidAsync(Principal(account.Id.ToString(), DateTime.UtcNow), CancellationToken.None));
    }

    [Theory]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Disabled)]
    public async Task InactiveAccount_IsInvalid(AccountStatus status)
    {
        var (validator, account) = await SeedAsync(item => item.Status = status);
        Assert.False(await validator.IsValidAsync(Principal(account.Id.ToString(), DateTime.UtcNow), CancellationToken.None));
    }

    [Fact]
    public async Task TokenIssuedBeforeCredentialsChange_IsInvalid()
    {
        var (validator, account) = await SeedAsync(item => item.CredentialsChangedAt = DateTime.UtcNow);
        Assert.False(await validator.IsValidAsync(Principal(account.Id.ToString(), DateTime.UtcNow.AddMinutes(-10)), CancellationToken.None));
    }

    [Fact]
    public async Task TokenIssuedAfterCredentialsChange_IsValid()
    {
        var (validator, account) = await SeedAsync(item => item.CredentialsChangedAt = DateTime.UtcNow.AddMinutes(-10));
        Assert.True(await validator.IsValidAsync(Principal(account.Id.ToString(), DateTime.UtcNow), CancellationToken.None));
    }
}
