# Empresas, funcionários e verificação — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convites de funcionários (24h, só para contas de participante existentes), aceite/recusa/revogação/remoção, verificação da empresa por Admin, regra única de acesso à empresa (`ICompanyAccessService`) e telas mínimas no front.

**Architecture:** Nova entidade `CompanyInvitation` no PostgreSQL. `CompanyAccessService` resolve "qual empresa esta conta gerencia" (conta principal ou membership ativo). `CompanyService` concentra as regras de convite, funcionários e verificação, devolvendo `CompanyServiceResult<T>`; `CompaniesController` é fino e só mapeia erros para HTTP. No front, dois componentes novos nos dashboards e rotas novas na API simulada.

**Tech Stack:** ASP.NET Core 8 (net8.0), EF Core 8.0.11 + Npgsql, xUnit + EF InMemory + `Microsoft.AspNetCore.Mvc.Testing`; React 19 + Vite + Tailwind.

**Spec:** `docs/superpowers/specs/2026-10-28-empresas-e-funcionarios-design.md`

## Global Constraints

- Branch `feat/empresas-e-funcionarios` nos dois repositórios, criada a partir de `master` (API) e `main` (front) atualizadas. Não commitar direto na principal.
- Não renomear nada existente (classes, métodos, rotas, DTOs, colunas, componentes). Mudanças só aditivas, exceto `Company` (nova coleção `Invitations`), `DampCodeDbContext` (novo `DbSet`), `Program.cs` (registros), dashboards e mock.
- Estilo C#: file-scoped namespace, primary constructors, `sealed`, `CancellationToken cancellationToken`, records para DTOs de saída, classes com DataAnnotations para DTOs de entrada, mensagens em português, `ProblemDetails { Detail = Message }`.
- Configuração EF: tabela e colunas em `snake_case`; constraints `pk_`/`fk_`/`ix_`/`ck_`; enums `HasConversion<string>().HasMaxLength(32)`.
- `Normalize(email)` = `email.Trim().ToUpperInvariant()` (mesmo do `AuthService`).
- Convite expira em exatamente `CreatedAt + 24h`; expirado = `Status == Pending && ExpiresAt <= DateTime.UtcNow`.
- Máquina só tem SDK .NET 10: comandos `dotnet ef` com `DOTNET_ROLL_FORWARD=Major`; testes com `dotnet test DampCode_API.Tests`.
- Front: `fetch` direto em `https://localhost:7092`, header `Authorization: Bearer ${user.token}` só nas chamadas novas; comentários em português no tom dos arquivos vizinhos.
- Commits no padrão do repositório (`Feat:`, `Fix:`, `Test:`, `Docs:`), em português, sem linha de coautoria (pedido do usuário para manter o histórico com a cara do time).
- Mensagens de erro exatamente como na seção 5.3 do spec.

## Review Focus

- E-mail do convite com caixa/espaços diferentes do cadastro (`" Maria@Exemplo.com "`) → encontra a conta e o participante vê o convite. (Task 3 e Task 4)
- Funcionário removido que recebe novo convite da mesma empresa → convite criado e aceite funciona, gerando um segundo membership (histórico preservado). (Task 4)
- Participante que já é funcionário ativo tenta aceitar convite de outra empresa → 409 "Você já está vinculado a uma empresa." (Task 4)
- Funcionário (não principal) chamando `POST /me/invitations` ou `DELETE /me/employees/{id}` → 403. (Task 5)
- Empresa com `DeletedAt` preenchido → `GET /api/Companies/me` 404 para conta principal e funcionário. (Task 2)

---

## File Structure

