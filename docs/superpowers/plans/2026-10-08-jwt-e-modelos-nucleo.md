# JWT e revisão das models do núcleo — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar autenticação JWT (token no login/cadastro, `[Authorize]`, Swagger Authorize, `GET /api/Auth/me`), limpar o legado, alinhar `Hackathon` aos estados definidos e gerar a migration inicial do núcleo.

**Architecture:** `TokenService` (HS256, configurado por `JwtOptions`) gera o token; o JwtBearer valida assinatura/issuer/audience/expiração e, em `OnTokenValidated`, o `AccountTokenValidator` confere no PostgreSQL se a conta continua ativa e sem troca de credenciais após o `iat`. Controllers continuam finos; regras ficam nos services. Tudo o que é JWT fica na pasta `Authentication/`, exceto o `TokenService`, que mantém nome e pasta.

**Tech Stack:** ASP.NET Core 8 (net8.0), EF Core 8.0.11 + Npgsql, `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.11, `Microsoft.IdentityModel.JsonWebTokens`, Swashbuckle 6.6.2, xUnit, EF Core InMemory, `Microsoft.AspNetCore.Mvc.Testing`.

**Spec:** `docs/superpowers/specs/2026-10-08-jwt-e-modelos-nucleo-design.md`

## Global Constraints

- Trabalhar na branch `feat/jwt-e-modelos-nucleo` (não commitar direto na `master`).
- Projeto da API permanece em `net8.0`; pacotes EF/ASP.NET na linha `8.0.11`.
- A máquina só tem runtime .NET 10: o projeto de testes usa `<RollForward>Major</RollForward>`; comandos `dotnet ef` rodam com `DOTNET_ROLL_FORWARD=Major`.
- Rotas existentes, nomes de DTOs e `CustomSchemaIds` do Swagger (`ParticipanteDto`, `EmpresaDto`, `CreateHackathonDTO`) não mudam; mudanças de contrato são apenas aditivas.
- `AuthResponseDto` JSON: `id`, `name`, `email`, `role`, `token`, `expiresAt`.
- Roles do front: `Participant → "participante"`, `Company → "empresa"`, `Admin → "admin"`.
- Claims do token: `sub`, `email`, `role` (nome do `AccountType`), `jti`, `iat`. Nada além disso.
- `Jwt:Issuer = "Api"`, `Jwt:Audience = "Front"`, `Jwt:ExpirationMinutes = 1440`; `Jwt:Secret` ≥ 32 bytes UTF-8, somente em User Secrets / `Jwt__Secret`, nunca versionado.
- `ClockSkew = 1 minuto`; `MapInboundClaims = false`.
- Nenhuma senha ou secret real em arquivos versionados.
- Código C# segue o estilo atual: file-scoped namespaces, primary constructors, `sealed`, `CancellationToken cancellationToken`.
- Mensagens de commit no padrão do repositório (`Feat:`, `Fix:`, `Chore:`, `Docs:`, `Test:`), em português.

## Review Focus

- Token assinado com outro secret (adulterado) → 401 em rota protegida. (Task 6)
- Token expirado → 401, mesmo com a conta ativa. (Task 6)
- Login com e-mail em caixa diferente da cadastrada → sucesso. (Task 5)
- Conta `Admin` sem perfil na listagem / `me` → `name` cai para o e-mail e `role` é `"admin"`. (Task 5)
- API sem `Jwt:Secret` (ou curto) → não sobe, mensagem cita `Jwt:Secret`. (Task 6)

---

## File Structure

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `DampCode_API/Models/StudentData.cs` | Delete | legado fora do DER |
| `DampCode_API/WeatherForecast.cs` | Delete | template |
| `DampCode_API/Controllers/WeatherForecastController.cs` | Delete | template |
| `DampCode_API/Models/Hackathon.cs` | Modify | enums de status e participação |
| `DampCode_API/Authentication/AccountRoles.cs` | Create | conversão `AccountType` → role do front; nome da claim de role |
| `DampCode_API/Authentication/JwtOptions.cs` | Create | opções `Jwt`, validação do secret, chave de assinatura |
| `DampCode_API/Services/ITokenService.cs` | Create | contrato + `TokenResult` |
| `DampCode_API/Services/TokenService.cs` | Rewrite | geração do token |
| `DampCode_API/Authentication/AccountTokenValidator.cs` | Create | checagem da conta por requisição |
| `DampCode_API/Authentication/JwtAuthenticationExtensions.cs` | Create | registro de opções, services e JwtBearer |
| `DampCode_API/Dto/AuthResponseDto.cs` | Modify | `Token`, `ExpiresAt` |
| `DampCode_API/Dto/UserResponseProjection.cs` | Create | projeção única `Account` → `UserResponseDto` |
| `DampCode_API/Services/IAuthService.cs` | Modify | `GetAccountAsync` |
| `DampCode_API/Services/AuthService.cs` | Modify | token no sucesso, `GetAccountAsync` |
| `DampCode_API/Controllers/AuthController.cs` | Modify | `[AllowAnonymous]`, `GET me` |
| `DampCode_API/Controllers/UsersController.cs` | Modify | `[Authorize]`, projeção única |
| `DampCode_API/Program.cs` | Modify | JWT, Swagger Bearer, `UseAuthentication`, `partial class Program` |
| `DampCode_API/DampCode_API.csproj` | Modify | pacotes JwtBearer e OpenIdConnect alinhado |
| `DampCode_API/Data/DampCodeDbContextFactory.cs` | Create | DbContext em design-time |
| `DampCode_API/Data/Migrations/*` | Generate | migration `InitialCore` |
| `.config/dotnet-tools.json` | Create | `dotnet-ef` 8.0.11 local |
| `DampCode_API.Tests/*` | Create | testes |
| `DampCode_API.slnx` | Modify | inclui projeto de testes |
| `README.md` | Rewrite | setup e fluxo de teste |

---

### Task 0: Branch de trabalho

- [ ] **Step 1: Criar a branch**

```bash
git checkout master
git checkout -b feat/jwt-e-modelos-nucleo
```

Expected: `Switched to a new branch 'feat/jwt-e-modelos-nucleo'`.

---

### Task 1: Remover legado e tipar estados do Hackathon

**Files:**
- Delete: `DampCode_API/Models/StudentData.cs`, `DampCode_API/WeatherForecast.cs`, `DampCode_API/Controllers/WeatherForecastController.cs`
- Modify: `DampCode_API/Models/Hackathon.cs`

**Interfaces:**
- Produces: `HackathonParticipationMode { Individual, Team }`, `HackathonStatus { Draft, Scheduled, Active, InReview, Closed, Cancelled }` em `DampCode_API.Models`.

Sem teste: são remoções de código não referenciado e uma mudança de tipo em uma model fora do DbContext. A verificação é o build.

- [ ] **Step 1: Confirmar que nada referencia os arquivos a remover**

Run: `git grep -n -E "StudentData|WeatherForecast" -- "*.cs"`
Expected: somente linhas dentro dos três arquivos que serão removidos.

- [ ] **Step 2: Remover os arquivos**

```bash
git rm DampCode_API/Models/StudentData.cs DampCode_API/WeatherForecast.cs DampCode_API/Controllers/WeatherForecastController.cs
```

- [ ] **Step 3: Substituir `DampCode_API/Models/Hackathon.cs`**

```csharp
namespace DampCode_API.Models;

// Entidade-base preservada para a próxima etapa. Ainda não faz parte do DbContext
// porque publicação e gerenciamento dependem da autorização empresarial.
public sealed class Hackathon
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public required string Description { get; set; }
    public required string Area { get; set; }
    public HackathonParticipationMode ParticipationMode { get; set; }
    public HackathonStatus Status { get; set; } = HackathonStatus.Draft;
    // Valores (quiz, links, arquivos, executor) ainda não definidos.
    public string? EvaluationMethod { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime? ReviewEndsAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? BackgroundColor { get; set; }
    public string? LogoStorageKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

public enum HackathonParticipationMode { Individual, Team }
public enum HackathonStatus { Draft, Scheduled, Active, InReview, Closed, Cancelled }
```

- [ ] **Step 4: Build**

Run: `dotnet build DampCode_API/DampCode_API.csproj -nologo -v q`
Expected: `0 Erro(s)` (ou `0 Error(s)`).

- [ ] **Step 5: Commit**

```bash
git add -A DampCode_API/Models DampCode_API/Controllers DampCode_API/WeatherForecast.cs
git commit -m "Chore: remove legado StudentData/WeatherForecast e tipa estados do Hackathon"
```

---

### Task 2: Projeto de testes e `AccountRoles` (corrige role do Admin)

**Files:**
- Create: `DampCode_API.Tests/DampCode_API.Tests.csproj`
- Create: `DampCode_API.Tests/AccountRolesTests.cs`
- Create: `DampCode_API/Authentication/AccountRoles.cs`
- Modify: `DampCode_API/Services/AuthService.cs` (método `Success`)
- Modify: `DampCode_API.slnx`

**Interfaces:**
- Produces: `DampCode_API.Authentication.AccountRoles` com `const string ClaimType = "role"` e `static string ToFrontRole(AccountType accountType)`.

- [ ] **Step 1: Criar `DampCode_API.Tests/DampCode_API.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <!-- Permite rodar os testes em máquinas que só têm runtimes mais novos que o .NET 8. -->
    <RollForward>Major</RollForward>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.11" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\DampCode_API\DampCode_API.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Adicionar à solution**

Run: `dotnet sln DampCode_API.slnx add DampCode_API.Tests/DampCode_API.Tests.csproj`
Expected: `DampCode_API.slnx` passa a listar `DampCode_API.Tests/DampCode_API.Tests.csproj`.

- [ ] **Step 3: Escrever o teste que falha — `DampCode_API.Tests/AccountRolesTests.cs`**

```csharp
using DampCode_API.Authentication;
using DampCode_API.Models;

namespace DampCode_API.Tests;

public sealed class AccountRolesTests
{
    [Theory]
    [InlineData(AccountType.Participant, "participante")]
    [InlineData(AccountType.Company, "empresa")]
    [InlineData(AccountType.Admin, "admin")]
    public void ToFrontRole_MapsEveryAccountType(AccountType accountType, string expected) =>
        Assert.Equal(expected, AccountRoles.ToFrontRole(accountType));

    [Fact]
    public void ToFrontRole_UnknownValue_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AccountRoles.ToFrontRole((AccountType)99));
}
```

- [ ] **Step 4: Rodar e ver falhar**

Run: `dotnet test DampCode_API.Tests --filter "FullyQualifiedName~AccountRolesTests"`
Expected: erro de compilação `The type or namespace name 'Authentication' does not exist`.

- [ ] **Step 5: Implementar `DampCode_API/Authentication/AccountRoles.cs`**

```csharp
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
```

- [ ] **Step 6: Usar o helper no `AuthService`**

Em `DampCode_API/Services/AuthService.cs`, adicionar `using DampCode_API.Authentication;` e substituir o método `Success` inteiro por:

```csharp
    private static AuthServiceResult Success(Account account, string name) =>
        new(true, new AuthResponseDto(account.Id, name, account.Email, AccountRoles.ToFrontRole(account.AccountType)));
```

- [ ] **Step 7: Rodar e ver passar**

Run: `dotnet test DampCode_API.Tests --filter "FullyQualifiedName~AccountRolesTests"`
Expected: `Passed: 4` (aprovado), `Failed: 0`.
Se aparecer "You must install or update .NET to run this application" para `Microsoft.NETCore.App 8.0`, rode com `DOTNET_ROLL_FORWARD=Major` no ambiente e registre isso no relatório da task.

- [ ] **Step 8: Commit**

```bash
git add DampCode_API.Tests DampCode_API.slnx DampCode_API/Authentication/AccountRoles.cs DampCode_API/Services/AuthService.cs
git commit -m "Test: cria projeto de testes e centraliza conversão de roles (corrige Admin)"
```

---

### Task 3: `JwtOptions` e `TokenService`

**Files:**
- Create: `DampCode_API/Authentication/JwtOptions.cs`
- Create: `DampCode_API/Services/ITokenService.cs`
- Rewrite: `DampCode_API/Services/TokenService.cs`
- Create: `DampCode_API.Tests/TestJwt.cs`
- Create: `DampCode_API.Tests/TokenServiceTests.cs`

**Interfaces:**
- Consumes: `AccountRoles.ClaimType` (Task 2).
- Produces:
  - `DampCode_API.Authentication.JwtOptions` — `SectionName = "Jwt"`, `MinimumSecretBytes = 32`, `InvalidSecretMessage`, propriedades `Issuer`, `Audience`, `Secret`, `ExpirationMinutes` (default 1440), `bool HasValidSecret()`, `SymmetricSecurityKey CreateSigningKey()`.
  - `DampCode_API.Services.ITokenService` — `TokenResult GenerateToken(Account account)`.
  - `DampCode_API.Services.TokenResult(string Token, DateTime ExpiresAt)`.
  - `DampCode_API.Services.TokenService(IOptions<JwtOptions> options)` — lança `InvalidOperationException` se o secret for inválido.
  - Teste: `DampCode_API.Tests.TestJwt` — `Secret`, `IOptions<JwtOptions> Options(int expirationMinutes = 1440, string? secret = null)`.

- [ ] **Step 1: Criar o helper de teste `DampCode_API.Tests/TestJwt.cs`**

```csharp
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
```

- [ ] **Step 2: Escrever os testes que falham — `DampCode_API.Tests/TokenServiceTests.cs`**

```csharp
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
```

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test DampCode_API.Tests --filter "FullyQualifiedName~TokenServiceTests"`
Expected: erro de compilação (`JwtOptions` não existe / `TokenService` sem construtor com `IOptions<JwtOptions>`).

- [ ] **Step 4: Criar `DampCode_API/Authentication/JwtOptions.cs`**

```csharp
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DampCode_API.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSecretBytes = 32;
    public const string InvalidSecretMessage =
        "Configure Jwt:Secret com pelo menos 32 bytes por User Secrets ou pela variável Jwt__Secret.";

    [Required]
    public string Issuer { get; set; } = string.Empty;
    [Required]
    public string Audience { get; set; } = string.Empty;
    // Validado por HasValidSecret para que a mensagem de erro seja sempre InvalidSecretMessage.
    public string Secret { get; set; } = string.Empty;
    [Range(1, 43200)]
    public int ExpirationMinutes { get; set; } = 1440;

    public bool HasValidSecret() => Encoding.UTF8.GetByteCount(Secret) >= MinimumSecretBytes;
    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(Secret));
}
```

- [ ] **Step 5: Criar `DampCode_API/Services/ITokenService.cs`**

```csharp
using DampCode_API.Models;

namespace DampCode_API.Services;

public interface ITokenService
{
    TokenResult GenerateToken(Account account);
}

public sealed record TokenResult(string Token, DateTime ExpiresAt);
```

- [ ] **Step 6: Substituir `DampCode_API/Services/TokenService.cs` por inteiro**

```csharp
using DampCode_API.Authentication;
using DampCode_API.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DampCode_API.Services;

public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new();

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (!_options.HasValidSecret()) throw new InvalidOperationException(JwtOptions.InvalidSecretMessage);
        _credentials = new SigningCredentials(_options.CreateSigningKey(), SecurityAlgorithms.HmacSha256);
    }

    public TokenResult GenerateToken(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.ExpirationMinutes);

        // Somente identificadores estáveis; nome, empresa e permissões são lidos do banco.
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = account.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = account.Email,
                [AccountRoles.ClaimType] = account.AccountType.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
            }
        });
        return new TokenResult(token, expiresAt);
    }
}
```

- [ ] **Step 7: Rodar e ver passar**

Run: `dotnet test DampCode_API.Tests --filter "FullyQualifiedName~TokenServiceTests"`
Expected: `Passed: 5`, `Failed: 0`.

- [ ] **Step 8: Commit**

```bash
git add DampCode_API/Authentication/JwtOptions.cs DampCode_API/Services/ITokenService.cs DampCode_API/Services/TokenService.cs DampCode_API.Tests/TestJwt.cs DampCode_API.Tests/TokenServiceTests.cs
git commit -m "Feat: TokenService com JwtOptions e claims mínimas"
```

---

### Task 4: `AccountTokenValidator`

**Files:**
- Create: `DampCode_API/Authentication/AccountTokenValidator.cs`
- Create: `DampCode_API.Tests/TestDb.cs`
- Create: `DampCode_API.Tests/AccountTokenValidatorTests.cs`

**Interfaces:**
- Produces:
  - `DampCode_API.Authentication.AccountTokenValidator(DampCodeDbContext dbContext)` com `Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)`. Lê as claims `sub` e `iat` (nomes curtos, pois `MapInboundClaims = false`).
  - Teste: `DampCode_API.Tests.TestDb.Create()` → `DampCodeDbContext` InMemory isolado.

- [ ] **Step 1: Criar `DampCode_API.Tests/TestDb.cs`**

```csharp
using DampCode_API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DampCode_API.Tests;

internal static class TestDb
{
    // InMemory ignora transações, check constraints e índices únicos; essas regras são do PostgreSQL.
    public static void Configure(DbContextOptionsBuilder builder, string databaseName) =>
        builder.UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));

    public static DampCodeDbContext Create()
    {
        var builder = new DbContextOptionsBuilder<DampCodeDbContext>();
        Configure(builder, Guid.NewGuid().ToString());
        return new DampCodeDbContext(builder.Options);
    }
}
```

- [ ] **Step 2: Escrever os testes que falham — `DampCode_API.Tests/AccountTokenValidatorTests.cs`**

```csharp
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
```

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test DampCode_API.Tests --filter "FullyQualifiedName~AccountTokenValidatorTests"`
Expected: erro de compilação `AccountTokenValidator` não encontrado.

- [ ] **Step 4: Implementar `DampCode_API/Authentication/AccountTokenValidator.cs`**

```csharp
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
```

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test DampCode_API.Tests --filter "FullyQualifiedName~AccountTokenValidatorTests"`
Expected: `Passed: 8`, `Failed: 0`.

- [ ] **Step 6: Commit**

```bash
git add DampCode_API/Authentication/AccountTokenValidator.cs DampCode_API.Tests/TestDb.cs DampCode_API.Tests/AccountTokenValidatorTests.cs
git commit -m "Feat: valida estado da conta para cada token recebido"
```

---

### Task 5: Token na resposta de autenticação, projeção única e `GetAccountAsync`

**Files:**
- Modify: `DampCode_API/Dto/AuthResponseDto.cs`
- Create: `DampCode_API/Dto/UserResponseProjection.cs`
- Modify: `DampCode_API/Services/IAuthService.cs`
- Modify: `DampCode_API/Services/AuthService.cs`
- Modify: `DampCode_API/Controllers/UsersController.cs`
- Create: `DampCode_API.Tests/AuthServiceTests.cs`

**Interfaces:**
- Consumes: `ITokenService`, `TokenResult` (Task 3); `AccountRoles.ToFrontRole` (Task 2); `TestDb`, `TestJwt` (Tasks 3–4).
- Produces:
  - `AuthResponseDto(Guid Id, string Name, string Email, string Role, string Token, DateTime ExpiresAt)`.
  - `UserResponseProjection.SelectUserResponse(this IQueryable<Account> accounts)` → `IQueryable<UserResponseDto>`.
  - `IAuthService.GetAccountAsync(Guid accountId, CancellationToken cancellationToken)` → `Task<UserResponseDto?>` (null se não existe ou está deletada).
  - `AuthService(DampCodeDbContext dbContext, ITokenService tokenService)`.

Observação: após esta task o `Program.cs` ainda não registra `ITokenService`; o build passa, mas a API só volta a subir depois da Task 6.

- [ ] **Step 1: Escrever os testes que falham — `DampCode_API.Tests/AuthServiceTests.cs`**

```csharp
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
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test DampCode_API.Tests --filter "FullyQualifiedName~AuthServiceTests"`
Expected: erro de compilação (`AuthService` sem construtor com `ITokenService`, `Token`/`ExpiresAt`/`GetAccountAsync` inexistentes).

- [ ] **Step 3: Substituir `DampCode_API/Dto/AuthResponseDto.cs`**

```csharp
namespace DampCode_API.Dto;

public sealed record AuthResponseDto(Guid Id, string Name, string Email, string Role, string Token, DateTime ExpiresAt);
```

- [ ] **Step 4: Criar `DampCode_API/Dto/UserResponseProjection.cs`**

```csharp
using DampCode_API.Authentication;
using DampCode_API.Models;

namespace DampCode_API.Dto;

public static class UserResponseProjection
{
    // Conta sem perfil nem empresa (ex.: Admin) usa o e-mail como nome.
    public static IQueryable<UserResponseDto> SelectUserResponse(this IQueryable<Account> accounts) =>
        accounts.Select(account => new UserResponseDto(
            account.Id,
            (account.AccountType == AccountType.Company ? account.PrimaryCompany!.Name : account.UserProfile!.Name) ?? account.Email,
            account.Email,
            AccountRoles.ToFrontRole(account.AccountType),
            account.UserProfile == null ? null : account.UserProfile.Level,
            account.UserProfile == null ? null : account.UserProfile.Xp));
}
```

- [ ] **Step 5: Substituir `DampCode_API/Services/IAuthService.cs`**

```csharp
using DampCode_API.Dto;

namespace DampCode_API.Services;

public interface IAuthService
{
    Task<AuthServiceResult> RegisterParticipantAsync(ParticipantDto dto, CancellationToken cancellationToken);
    Task<AuthServiceResult> RegisterCompanyAsync(CompanyDto dto, CancellationToken cancellationToken);
    Task<AuthServiceResult> LoginAsync(LoginDto dto, CancellationToken cancellationToken);
    Task<UserResponseDto?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken);
}

public sealed record AuthServiceResult(bool Succeeded, AuthResponseDto? Value = null, AuthServiceError Error = AuthServiceError.None, string? Message = null);
public enum AuthServiceError { None, Conflict, InvalidCredentials, InactiveAccount }
```

- [ ] **Step 6: Ajustar `DampCode_API/Services/AuthService.cs`**

Trocar a declaração da classe:

```csharp
public sealed class AuthService(DampCodeDbContext dbContext, ITokenService tokenService) : IAuthService
```

Adicionar logo após `LoginAsync`:

```csharp
    public Task<UserResponseDto?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.Accounts.AsNoTracking()
            .Where(account => account.Id == accountId && account.DeletedAt == null)
            .SelectUserResponse()
            .SingleOrDefaultAsync(cancellationToken);
```

Substituir o método `Success` (versão da Task 2) por:

```csharp
    private AuthServiceResult Success(Account account, string name)
    {
        var token = tokenService.GenerateToken(account);
        return new(true, new AuthResponseDto(account.Id, name, account.Email,
            AccountRoles.ToFrontRole(account.AccountType), token.Token, token.ExpiresAt));
    }
```

As demais linhas do arquivo permanecem iguais.

- [ ] **Step 7: Substituir `DampCode_API/Controllers/UsersController.cs`** (o `[Authorize]` entra na Task 6)

```csharp
using DampCode_API.Data;
using DampCode_API.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class UsersController(DampCodeDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetAllUsers(CancellationToken cancellationToken)
    {
        var users = await dbContext.Accounts.AsNoTracking().Where(account => account.DeletedAt == null)
            .SelectUserResponse()
            .ToListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponseDto>> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Accounts.AsNoTracking().Where(account => account.Id == id && account.DeletedAt == null)
            .SelectUserResponse()
            .SingleOrDefaultAsync(cancellationToken);
        return user is null ? NotFound(new { message = "Usuário não encontrado." }) : Ok(user);
    }
}
```

- [ ] **Step 8: Rodar e ver passar**

Run: `dotnet test DampCode_API.Tests`
Expected: todos os testes passam (`Failed: 0`), incluindo os 10 de `AuthServiceTests`.

- [ ] **Step 9: Commit**

```bash
git add DampCode_API/Dto DampCode_API/Services/IAuthService.cs DampCode_API/Services/AuthService.cs DampCode_API/Controllers/UsersController.cs DampCode_API.Tests/AuthServiceTests.cs
git commit -m "Feat: retorna JWT na autenticação e unifica projeção de usuários"
```

---

### Task 6: Pipeline JWT, `[Authorize]`, `GET /api/Auth/me` e Swagger

**Files:**
- Modify: `DampCode_API/DampCode_API.csproj`
- Create: `DampCode_API/Authentication/JwtAuthenticationExtensions.cs`
- Modify: `DampCode_API/Program.cs`
- Modify: `DampCode_API/Controllers/AuthController.cs`
- Modify: `DampCode_API/Controllers/UsersController.cs` (atributo)
- Create: `DampCode_API.Tests/ApiFactory.cs`
- Create: `DampCode_API.Tests/AuthenticationEndpointTests.cs`

**Interfaces:**
- Consumes: `JwtOptions`, `TokenService`, `AccountTokenValidator`, `AccountRoles.ClaimType`, `IAuthService.GetAccountAsync`, `TestDb.Configure`, `TestJwt`.
- Produces:
  - `JwtAuthenticationExtensions.AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)`.
  - Rota `GET /api/Auth/me` → `200 UserResponseDto` | `401` | `404`.
  - `public partial class Program;` (para `WebApplicationFactory<Program>`).
  - Teste: `ApiFactory(string jwtSecret = TestJwt.Secret)`.

- [ ] **Step 1: Adicionar pacotes**

```bash
dotnet add DampCode_API/DampCode_API.csproj package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.11
dotnet add DampCode_API/DampCode_API.csproj package Microsoft.IdentityModel.Protocols.OpenIdConnect --version 8.22.0
```

O segundo pacote alinha a versão do IdentityModel usada pelo JwtBearer com a do `System.IdentityModel.Tokens.Jwt` 8.22.0 já presente; versões 7.x e 8.x misturadas falham em tempo de execução.

- [ ] **Step 2: Criar `DampCode_API.Tests/ApiFactory.cs`**

```csharp
using DampCode_API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DampCode_API.Tests;

public sealed class ApiFactory(string jwtSecret) : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public ApiFactory() : this(TestJwt.Secret) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" evita carregar os User Secrets reais do desenvolvedor.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PostgreSql", "Host=nao-usado-nos-testes");
        builder.UseSetting("Jwt:Secret", jwtSecret);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<DampCodeDbContext>>();
            services.AddDbContext<DampCodeDbContext>(options => TestDb.Configure(options, _databaseName));
        });
    }
}
```

- [ ] **Step 3: Escrever os testes que falham — `DampCode_API.Tests/AuthenticationEndpointTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using DampCode_API.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Swagger;

namespace DampCode_API.Tests;

public sealed class AuthenticationEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<AuthResponseDto> RegisterAsync(HttpClient client)
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var response = await client.PostAsJsonAsync("/api/Auth/register/participante", new
        {
            name = "Ana Souza",
            email,
            password = "SenhaForte123",
            tecnologias = new[] { "React" }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }

    private static HttpRequestMessage Get(string url, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task Users_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/Users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Users_WithToken_Returns200()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);

        var response = await client.SendAsync(Get("/api/Users", auth.Token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsPreviousFieldsAndToken()
    {
        var client = factory.CreateClient();
        var registered = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync("/api/Auth/login", new { email = registered.Email, password = "SenhaForte123" });
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(registered.Id, body!.Id);
        Assert.Equal("Ana Souza", body.Name);
        Assert.Equal("participante", body.Role);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.True(body.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Me_ReturnsAuthenticatedAccount()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);

        var response = await client.SendAsync(Get("/api/Auth/me", auth.Token));
        var me = await response.Content.ReadFromJsonAsync<UserResponseDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(auth.Id, me!.Id);
        Assert.Equal("participante", me.Role);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithTokenSignedByAnotherSecret_Returns401()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);
        var account = new Account { Id = auth.Id, Email = auth.Email, NormalizedEmail = auth.Email.ToUpperInvariant(), PasswordHash = "x" };
        var forged = new TokenService(TestJwt.Options(secret: "outro-segredo-qualquer-com-mais-de-32-bytes")).GenerateToken(account);

        var response = await client.SendAsync(Get("/api/Auth/me", forged.Token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithExpiredToken_Returns401()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);
        var issuedAt = DateTime.UtcNow.AddMinutes(-30);
        var expired = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "Api",
            Audience = "Front",
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = DateTime.UtcNow.AddMinutes(-10),
            SigningCredentials = new SigningCredentials(TestJwt.Options().Value.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object> { ["sub"] = auth.Id.ToString(), ["role"] = "Participant" }
        });

        var response = await client.SendAsync(Get("/api/Auth/me", expired));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_AfterAccountIsSuspended_Returns401()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DampCodeDbContext>();
            dbContext.Accounts.Single(account => account.Id == auth.Id).Status = AccountStatus.Suspended;
            await dbContext.SaveChangesAsync();
        }

        var response = await client.SendAsync(Get("/api/Auth/me", auth.Token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Swagger_DeclaresBearerScheme()
    {
        var document = factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        Assert.True(document.Components.SecuritySchemes.TryGetValue("Bearer", out var scheme));
        Assert.Equal("bearer", scheme.Scheme);
        Assert.Equal("JWT", scheme.BearerFormat);
        Assert.Contains("ParticipanteDto", document.Components.Schemas.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("curta")]
    public void Startup_WithInvalidSecret_FailsWithClearMessage(string secret)
    {
        using var invalidFactory = new ApiFactory(secret);

        var exception = Assert.ThrowsAny<Exception>(() => invalidFactory.CreateClient());

        Assert.Contains("Jwt:Secret", exception.ToString());
    }
}
```

- [ ] **Step 4: Rodar e ver falhar**

Run: `dotnet test DampCode_API.Tests --filter "FullyQualifiedName~AuthenticationEndpointTests"`
Expected: erro de compilação `'Program' is inaccessible due to its protection level` (ou equivalente).

- [ ] **Step 5: Criar `DampCode_API/Authentication/JwtAuthenticationExtensions.cs`**

```csharp
using DampCode_API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DampCode_API.Authentication;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.HasValidSecret(), JwtOptions.InvalidSecretMessage)
            .ValidateOnStart();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<AccountTokenValidator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = AccountRoles.ClaimType
                };
                bearer.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var validator = context.HttpContext.RequestServices.GetRequiredService<AccountTokenValidator>();
                        if (!await validator.IsValidAsync(context.Principal!, context.HttpContext.RequestAborted))
                            context.Fail("Conta indisponível para este token.");
                    }
                };
            });
        return services;
    }
}
```

- [ ] **Step 6: Substituir `DampCode_API/Program.cs`**

```csharp
using DampCode_API.Authentication;
using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type == typeof(ParticipantDto) ? "ParticipanteDto"
        : type == typeof(CompanyDto) ? "EmpresaDto"
        : type == typeof(CreateHackathonDto) ? "CreateHackathonDTO"
        : type.Name);
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Cole apenas o token retornado pelo login, sem o prefixo Bearer."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddDbContext<DampCodeDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("PostgreSql");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Configure a connection string 'PostgreSql' por User Secrets ou pela variável " +
            "ConnectionStrings__PostgreSql.");
    }
    options.UseNpgsql(connectionString);
});
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowReact");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

// Exposto para os testes de integração (WebApplicationFactory<Program>).
public partial class Program;
```

- [ ] **Step 7: Substituir `DampCode_API/Controllers/AuthController.cs`**

```csharp
using System.Security.Claims;
using DampCode_API.Dto;
using DampCode_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DampCode_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register/participante")]
    public async Task<IActionResult> RegisterParticipant([FromBody] ParticipantDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await authService.RegisterParticipantAsync(dto, cancellationToken), StatusCodes.Status201Created);

    [AllowAnonymous]
    [HttpPost("register/empresa")]
    public async Task<IActionResult> RegisterCompany([FromBody] CompanyDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await authService.RegisterCompanyAsync(dto, cancellationToken), StatusCodes.Status201Created);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await authService.LoginAsync(dto, cancellationToken), StatusCodes.Status200OK);

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponseDto>> Me(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var accountId)) return Unauthorized();
        var user = await authService.GetAccountAsync(accountId, cancellationToken);
        return user is null ? NotFound(new { message = "Usuário não encontrado." }) : Ok(user);
    }

    private IActionResult ToActionResult(AuthServiceResult result, int successStatusCode)
    {
        if (result.Succeeded) return StatusCode(successStatusCode, result.Value);
        var problem = new ProblemDetails { Detail = result.Message };
        return result.Error switch
        {
            AuthServiceError.Conflict => Conflict(problem),
            AuthServiceError.InvalidCredentials => Unauthorized(problem),
            AuthServiceError.InactiveAccount => StatusCode(StatusCodes.Status403Forbidden, problem),
            _ => StatusCode(StatusCodes.Status500InternalServerError, problem)
        };
    }
}
```

Atenção: `[AllowAnonymous]` vai em cada ação pública, **não** na classe — na classe ele anularia o `[Authorize]` do `me`.

- [ ] **Step 8: Proteger `UsersController`**

Em `DampCode_API/Controllers/UsersController.cs`, adicionar `using Microsoft.AspNetCore.Authorization;` e o atributo `[Authorize]` acima de `[Route("api/[controller]")]`.

- [ ] **Step 9: Rodar todos os testes**

Run: `dotnet test DampCode_API.Tests`
Expected: `Failed: 0`; `AuthenticationEndpointTests` com 11 aprovados.

- [ ] **Step 10: Commit**

```bash
git add DampCode_API/DampCode_API.csproj DampCode_API/Authentication/JwtAuthenticationExtensions.cs DampCode_API/Program.cs DampCode_API/Controllers DampCode_API.Tests/ApiFactory.cs DampCode_API.Tests/AuthenticationEndpointTests.cs
git commit -m "Feat: ativa autenticação JWT, protege rotas, adiciona /api/Auth/me e Bearer no Swagger"
```

---

### Task 7: Migration inicial do núcleo

**Files:**
- Create: `.config/dotnet-tools.json` (via comando)
- Create: `DampCode_API/Data/DampCodeDbContextFactory.cs`
- Generate: `DampCode_API/Data/Migrations/*_InitialCore.cs`, `*_InitialCore.Designer.cs`, `DampCodeDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: `DampCodeDbContext` e as 7 configurações existentes.
- Produces: migration `InitialCore`.

Sem teste automatizado: a verificação é a revisão do SQL gerado (Step 5) e o build/test completo.

- [ ] **Step 1: Instalar o `dotnet-ef` como ferramenta local**

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 8.0.11
```

Expected: `.config/dotnet-tools.json` criado com `dotnet-ef` 8.0.11.

- [ ] **Step 2: Criar `DampCode_API/Data/DampCodeDbContextFactory.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DampCode_API.Data;

// Usada somente pelas ferramentas do EF Core (dotnet ef). A API continua usando a configuração do Program.cs.
public sealed class DampCodeDbContextFactory : IDesignTimeDbContextFactory<DampCodeDbContext>
{
    // Exemplo local sem senha; gerar migrations não abre conexão.
    private const string SampleConnectionString = "Host=localhost;Port=5432;Database=dampcode_dev;Username=postgres";

    public DampCodeDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DampCodeDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("PostgreSql");
        var options = new DbContextOptionsBuilder<DampCodeDbContext>()
            .UseNpgsql(string.IsNullOrWhiteSpace(connectionString) ? SampleConnectionString : connectionString)
            .Options;
        return new DampCodeDbContext(options);
    }
}
```

- [ ] **Step 3: Gerar a migration**

```bash
DOTNET_ROLL_FORWARD=Major dotnet ef migrations add InitialCore --project DampCode_API --startup-project DampCode_API --output-dir Data/Migrations
```

Expected: `Done.` e três arquivos novos em `DampCode_API/Data/Migrations/`.

- [ ] **Step 4: Gerar o script SQL para revisão (não versionar)**

```bash
DOTNET_ROLL_FORWARD=Major dotnet ef migrations script --project DampCode_API --startup-project DampCode_API --output "$TMPDIR/initial-core.sql"
```

(Em PowerShell, use `$env:TEMP\initial-core.sql`; o arquivo fica fora do repositório.)

- [ ] **Step 5: Revisar o SQL gerado** — conferir cada item e registrar no relatório da task:

- exatamente 7 `CREATE TABLE`: `accounts`, `user_profiles`, `companies`, `company_memberships`, `technologies`, `user_technologies`, `company_technologies`; **nenhuma** tabela de hackathons ou submissions;
- PKs: `pk_accounts`, `pk_user_profiles`, `pk_companies`, `pk_company_memberships`, `pk_technologies`, `pk_user_technologies (user_profile_id, technology_id)`, `pk_company_technologies (company_id, technology_id)`;
- FKs: `fk_user_profiles_accounts` (CASCADE), `fk_companies_primary_accounts` (RESTRICT), `fk_company_memberships_companies` (RESTRICT), `fk_company_memberships_accounts` (RESTRICT), `fk_user_technologies_user_profiles` (CASCADE), `fk_user_technologies_technologies` (RESTRICT), `fk_company_technologies_companies` (CASCADE), `fk_company_technologies_technologies` (RESTRICT);
- checks: `ck_accounts_account_type`, `ck_accounts_status`, `ck_companies_cnpj`, `ck_companies_verification_status`, `ck_companies_reputation_score`, `ck_user_profiles_level`, `ck_user_profiles_xp`, `ck_user_profiles_visibility`;
- índices únicos: `ux_accounts_normalized_email`, `ux_companies_cnpj`, `ux_companies_primary_account_id`, `ux_user_profiles_account_id`, `ux_technologies_normalized_name`, `ux_company_memberships_history`;
- índice parcial: `CREATE UNIQUE INDEX ux_company_memberships_active_account ON company_memberships (account_id) WHERE ended_at IS NULL;`
- colunas de data como `timestamp with time zone`; `id` como `uuid` sem default (GUID gerado pela aplicação).

Qualquer divergência: parar e reportar (não editar a migration à mão).

- [ ] **Step 6: Build e testes completos**

Run: `dotnet build DampCode_API.slnx -nologo -v q` e depois `dotnet test DampCode_API.Tests`
Expected: `0 Erro(s)`; `Failed: 0`.

- [ ] **Step 7: Commit**

```bash
git add .config/dotnet-tools.json DampCode_API/Data/DampCodeDbContextFactory.cs DampCode_API/Data/Migrations
git commit -m "Feat: migration InitialCore do núcleo de contas e empresas"
```

---

### Task 8: README de configuração e teste

**Files:**
- Rewrite: `README.md`

- [ ] **Step 1: Substituir `README.md`**

````markdown
# DampCode API

API ASP.NET Core 8 da DampCode (PostgreSQL via EF Core, autenticação JWT).

## Configuração local

Os segredos ficam em User Secrets, nunca no repositório. Na pasta `DampCode_API/`:

```bash
dotnet user-secrets set "ConnectionStrings:PostgreSql" "Host=localhost;Port=5432;Database=dampcode_dev;Username=postgres;Password=<sua-senha>"
dotnet user-secrets set "Jwt:Secret" "<texto aleatório com pelo menos 32 caracteres>"
```

Em produção use as variáveis `ConnectionStrings__PostgreSql` e `Jwt__Secret`.
Sem `Jwt:Secret` válido a API não inicia.

`Jwt:Issuer`, `Jwt:Audience` e `Jwt:ExpirationMinutes` (padrão 1440 = 24h) ficam em `appsettings.json`.

## Banco de dados

Na raiz do repositório:

```bash
dotnet tool restore
dotnet ef database update --project DampCode_API --startup-project DampCode_API
```

Para criar uma nova migration:

```bash
dotnet ef migrations add <Nome> --project DampCode_API --startup-project DampCode_API --output-dir Data/Migrations
```

> Se a máquina só tiver runtimes mais novos que o .NET 8, prefixe os comandos `dotnet ef` com `DOTNET_ROLL_FORWARD=Major`.

## Testando a autenticação no Swagger

1. `dotnet run --project DampCode_API` e abra `/swagger`.
2. Chame `POST /api/Auth/login` (ou um dos cadastros) e copie o campo `token` da resposta.
3. Clique em **Authorize** e cole apenas o token (sem `Bearer `).
4. Chame `GET /api/Auth/me` ou `GET /api/Users`.

Tokens expiram em 24h e deixam de valer se a conta for suspensa, desativada, excluída ou tiver as credenciais alteradas.

## Testes

```bash
dotnet test DampCode_API.Tests
```
````

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "Docs: README com configuração de secrets, migrations e teste do JWT"
```

---

## Verificação final

- [ ] `dotnet build DampCode_API.slnx -nologo -v q` → `0 Erro(s)`.
- [ ] `dotnet test DampCode_API.Tests` → `Failed: 0`.
- [ ] `git grep -n -i "password=" -- ":!README.md" ":!docs"` e `git grep -n "\"Secret\"" -- "*.json"` → nenhum resultado (nenhuma senha ou secret versionado).
- [ ] Conferir os critérios de aceite da seção 8 da spec, um por um.
