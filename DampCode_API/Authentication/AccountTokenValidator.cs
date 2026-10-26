using System.Security.Claims;
using DampCode_API.Data;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DampCode_API.Authentication;

// O token identifica a conta; o estado atual da conta é sempre conferido no banco.
public sealed class AccountTokenValidator(DampCodeDbContext dbContext)
{
    public async Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var accountId)) return false;
        if (!long.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Iat), out var issuedAt)) return false;

        var account = await dbContext.Accounts.AsNoTracking()
            .Where(item => item.Id == accountId)
            .Select(item => new { item.Status, item.DeletedAt, item.CredentialsChangedAt })
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null || account.DeletedAt is not null || account.Status != AccountStatus.Active) return false;
        return account.CredentialsChangedAt is not { } changedAt
            || ToUnixSeconds(changedAt) <= issuedAt;
    }

    private static long ToUnixSeconds(DateTime value) =>
        new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
