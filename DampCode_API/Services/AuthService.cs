using System.Text.RegularExpressions;
using DampCode_API.Authentication;
using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Services;

public sealed class AuthService(DampCodeDbContext dbContext, ITokenService tokenService) : IAuthService
{
    // \z em vez de $: o $ do .NET aceita um "\n" no fim, que estouraria a coluna de 7 caracteres.
    private static readonly Regex ColorPattern = new(@"^#[0-9A-Fa-f]{6}\z", RegexOptions.Compiled);

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
        return Success(account, ResolveName(account));
    }

    public Task<UserResponseDto?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.Accounts.AsNoTracking()
            .Where(account => account.Id == accountId && account.DeletedAt == null)
            .SelectUserResponse()
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AuthServiceResult<UserResponseDto>> UpdateAccountAsync(Guid accountId, UpdateAccountDto dto, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts
            .Include(item => item.UserProfile)
            .SingleOrDefaultAsync(item => item.Id == accountId && item.DeletedAt == null, cancellationToken);
        if (account is null) return new(false, Error: AuthServiceError.NotFound, Message: "Usuário não encontrado.");

        var now = DateTime.UtcNow;
        if (dto.Name is not null)
        {
            // O nome só existe no perfil do participante; empresa e admin não editam por aqui.
            if (account.AccountType == AccountType.Company) return Invalid<UserResponseDto>("O nome da empresa não pode ser mudado por aqui.");
            if (account.UserProfile is null) return Invalid<UserResponseDto>("Esta conta não tem nome para editar.");
            var name = dto.Name.Trim();
            if (name.Length is < 2 or > 150) return Invalid<UserResponseDto>("O nome precisa ter entre 2 e 150 caracteres.");
            account.UserProfile.Name = name;
            account.UserProfile.UpdatedAt = now;
        }

        if (!string.IsNullOrEmpty(dto.AvatarColor) && !ColorPattern.IsMatch(dto.AvatarColor))
            return Invalid<UserResponseDto>("Cor inválida. Use o formato #RRGGBB.");
        account.AvatarColor = string.IsNullOrEmpty(dto.AvatarColor) ? null : dto.AvatarColor;
        account.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, await GetAccountAsync(accountId, cancellationToken));
    }

    public async Task<AuthServiceResult> ChangePasswordAsync(Guid accountId, ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts
            .Include(item => item.UserProfile)
            .Include(item => item.PrimaryCompany)
            .SingleOrDefaultAsync(item => item.Id == accountId && item.DeletedAt == null, cancellationToken);
        if (account is null) return new(false, Error: AuthServiceError.NotFound, Message: "Usuário não encontrado.");

        // 400 e não 401: senha errada aqui não deve deslogar a pessoa no front.
        if (!BCrypt.Net.BCrypt.EnhancedVerify(dto.SenhaAtual, account.PasswordHash))
            return new(false, Error: AuthServiceError.Validation, Message: "Senha atual incorreta.");
        if (dto.NovaSenha.Length is < 10 or > 128)
            return new(false, Error: AuthServiceError.Validation, Message: "A nova senha precisa ter entre 10 e 128 caracteres.");
        if (dto.NovaSenha == dto.SenhaAtual)
            return new(false, Error: AuthServiceError.Validation, Message: "A nova senha precisa ser diferente da atual.");

        // O iat do JWT só tem segundos: a troca passa a valer no próximo segundo cheio,
        // assim um token antigo do mesmo segundo também cai, e o novo token nasce nesse segundo.
        var now = DateTime.UtcNow;
        account.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(dto.NovaSenha);
        account.CredentialsChangedAt = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc).AddSeconds(1);
        account.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Success(account, ResolveName(account));
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
    private static AuthServiceResult<T> Invalid<T>(string message) => new(false, Error: AuthServiceError.Validation, Message: message);
    // Conta sem perfil nem empresa (ex.: Admin) usa o e-mail como nome.
    private static string ResolveName(Account account) =>
        (account.AccountType == AccountType.Company ? account.PrimaryCompany?.Name : account.UserProfile?.Name) ?? account.Email;
    private AuthServiceResult Success(Account account, string name)
    {
        var token = tokenService.GenerateToken(account);
        return new(true, new AuthResponseDto(account.Id, name, account.Email,
            AccountRoles.ToFrontRole(account.AccountType), token.Token, token.ExpiresAt, account.AvatarColor));
    }
}
