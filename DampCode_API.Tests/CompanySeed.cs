using DampCode_API.Data;
using DampCode_API.Models;

namespace DampCode_API.Tests;

internal static class CompanySeed
{
    public static async Task<Account> ParticipantAsync(DampCodeDbContext db, string email, string name = "Maria Lima")
    {
        var account = NewAccount(email, AccountType.Participant);
        db.Accounts.Add(account);
        db.UserProfiles.Add(new UserProfile { AccountId = account.Id, Name = name, Account = account });
        await db.SaveChangesAsync();
        return account;
    }

    public static async Task<Company> CompanyAsync(DampCodeDbContext db, string email = "empresa@exemplo.com",
        CompanyVerificationStatus status = CompanyVerificationStatus.Pending)
    {
        var account = NewAccount(email, AccountType.Company);
        var company = new Company
        {
            PrimaryAccountId = account.Id,
            Name = "Empresa Exemplo",
            Cnpj = string.Concat(Enumerable.Range(0, 14).Select(_ => Random.Shared.Next(10))),
            VerificationStatus = status,
            PrimaryAccount = account
        };
        db.Accounts.Add(account);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    private static Account NewAccount(string email, AccountType type) => new()
    {
        Email = email,
        NormalizedEmail = email.Trim().ToUpperInvariant(),
        PasswordHash = "x",
        AccountType = type
    };
}
