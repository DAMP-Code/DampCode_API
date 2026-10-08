using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Services;

public sealed class AuthService(DampCodeDbContext dbContext) : IAuthService
{
    public async Task<AuthServiceResult> RegisterParticipantAsync(ParticipantDto dto, CancellationToken cancellationToken)
    {
        var email = dto.Email.Trim();
        var normalizedEmail = Normalize(email);
        if (await EmailExistsAsync(normalizedEmail, cancellationToken)) return Conflict("E-mail já cadastrado.");

        var account = new Account
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(dto.Password),
            AccountType = AccountType.Participant
        };
        var profile = new UserProfile { AccountId = account.Id, Name = dto.Name.Trim(), Account = account };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Accounts.Add(account);
        dbContext.UserProfiles.Add(profile);
        await AddUserTechnologiesAsync(profile, dto.Tecnologias, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Success(account, profile.Name);
    }

    public async Task<AuthServiceResult> RegisterCompanyAsync(CompanyDto dto, CancellationToken cancellationToken)
    {
        var email = dto.Email.Trim();
        var normalizedEmail = Normalize(email);
        var cnpj = OnlyDigits(dto.Cnpj);
        if (await EmailExistsAsync(normalizedEmail, cancellationToken)) return Conflict("E-mail já cadastrado.");
        if (await dbContext.Companies.AnyAsync(company => company.Cnpj == cnpj, cancellationToken)) return Conflict("CNPJ já cadastrado.");

        var account = new Account
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(dto.Password),
            AccountType = AccountType.Company
        };
        var company = new Company
        {
            PrimaryAccountId = account.Id,
            Name = dto.Name.Trim(),
            Cnpj = cnpj,
            Area = dto.Area.Trim(),
            Description = dto.Descricao.Trim(),
            PrimaryAccount = account
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Accounts.Add(account);
        dbContext.Companies.Add(company);
        await AddCompanyTechnologiesAsync(company, dto.Tecnologias, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Success(account, company.Name);
    }

    public async Task<AuthServiceResult> LoginAsync(LoginDto dto, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts
            .Include(item => item.UserProfile)
            .Include(item => item.PrimaryCompany)
            .SingleOrDefaultAsync(item => item.NormalizedEmail == Normalize(dto.Email) && item.DeletedAt == null, cancellationToken);

        if (account is null || !BCrypt.Net.BCrypt.EnhancedVerify(dto.Password, account.PasswordHash))
            return new(false, Error: AuthServiceError.InvalidCredentials, Message: "E-mail ou senha inválidos.");
        if (account.Status != AccountStatus.Active)
            return new(false, Error: AuthServiceError.InactiveAccount, Message: "Conta indisponível.");

        account.LastLoginAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        var name = account.AccountType == AccountType.Company ? account.PrimaryCompany?.Name : account.UserProfile?.Name;
        return Success(account, name ?? account.Email);
    }

    private async Task AddUserTechnologiesAsync(UserProfile profile, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        foreach (var technology in await ResolveTechnologiesAsync(names, cancellationToken))
            profile.Technologies.Add(new UserTechnology { UserProfile = profile, Technology = technology });
    }

    private async Task AddCompanyTechnologiesAsync(Company company, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        foreach (var technology in await ResolveTechnologiesAsync(names, cancellationToken))
            company.Technologies.Add(new CompanyTechnology { Company = company, Technology = technology });
    }

    private async Task<IReadOnlyCollection<Technology>> ResolveTechnologiesAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var distinctNames = names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (distinctNames.Length == 0) return [];

        var normalizedNames = distinctNames.Select(Normalize).ToArray();
        var existing = await dbContext.Technologies.Where(item => normalizedNames.Contains(item.NormalizedName))
            .ToDictionaryAsync(item => item.NormalizedName, cancellationToken);
        var result = new List<Technology>(distinctNames.Length);
        foreach (var name in distinctNames)
        {
            var normalizedName = Normalize(name);
            if (!existing.TryGetValue(normalizedName, out var technology))
            {
                technology = new Technology { Name = name, NormalizedName = normalizedName };
                dbContext.Technologies.Add(technology);
                existing.Add(normalizedName, technology);
            }
            result.Add(technology);
        }
        return result;
    }

    private Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Accounts.AnyAsync(account => account.NormalizedEmail == email, cancellationToken);
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static string OnlyDigits(string value) => new(value.Where(char.IsDigit).ToArray());
    private static AuthServiceResult Conflict(string message) => new(false, Error: AuthServiceError.Conflict, Message: message);
    private static AuthServiceResult Success(Account account, string name) =>
        new(true, new AuthResponseDto(account.Id, name, account.Email, account.AccountType switch
        {
            AccountType.Company => "empresa",
            AccountType.Participant => "participante",
            AccountType.Admin => "admin",
            _ => throw new ArgumentOutOfRangeException(nameof(account.AccountType))
        }));
}
