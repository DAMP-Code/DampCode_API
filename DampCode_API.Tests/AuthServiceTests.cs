using System.Security.Claims;
using System.Text.Json;
using DampCode_API.Authentication;
using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using DampCode_API.Services;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DampCode_API.Tests;

public sealed class AuthServiceTests
{
    private readonly DampCodeDbContext _dbContext = TestDb.Create();
    private readonly AuthService _service;

    public AuthServiceTests() => _service = new AuthService(_dbContext, new TokenService(TestJwt.Options()));

    private static ParticipantDto Participant(string email = "ana@exemplo.com") => new()
    {
        Name = "Ana Souza",
        Email = email,
        Password = "SenhaForte123",
        Tecnologias = ["React", "react", "C#"]
    };

    private static CompanyDto Company(string email, string cnpj) => new()
    {
        Name = "Empresa Exemplo",
        Email = email,
        Password = "SenhaForte123",
        Cnpj = cnpj,
        Area = "Tecnologia",
        Descricao = "Empresa de software de exemplo"
    };

    [Fact]
    public async Task RegisterParticipant_ReturnsTokenForCreatedAccount()
    {
        var result = await _service.RegisterParticipantAsync(Participant(), CancellationToken.None);

        Assert.True(result.Succeeded);
        var value = result.Value!;
        Assert.Equal("participante", value.Role);
        Assert.Equal("Ana Souza", value.Name);
        Assert.True(value.ExpiresAt > DateTime.UtcNow);
        Assert.Equal(value.Id.ToString(), new JsonWebTokenHandler().ReadJsonWebToken(value.Token).Subject);
        Assert.Equal(2, _dbContext.UserTechnologies.Count());
    }

    [Fact]
    public async Task RegisterCompany_ReturnsEmpresaRoleAndToken()
    {
        var result = await _service.RegisterCompanyAsync(Company("rh@empresa.com", "12.345.678/0001-90"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("empresa", result.Value!.Role);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Token));
        Assert.Equal("12345678000190", _dbContext.Companies.Single().Cnpj);
    }

