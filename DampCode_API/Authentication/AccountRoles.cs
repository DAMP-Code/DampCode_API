using DampCode_API.Models;

namespace DampCode_API.Authentication;

public static class AccountRoles
{
    public const string ClaimType = "role";

    public static string ToFrontRole(AccountType accountType) => accountType switch
    {
        AccountType.Participant => "participante",
        AccountType.Company => "empresa",
        AccountType.Admin => "admin",
        _ => throw new ArgumentOutOfRangeException(nameof(accountType))
    };
}