**API (`DampCode_API/`)**

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `Models/CompanyInvitation.cs` | Create | entidade + `CompanyInvitationStatus` |
| `Models/Company.cs` | Modify | `ICollection<CompanyInvitation> Invitations` |
| `Data/Configurations/CompanyInvitationConfiguration.cs` | Create | mapeamento `company_invitations` |
| `Data/DampCodeDbContext.cs` | Modify | `DbSet<CompanyInvitation> CompanyInvitations` |
| `Data/Migrations/*_CompanyInvitations.cs` | Create (gerado) | migration |
| `Dto/CompanyAccessDto.cs`, `CompanyEmployeeDto.cs`, `CompanyInvitationDto.cs`, `ReceivedInvitationDto.cs`, `CompanyVerificationDto.cs` | Create | records de saída |
| `Dto/InviteEmployeeDto.cs`, `UpdateVerificationDto.cs` | Create | entrada com DataAnnotations |
| `Services/ICompanyAccessService.cs`, `CompanyAccessService.cs` | Create | empresa gerenciada pela conta |
| `Services/ICompanyService.cs`, `CompanyService.cs` | Create | convites, funcionários, verificação; `CompanyServiceResult<T>`, `CompanyServiceError` |
| `Controllers/CompaniesController.cs` | Create | rotas da seção 5.1 |
| `Program.cs` | Modify | registrar os dois services |
| `../README.md` | Modify | seção "Conta Admin" |

**Testes (`DampCode_API.Tests/`)**: `CompanyAccessServiceTests.cs`, `CompanyServiceTests.cs`, `CompaniesEndpointTests.cs`, mais `CompanySeed.cs` (helper interno para criar conta participante/empresa no `DampCodeDbContext`).

**Front (`Damp-Code-Front/src/`)**

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `mock/seed.js` | Modify | `companies`, `memberships`, `invitations`, mais um participante |
| `mock/installMockApi.js` | Modify | token mock no login/cadastro + rotas de `/api/Companies` |
| `app-components/company-dashboard/ManageEmployees.jsx` | Create | funcionários, convites, convidar/revogar/remover |
| `Pages/company/CompanyDashboard.jsx` | Modify | busca `Companies/me`, aviso de verificação, oculta seções para funcionário |
| `app-components/participant-dashboard/ReceivedInvitations.jsx` | Create | convites recebidos, aceitar/recusar |
| `Pages/participant/ParticipantDashboard.jsx` | Modify | caixa de convites + botão "Painel da empresa" |

---

### Task 1: Entidade `CompanyInvitation`, configuração e migration

**Files:**
- Create: `DampCode_API/Models/CompanyInvitation.cs`, `DampCode_API/Data/Configurations/CompanyInvitationConfiguration.cs`, migration gerada
- Modify: `DampCode_API/Models/Company.cs`, `DampCode_API/Data/DampCodeDbContext.cs`
- Test: `DampCode_API.Tests/CompanyServiceTests.cs` (primeiro teste), `DampCode_API.Tests/CompanySeed.cs`

**Interfaces:**
- Produces: `CompanyInvitation` com propriedades da seção 4.1 do spec (`Id, CompanyId, InvitedByAccountId, Email, NormalizedEmail, JobTitle, Status, ExpiresAt, RespondedAt, CreatedAt, UpdatedAt`, navegações `Company`, `InvitedByAccount`); `enum CompanyInvitationStatus { Pending, Accepted, Declined, Revoked }`; `DampCodeDbContext.CompanyInvitations`.
- Produces: `internal static class CompanySeed` com `Task<Account> ParticipantAsync(DampCodeDbContext db, string email, string name = "Maria Lima")` e `Task<Company> CompanyAsync(DampCodeDbContext db, string email = "empresa@exemplo.com", CompanyVerificationStatus status = CompanyVerificationStatus.Pending)` (cria `Account` + `UserProfile` / `Account` + `Company` com CNPJ aleatório de 14 dígitos, `PasswordHash = "x"`).

- [ ] **Step 1: Criar branch** `git switch -c feat/empresas-e-funcionarios` no repositório da API.
- [ ] **Step 2: Teste de persistência**

```csharp
[Fact]
public async Task CompanyInvitation_PersistsWithCompany()
{
    var company = await CompanySeed.CompanyAsync(_dbContext);
    _dbContext.CompanyInvitations.Add(new CompanyInvitation
    {
        CompanyId = company.Id, InvitedByAccountId = company.PrimaryAccountId,
        Email = "maria@exemplo.com", NormalizedEmail = "MARIA@EXEMPLO.COM",
        ExpiresAt = DateTime.UtcNow.AddHours(24)
    });
    await _dbContext.SaveChangesAsync();
    Assert.Equal(CompanyInvitationStatus.Pending, _dbContext.CompanyInvitations.Single().Status);
}
```

