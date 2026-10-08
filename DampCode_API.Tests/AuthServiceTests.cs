using System.Text.Json;
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
}