    [Fact]
    public async Task Register_DuplicateEmailWithDifferentCase_IsConflict()
    {
        await _service.RegisterParticipantAsync(Participant("ana@exemplo.com"), CancellationToken.None);

        var result = await _service.RegisterParticipantAsync(Participant("ANA@Exemplo.com"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AuthServiceError.Conflict, result.Error);
    }

    [Fact]
    public async Task RegisterCompany_DuplicateCnpjInOtherFormat_IsConflict()
    {
        await _service.RegisterCompanyAsync(Company("a@empresa.com", "12.345.678/0001-90"), CancellationToken.None);

        var result = await _service.RegisterCompanyAsync(Company("b@empresa.com", "12345678000190"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AuthServiceError.Conflict, result.Error);
    }

    [Fact]
    public async Task Login_WithDifferentEmailCase_Succeeds()
    {
        await _service.RegisterParticipantAsync(Participant("ana@exemplo.com"), CancellationToken.None);

        var result = await _service.LoginAsync(new LoginDto { Email = "ANA@EXEMPLO.COM", Password = "SenhaForte123" }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.Token));
        Assert.NotNull(_dbContext.Accounts.Single().LastLoginAt);
    }

    [Fact]
    public async Task Login_WithWrongPassword_IsInvalidCredentials()
    {
        await _service.RegisterParticipantAsync(Participant(), CancellationToken.None);

        var result = await _service.LoginAsync(new LoginDto { Email = "ana@exemplo.com", Password = "SenhaErrada123" }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AuthServiceError.InvalidCredentials, result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Login_SuspendedAccount_IsInactive()
    {
        await _service.RegisterParticipantAsync(Participant(), CancellationToken.None);
        _dbContext.Accounts.Single().Status = AccountStatus.Suspended;
        await _dbContext.SaveChangesAsync();

        var result = await _service.LoginAsync(new LoginDto { Email = "ana@exemplo.com", Password = "SenhaForte123" }, CancellationToken.None);

        Assert.Equal(AuthServiceError.InactiveAccount, result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task AuthResponse_NeverContainsPasswordHash()
    {
        var result = await _service.RegisterParticipantAsync(Participant(), CancellationToken.None);
        var json = JsonSerializer.Serialize(result.Value);

        Assert.DoesNotContain(_dbContext.Accounts.Single().PasswordHash, json);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAccount_AdminWithoutProfile_FallsBackToEmailAndAdminRole()
    {
        var admin = new Account
        {
            Email = "admin@dampcode.com",
            NormalizedEmail = "ADMIN@DAMPCODE.COM",
            PasswordHash = "hash",
            AccountType = AccountType.Admin
        };
        _dbContext.Accounts.Add(admin);
        await _dbContext.SaveChangesAsync();

        var user = await _service.GetAccountAsync(admin.Id, CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal("admin", user.Role);
        Assert.Equal("admin@dampcode.com", user.Name);
    }

    [Fact]
    public async Task GetAccount_DeletedAccount_ReturnsNull()
    {
        var registered = await _service.RegisterParticipantAsync(Participant(), CancellationToken.None);
        _dbContext.Accounts.Single().DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        Assert.Null(await _service.GetAccountAsync(registered.Value!.Id, CancellationToken.None));
    }

    private async Task<Account> AddAdminAsync()
    {
        var admin = new Account
        {
            Email = "admin@dampcode.com",
            NormalizedEmail = "ADMIN@DAMPCODE.COM",
            PasswordHash = "hash",
            AccountType = AccountType.Admin
        };
        _dbContext.Accounts.Add(admin);
        await _dbContext.SaveChangesAsync();
        return admin;
    }

    [Fact]
    public async Task UpdateAccount_Participant_TrimsNameAndSavesColor()
    {
        var registered = (await _service.RegisterParticipantAsync(Participant(), CancellationToken.None)).Value!;

        var result = await _service.UpdateAccountAsync(registered.Id,
            new UpdateAccountDto { Name = "  Ana Lima  ", AvatarColor = "#C68FE6" }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Ana Lima", result.Value!.Name);
        Assert.Equal("#C68FE6", result.Value.AvatarColor);
        Assert.Equal("participante", result.Value.Role);
        Assert.Equal("Ana Lima", _dbContext.UserProfiles.Single().Name);
        Assert.Equal("#C68FE6", _dbContext.Accounts.Single().AvatarColor);
    }

    [Fact]
    public async Task UpdateAccount_NullName_KeepsName()
    {
        var registered = (await _service.RegisterParticipantAsync(Participant(), CancellationToken.None)).Value!;

        var result = await _service.UpdateAccountAsync(registered.Id,
            new UpdateAccountDto { Name = null, AvatarColor = "#123abc" }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Ana Souza", result.Value!.Name);
        Assert.Equal("#123abc", result.Value.AvatarColor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task UpdateAccount_EmptyColor_ClearsIt(string? color)
    {
        var registered = (await _service.RegisterParticipantAsync(Participant(), CancellationToken.None)).Value!;
        _dbContext.Accounts.Single().AvatarColor = "#C68FE6";
        await _dbContext.SaveChangesAsync();

        var result = await _service.UpdateAccountAsync(registered.Id, new UpdateAccountDto { AvatarColor = color }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.AvatarColor);
        Assert.Null(_dbContext.Accounts.Single().AvatarColor);
    }

    [Theory]
    [InlineData("C68FE6")]
    [InlineData("#C68FE")]
    [InlineData("#C68FE6A")]
    [InlineData("#GGGGGG")]
    [InlineData("red")]
    [InlineData(" ")]
    [InlineData("#C68FE6\n")]
    public async Task UpdateAccount_InvalidColor_IsValidation(string color)
    {
        var registered = (await _service.RegisterParticipantAsync(Participant(), CancellationToken.None)).Value!;

        var result = await _service.UpdateAccountAsync(registered.Id, new UpdateAccountDto { AvatarColor = color }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(AuthServiceError.Validation, result.Error);
        Assert.Equal("Cor inválida. Use o formato #RRGGBB.", result.Message);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("   A   ")]
    [InlineData("    ")]
    public async Task UpdateAccount_NameWithInvalidLength_IsValidation(string name)
    {
        var registered = (await _service.RegisterParticipantAsync(Participant(), CancellationToken.None)).Value!;

        var result = await _service.UpdateAccountAsync(registered.Id, new UpdateAccountDto { Name = name }, CancellationToken.None);

        Assert.Equal(AuthServiceError.Validation, result.Error);
        Assert.Equal("O nome precisa ter entre 2 e 150 caracteres.", result.Message);
        Assert.Equal("Ana Souza", _dbContext.UserProfiles.Single().Name);
    }

    [Fact]
    public async Task UpdateAccount_NameTooLong_IsValidation()
    {
        var registered = (await _service.RegisterParticipantAsync(Participant(), CancellationToken.None)).Value!;

        var result = await _service.UpdateAccountAsync(registered.Id, new UpdateAccountDto { Name = new string('a', 151) }, CancellationToken.None);

        Assert.Equal(AuthServiceError.Validation, result.Error);
    }

    [Fact]
    public async Task UpdateAccount_CompanyName_IsValidation()
    {
        var registered = (await _service.RegisterCompanyAsync(Company("rh@empresa.com", "12.345.678/0001-90"), CancellationToken.None)).Value!;

        var result = await _service.UpdateAccountAsync(registered.Id, new UpdateAccountDto { Name = "Outro Nome" }, CancellationToken.None);

        Assert.Equal(AuthServiceError.Validation, result.Error);
        Assert.Equal("O nome da empresa não pode ser mudado por aqui.", result.Message);
        Assert.Equal("Empresa Exemplo", _dbContext.Companies.Single().Name);
    }

    [Fact]
    public async Task UpdateAccount_CompanyColorOnly_Succeeds()
    {
        var registered = (await _service.RegisterCompanyAsync(Company("rh@empresa.com", "12.345.678/0001-90"), CancellationToken.None)).Value!;

        var result = await _service.UpdateAccountAsync(registered.Id, new UpdateAccountDto { AvatarColor = "#000000" }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Empresa Exemplo", result.Value!.Name);
        Assert.Equal("#000000", result.Value.AvatarColor);
    }

    [Fact]
    public async Task UpdateAccount_AdminName_IsValidation()
    {
        var admin = await AddAdminAsync();

        var result = await _service.UpdateAccountAsync(admin.Id, new UpdateAccountDto { Name = "Admin" }, CancellationToken.None);

        Assert.Equal(AuthServiceError.Validation, result.Error);
        Assert.Equal("Esta conta não tem nome para editar.", result.Message);
    }

    [Fact]
    public async Task UpdateAccount_UnknownAccount_IsNotFound()
    {
        var result = await _service.UpdateAccountAsync(Guid.NewGuid(), new UpdateAccountDto { AvatarColor = "#000000" }, CancellationToken.None);

        Assert.Equal(AuthServiceError.NotFound, result.Error);
    }

    [Fact]
    public async Task UpdateAccount_TouchesUpdatedAt()
    {
        var registered = (await _service.RegisterParticipantAsync(Participant(), CancellationToken.None)).Value!;
        var old = DateTime.UtcNow.AddDays(-1);
        _dbContext.Accounts.Single().UpdatedAt = old;
        _dbContext.UserProfiles.Single().UpdatedAt = old;
        await _dbContext.SaveChangesAsync();

        await _service.UpdateAccountAsync(registered.Id, new UpdateAccountDto { Name = "Ana Lima", AvatarColor = "#C68FE6" }, CancellationToken.None);

        Assert.True(_dbContext.Accounts.Single().UpdatedAt > old);
        Assert.True(_dbContext.UserProfiles.Single().UpdatedAt > old);
    }

    [Fact]
    public async Task AvatarColor_IsReturnedByLoginAndGetAccount()
    {
        var registered = (await _service.RegisterParticipantAsync(Participant(), CancellationToken.None)).Value!;
        Assert.Null(registered.AvatarColor);
        await _service.UpdateAccountAsync(registered.Id, new UpdateAccountDto { AvatarColor = "#C68FE6" }, CancellationToken.None);

        var login = await _service.LoginAsync(new LoginDto { Email = "ana@exemplo.com", Password = "SenhaForte123" }, CancellationToken.None);
        var user = await _service.GetAccountAsync(registered.Id, CancellationToken.None);

        Assert.Equal("#C68FE6", login.Value!.AvatarColor);
        Assert.Equal("#C68FE6", user!.AvatarColor);
    }

    private static ChangePasswordDto NewPassword(string current = "SenhaForte123", string next = "NovaSenhaForte456") =>
        new() { SenhaAtual = current, NovaSenha = next };

    [Fact]
    public async Task ChangePassword_ReturnsNewTokenAndReplacesHash()
    {
        var registered = (await _service.RegisterParticipantAsync(Participant(), CancellationToken.None)).Value!;

        var result = await _service.ChangePasswordAsync(registered.Id, NewPassword(), CancellationToken.None);

        Assert.True(result.Succeeded);
        var value = result.Value!;
        Assert.Equal(registered.Id, value.Id);
        Assert.Equal("Ana Souza", value.Name);
        Assert.Equal("participante", value.Role);
        Assert.NotEqual(registered.Token, value.Token);
        var account = _dbContext.Accounts.Single();
        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify("NovaSenhaForte456", account.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.EnhancedVerify("SenhaForte123", account.PasswordHash));
        Assert.NotNull(account.CredentialsChangedAt);
    }

    [Fact]
    public async Task ChangePassword_OldTokenIsRejectedAndNewTokenIsAccepted()
    {
        var registered = (await _service.RegisterParticipantAsync(Participant(), CancellationToken.None)).Value!;

        // Sem esperar: o token antigo é do mesmo segundo da troca.
        var result = await _service.ChangePasswordAsync(registered.Id, NewPassword(), CancellationToken.None);

        var validator = new AccountTokenValidator(_dbContext);
        Assert.False(await validator.IsValidAsync(PrincipalOf(registered.Token), CancellationToken.None));
        Assert.True(await validator.IsValidAsync(PrincipalOf(result.Value!.Token), CancellationToken.None));
    }

    [Fact]
    public async Task ChangePassword_CompanyAccount_UsesCompanyName()
    {
        var registered = (await _service.RegisterCompanyAsync(Company("rh@empresa.com", "12.345.678/0001-90"), CancellationToken.None)).Value!;

        var result = await _service.ChangePasswordAsync(registered.Id, NewPassword(), CancellationToken.None);

        Assert.Equal("Empresa Exemplo", result.Value!.Name);
        Assert.Equal("empresa", result.Value.Role);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_IsValidation()
    {
        var registered = (await _service.RegisterParticipantAsync(Participant(), CancellationToken.None)).Value!;
        var hash = _dbContext.Accounts.Single().PasswordHash;

        var result = await _service.ChangePasswordAsync(registered.Id, NewPassword(current: "SenhaErrada123"), CancellationToken.None);

        Assert.Equal(AuthServiceError.Validation, result.Error);
        Assert.Equal("Senha atual incorreta.", result.Message);
        Assert.Equal(hash, _dbContext.Accounts.Single().PasswordHash);
        Assert.Null(_dbContext.Accounts.Single().CredentialsChangedAt);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(129)]
    public async Task ChangePassword_NewPasswordWithInvalidLength_IsValidation(int length)
    {
        var registered = (await _service.RegisterParticipantAsync(Participant(), CancellationToken.None)).Value!;

        var result = await _service.ChangePasswordAsync(registered.Id, NewPassword(next: new string('a', length)), CancellationToken.None);

        Assert.Equal(AuthServiceError.Validation, result.Error);
        Assert.Equal("A nova senha precisa ter entre 10 e 128 caracteres.", result.Message);
    }

    [Fact]
    public async Task ChangePassword_SameAsCurrent_IsValidation()
    {
        var registered = (await _service.RegisterParticipantAsync(Participant(), CancellationToken.None)).Value!;

        var result = await _service.ChangePasswordAsync(registered.Id, NewPassword(next: "SenhaForte123"), CancellationToken.None);

        Assert.Equal(AuthServiceError.Validation, result.Error);
        Assert.Equal("A nova senha precisa ser diferente da atual.", result.Message);
    }

    [Fact]
    public async Task ChangePassword_UnknownAccount_IsNotFound()
    {
        var result = await _service.ChangePasswordAsync(Guid.NewGuid(), NewPassword(), CancellationToken.None);

        Assert.Equal(AuthServiceError.NotFound, result.Error);
    }

    private static ClaimsPrincipal PrincipalOf(string token) =>
        new(new ClaimsIdentity(new JsonWebTokenHandler().ReadJsonWebToken(token).Claims, "test"));
}