- [ ] **Step 3: Rodar** `dotnet test DampCode_API.Tests --filter CompanyInvitation_PersistsWithCompany` → falha de compilação (`CompanyInvitation` não existe).
- [ ] **Step 4: Implementar** entidade, `Company.Invitations`, `DbSet` e `CompanyInvitationConfiguration` (tabela `company_invitations`, `pk_company_invitations`, `ck_company_invitations_status` com `status IN ('Pending', 'Accepted', 'Declined', 'Revoked')`, `email`/`normalized_email` max 254, `job_title` max 150, índices `ix_company_invitations_normalized_email` e `ix_company_invitations_company_id`, FKs `fk_company_invitations_companies` (com `WithMany(company => company.Invitations)`) e `fk_company_invitations_accounts` (sem coleção inversa), ambos `Restrict`). Seguir `CompanyMembershipConfiguration` como modelo.
- [ ] **Step 5: Rodar o teste** → PASS. Rodar `dotnet test DampCode_API.Tests` → todos passam.
- [ ] **Step 6: Gerar migration** `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add CompanyInvitations --project DampCode_API --startup-project DampCode_API --output-dir Data/Migrations`. Conferir que o `Up` só cria `company_invitations` e seus índices/constraints. Se não houver connection string configurada, parar e pedir ao usuário.
- [ ] **Step 7: Commit** `Feat: entidade CompanyInvitation e migration de convites de funcionários`.

### Task 2: `ICompanyAccessService`

**Files:**
- Create: `Services/ICompanyAccessService.cs`, `Services/CompanyAccessService.cs`, `Dto/CompanyAccessDto.cs`
- Test: `DampCode_API.Tests/CompanyAccessServiceTests.cs`

**Interfaces:**
- Produces: `public sealed record CompanyAccessDto(Guid CompanyId, string Name, string VerificationStatus, bool IsPrimaryAccount, bool CanPublish);`
- Produces: `Task<CompanyAccessDto?> ICompanyAccessService.GetManagedCompanyAsync(Guid accountId, CancellationToken cancellationToken)`; `CompanyAccessService(DampCodeDbContext dbContext)`.

- [ ] **Step 1: Testes**
  - `GetManagedCompany_PrimaryAccount_ReturnsCompanyAsPrimary` → `IsPrimaryAccount == true`, `Name` da empresa.
  - `GetManagedCompany_ActiveEmployee_ReturnsCompanyAsEmployee` → membership com `EndedAt == null` → `IsPrimaryAccount == false`.
  - `GetManagedCompany_RemovedEmployee_ReturnsNull` → membership com `EndedAt` preenchido.
  - `GetManagedCompany_ParticipantWithoutCompany_ReturnsNull`.
  - `GetManagedCompany_DeletedCompany_ReturnsNull` → `DeletedAt` preenchido, para principal e funcionário.
  - `GetManagedCompany_CanPublishOnlyWhenVerified` → `[Theory]` com `Pending → false`, `Verified → true`, `Rejected → false`; `VerificationStatus` igual a `status.ToString()`.
- [ ] **Step 2: Rodar** → falha de compilação.
- [ ] **Step 3: Implementar** com `AsNoTracking`: primeiro procura `Companies` por `PrimaryAccountId`, depois `CompanyMemberships` com `EndedAt == null`; ambos filtrando `DeletedAt == null` da empresa.
- [ ] **Step 4: Rodar** `dotnet test DampCode_API.Tests` → PASS.
- [ ] **Step 5: Commit** `Feat: serviço de acesso à empresa da conta`.

### Task 3: `CompanyService` — convidar, listar e revogar convites

**Files:**
- Create: `Services/ICompanyService.cs`, `Services/CompanyService.cs`, `Dto/InviteEmployeeDto.cs`, `Dto/CompanyInvitationDto.cs`
- Test: `DampCode_API.Tests/CompanyServiceTests.cs`

**Interfaces:**
- Consumes: `CompanySeed` (Task 1).
- Produces:
  - `public sealed record CompanyServiceResult<T>(bool Succeeded, T? Value = default, CompanyServiceError Error = CompanyServiceError.None, string? Message = null);`
  - `public enum CompanyServiceError { None, NotFound, Forbidden, Conflict, Expired, Validation }`
  - `public sealed class InviteEmployeeDto { [Required, EmailAddress, StringLength(254)] public required string Email { get; set; } [StringLength(150)] public string? JobTitle { get; set; } }`
  - `public sealed record CompanyInvitationDto(Guid Id, string Email, string? JobTitle, DateTime ExpiresAt, DateTime CreatedAt);`
  - `CompanyService(DampCodeDbContext dbContext)` com:
    - `Task<CompanyServiceResult<CompanyInvitationDto>> InviteEmployeeAsync(Guid accountId, InviteEmployeeDto dto, CancellationToken cancellationToken)`
    - `Task<CompanyServiceResult<IReadOnlyList<CompanyInvitationDto>>> GetSentInvitationsAsync(Guid accountId, CancellationToken cancellationToken)`
    - `Task<CompanyServiceResult<bool>> RevokeInvitationAsync(Guid accountId, Guid invitationId, CancellationToken cancellationToken)`
  - Helper privado `Task<Company?> GetPrimaryCompanyAsync(Guid accountId, CancellationToken)` (empresa com `PrimaryAccountId == accountId` e `DeletedAt == null`); usado também na Task 4.

- [ ] **Step 1: Testes** (mensagens exatas da seção 5.3 do spec):
  - `InviteEmployee_ValidParticipant_CreatesPendingInvitationFor24Hours` → `ExpiresAt - CreatedAt == TimeSpan.FromHours(24)`, `NormalizedEmail == "MARIA@EXEMPLO.COM"`, `JobTitle` copiado.
  - `InviteEmployee_EmailWithDifferentCaseAndSpaces_FindsAccount` → `" Maria@Exemplo.com "` → sucesso, `Email == "Maria@Exemplo.com"`.
  - `InviteEmployee_NotPrimaryAccount_ReturnsForbidden` → chamado por participante.
  - `InviteEmployee_OwnEmail_ReturnsValidation`.
  - `InviteEmployee_EmailWithoutAccount_ReturnsNotFound` → mensagem "Nenhum participante encontrado com este e-mail. Peça para a pessoa criar a conta primeiro."
  - `InviteEmployee_CompanyAccountEmail_ReturnsConflict`.
  - `InviteEmployee_AlreadyActiveEmployee_ReturnsConflict`.
  - `InviteEmployee_PendingInvitationExists_ReturnsConflict`.
  - `InviteEmployee_PreviousInvitationExpired_CreatesNewInvitation` → ajustar `ExpiresAt` do anterior para o passado.
  - `GetSentInvitations_ReturnsOnlyPendingNotExpired`.
  - `RevokeInvitation_Pending_SetsRevokedAndRespondedAt`; `RevokeInvitation_OtherCompany_ReturnsNotFound`; `RevokeInvitation_AlreadyAnswered_ReturnsConflict`.
- [ ] **Step 2: Rodar** → falha de compilação.
- [ ] **Step 3: Implementar** os três métodos. Conta convidada: `Accounts` por `NormalizedEmail` com `DeletedAt == null`.
- [ ] **Step 4: Rodar** `dotnet test DampCode_API.Tests` → PASS.
- [ ] **Step 5: Commit** `Feat: convites de funcionários pela conta principal da empresa`.

### Task 4: `CompanyService` — convites recebidos, aceite, recusa, funcionários e verificação

**Files:**
- Create: `Dto/ReceivedInvitationDto.cs`, `Dto/CompanyEmployeeDto.cs`, `Dto/CompanyVerificationDto.cs`, `Dto/UpdateVerificationDto.cs`
- Modify: `Services/ICompanyService.cs`, `Services/CompanyService.cs`
- Test: `DampCode_API.Tests/CompanyServiceTests.cs`

**Interfaces:**
- Consumes: `ICompanyAccessService` (Task 2), injetado em `CompanyService(DampCodeDbContext dbContext, ICompanyAccessService companyAccessService)`. Atualizar o construtor dos testes da Task 3.
- Produces:
  - `ReceivedInvitationDto(Guid Id, string CompanyName, string? JobTitle, DateTime ExpiresAt)`
  - `CompanyEmployeeDto(Guid AccountId, string Name, string Email, string? JobTitle, DateTime StartedAt)`
  - `CompanyVerificationDto(Guid Id, string Name, string Cnpj, string? Area, DateTime CreatedAt)`
  - `UpdateVerificationDto { [Required] public required string Status { get; set; } }`
  - Métodos:
    - `Task<IReadOnlyList<ReceivedInvitationDto>> GetReceivedInvitationsAsync(Guid accountId, CancellationToken)`
    - `Task<CompanyServiceResult<CompanyAccessDto>> AcceptInvitationAsync(Guid accountId, Guid invitationId, CancellationToken)`
    - `Task<CompanyServiceResult<bool>> DeclineInvitationAsync(Guid accountId, Guid invitationId, CancellationToken)`
    - `Task<CompanyServiceResult<IReadOnlyList<CompanyEmployeeDto>>> GetEmployeesAsync(Guid accountId, CancellationToken)`
    - `Task<CompanyServiceResult<bool>> RemoveEmployeeAsync(Guid accountId, Guid employeeAccountId, CancellationToken)`
    - `Task<IReadOnlyList<CompanyVerificationDto>> GetPendingVerificationsAsync(CancellationToken)`
    - `Task<CompanyServiceResult<bool>> UpdateVerificationAsync(Guid companyId, UpdateVerificationDto dto, CancellationToken)`

- [ ] **Step 1: Testes**
  - `GetReceivedInvitations_ReturnsPendingNotExpiredForAccountEmail` → convite de outro e-mail, expirado e recusado não aparecem.
  - `AcceptInvitation_Valid_CreatesMembershipAndMarksAccepted` → membership com `JobTitle` do convite e `EndedAt == null`; retorno `IsPrimaryAccount == false`.
  - `AcceptInvitation_OtherAccountEmail_ReturnsNotFound` → "Convite não encontrado."
  - `AcceptInvitation_Expired_ReturnsExpired` → "Este convite expirou."
  - `AcceptInvitation_AlreadyAnswered_ReturnsConflict` → "Este convite já foi respondido."
  - `AcceptInvitation_AlreadyEmployeeElsewhere_ReturnsConflict` → "Você já está vinculado a uma empresa."
  - `AcceptInvitation_AfterRemoval_CreatesSecondMembership` → dois memberships para a mesma conta e empresa, só um ativo.
  - `DeclineInvitation_Valid_MarksDeclined`.
  - `GetEmployees_PrimaryOrEmployee_ReturnsActiveOnly`; `GetEmployees_NoCompany_ReturnsNotFound`.
  - `RemoveEmployee_Primary_SetsEndedAt`; `RemoveEmployee_NotEmployee_ReturnsNotFound`; `RemoveEmployee_ByEmployee_ReturnsForbidden`.
  - `GetPendingVerifications_ReturnsOnlyPending`.
  - `UpdateVerification_Verified_ChangesStatus`; `UpdateVerification_InvalidStatus_ReturnsValidation` (`"Pending"` e `"x"`); `UpdateVerification_UnknownCompany_ReturnsNotFound`.
- [ ] **Step 2: Rodar** → falha.
- [ ] **Step 3: Implementar.** No aceite, dentro de `BeginTransactionAsync`, a ordem das checagens segue a seção 5.3 do spec. Se o `SaveChangesAsync` lançar `DbUpdateException` (índice `ux_company_memberships_active_account` em aceite concorrente), devolver o mesmo 409 "Você já está vinculado a uma empresa." `UpdateVerification` aceita só `Verified`/`Rejected` (`Enum.TryParse` com `ignoreCase: true`).
- [ ] **Step 4: Rodar** `dotnet test DampCode_API.Tests` → PASS.
- [ ] **Step 5: Commit** `Feat: aceite de convites, gestão de funcionários e verificação de empresas`.

### Task 5: `CompaniesController`, registro e README

**Files:**
- Create: `Controllers/CompaniesController.cs`
- Modify: `Program.cs`, `../README.md`
- Test: `DampCode_API.Tests/CompaniesEndpointTests.cs`

**Interfaces:**
- Consumes: `ICompanyService` (Tasks 3–4), `ICompanyAccessService` (Task 2).
- Produces: rotas exatamente como na tabela 5.1 do spec; `[Authorize]` na classe e `[Authorize(Roles = "Admin")]` nas duas rotas de verificação.

- [ ] **Step 1: Testes de integração** (helpers no estilo de `AuthenticationEndpointTests`; para Admin e para a empresa já verificada, alterar o banco pelo `factory.Services.CreateScope()` e fazer login de novo):
  - `Companies_WithoutToken_Returns401` (`GET /api/Companies/me`).
  - `Me_ParticipantWithoutCompany_Returns404`.
  - `InviteEmployee_ByParticipant_Returns403`.
  - `InviteEmployee_ByEmployee_Returns403` e `RemoveEmployee_ByEmployee_Returns403`.
  - `PendingVerification_ByCompany_Returns403`; `PendingVerification_ByAdmin_Returns200`.
  - `InviteAcceptFlow_EmployeeAppearsInList` → empresa convida (201), participante vê em `invitations/received`, aceita (200, `isPrimaryAccount == false`), `GET /me/employees` pela empresa contém o e-mail, `GET /me` pelo funcionário retorna a empresa.
  - `AcceptInvitation_Expired_Returns410`.
- [ ] **Step 2: Rodar** → falha (404 nas rotas).
- [ ] **Step 3: Implementar** o controller (ID pelo `sub`, como em `AuthController.Me`; método privado `ToActionResult<T>(CompanyServiceResult<T>, int successStatusCode)` no mesmo formato do `AuthController`; 204 quando `T` é `bool`). Registrar `AddScoped<ICompanyAccessService, CompanyAccessService>()` e `AddScoped<ICompanyService, CompanyService>()` ao lado do `IAuthService`.
- [ ] **Step 4: README** — seção "Conta Admin" com o `UPDATE accounts SET account_type = 'Admin' WHERE normalized_email = '<E-MAIL EM MAIÚSCULAS>';` e o aviso de que é preciso fazer login de novo para o token trazer a role, e de que um admin rebaixado mantém o acesso até o token expirar.
- [ ] **Step 5: Rodar** `dotnet test DampCode_API.Tests` → todos passam; `dotnet build DampCode_API` sem warnings novos.
- [ ] **Step 6: Commit** `Feat: rotas de empresas, funcionários e verificação` e `Docs: como promover uma conta a Admin`.

### Task 6: API simulada do front

**Files:**
- Modify: `Damp-Code-Front/src/mock/seed.js`, `Damp-Code-Front/src/mock/installMockApi.js`

**Interfaces:**
- Produces: as rotas da tabela 5.1 do spec (exceto as de Admin), com os mesmos campos JSON em camelCase que a API real devolve (`companyId`, `name`, `verificationStatus`, `isPrimaryAccount`, `canPublish`, `accountId`, `email`, `jobTitle`, `startedAt`, `id`, `expiresAt`, `createdAt`, `companyName`) e os mesmos status HTTP e mensagens (`{ detail: "<mensagem>" }`).

- [ ] **Step 1: Criar branch** `git switch -c feat/empresas-e-funcionarios` no front.
- [ ] **Step 2: Seed** — acrescentar `companies` (uma por usuário `empresa` do seed, `verificationStatus`: iFood `"Verified"`, Nubank `"Pending"`), `memberships: []`, `invitations: []`, e um segundo participante `bruno@dampcode.dev` / `123456` também em `MOCK_ACCOUNTS`. Ao cadastrar empresa pelo mock, criar também a `company` com `"Pending"`.
- [ ] **Step 3: Identidade** — login e cadastros do mock passam a devolver `token: "mock-<id>"`; `installMockApi` lê `init.headers.Authorization` e passa o usuário para `handle(path, method, body, currentUser)`. Sem token válido nas rotas de `/api/Companies` → 401.
- [ ] **Step 4: Rotas** de `/api/Companies` com as mesmas regras do service (24h, só participante existente, um vínculo ativo, expiração calculada).
- [ ] **Step 5: Verificar** `npm run lint` e `npm run build` sem erros novos. No navegador (`npm run dev`), pelo console: login como iFood, `POST /api/Companies/me/invitations` para `bruno@dampcode.dev` → 201.
- [ ] **Step 6: Commit** `Feat: mock das rotas de empresas e funcionários`.

### Task 7: Dashboard da empresa

**Files:**
- Create: `src/app-components/company-dashboard/ManageEmployees.jsx`
- Modify: `src/Pages/company/CompanyDashboard.jsx`

**Interfaces:**
- Consumes: rotas da Task 6/5; `AuthContext` (`user.token`).
- Produces: `<ManageEmployees companyAccess={companyAccess} />`.

- [ ] **Step 1:** `CompanyDashboard` busca `GET /api/Companies/me` no `useEffect`. Se `verificationStatus !== "Verified"`, mostra no topo exatamente: "Sua empresa ainda não foi verificada. Você pode criar rascunhos, mas só poderá publicar depois da verificação." Se `isPrimaryAccount === false`, não renderiza `CreateChallenge` nem `ManageChallenges`. Enquanto `companyAccess` carrega, mantém o comportamento atual para a conta principal (não esconder nada até saber).
- [ ] **Step 2:** `ManageEmployees` — lista de funcionários (nome, e-mail, cargo, "desde dd/mm/aaaa"). Se `isPrimaryAccount`: formulário e-mail + cargo (mostra o `detail` do erro da API abaixo do botão); convites pendentes com "expira em Xh"; botão "Revogar"; botão "Remover" por funcionário com `window.confirm`. Recarrega as listas depois de cada ação. Visual igual ao do `ManageChallenges` (mesmos containers e classes).
- [ ] **Step 3: Verificar** `npm run lint` e `npm run build`. Com o mock: iFood convida Bruno e vê o convite pendente; Nubank vê o aviso de verificação.
- [ ] **Step 4: Commit** `Feat: gestão de funcionários no dashboard da empresa`.

### Task 8: Dashboard do participante

**Files:**
- Create: `src/app-components/participant-dashboard/ReceivedInvitations.jsx`
- Modify: `src/Pages/participant/ParticipantDashboard.jsx`

**Interfaces:**
- Consumes: `GET /api/Companies/invitations/received`, `POST .../accept`, `POST .../decline`, `GET /api/Companies/me`.

- [ ] **Step 1:** `ReceivedInvitations` lista convites (empresa, cargo, "expira em Xh") com Aceitar/Recusar; não renderiza nada se a lista estiver vazia; mostra o `detail` do erro (ex.: "Você já está vinculado a uma empresa."). Depois de aceitar, chama `onAccepted(companyAccess)`.
- [ ] **Step 2:** `ParticipantDashboard` busca `GET /api/Companies/me`; com vínculo, mostra o botão "Painel da empresa" (`navigate("/dashboard/Empresa")`) acima de `Search`. `ReceivedInvitations` fica logo acima de `Search`; `onAccepted` atualiza o vínculo e o botão aparece sem recarregar.
- [ ] **Step 3: Verificar** `npm run lint` e `npm run build`. Fluxo com o mock: iFood convida Bruno → login Bruno → aceita → botão "Painel da empresa" → painel sem criar/listar Hackathons, com a lista de funcionários só para leitura.
- [ ] **Step 4: Commit** `Feat: convites recebidos e acesso ao painel da empresa para funcionários`.
