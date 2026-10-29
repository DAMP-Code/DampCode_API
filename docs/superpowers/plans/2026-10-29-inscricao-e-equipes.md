# Inscrição e equipes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Participantes se inscrevem em Hackathon publicado (individual ou em equipe de até 5, com convite por e-mail e pedido para entrar), a contagem de inscritos vira real e as telas do front ganham esse fluxo.

**Architecture:** Quatro entidades novas no PostgreSQL (`HackathonTeam`, `Participation`, `TeamInvitation`, `TeamJoinRequest`). `ParticipationService` concentra todas as regras e devolve `ParticipationServiceResult<T>`; `HackathonsController` (inscrição e equipes do Hackathon), `TeamsController` e `ParticipationsController` ficam finos. `HackathonService` conta os inscritos; `CompanyService` barra virar funcionário de empresa em cujo Hackathon a pessoa está inscrita. No front, um bloco de inscrição na página do Hackathon, "Minhas inscrições" no painel e a API simulada com as mesmas regras.

**Tech Stack:** ASP.NET Core 8, EF Core 8.0.11 + Npgsql, xUnit + EF InMemory + `Microsoft.AspNetCore.Mvc.Testing`; React 19 + Vite + Tailwind.

**Spec:** `docs/superpowers/specs/2026-10-29-inscricao-e-equipes-design.md`

## Global Constraints

- Branch `feat/inscricao-e-equipes` nos dois repositórios (já criadas a partir de `feat/empresas-e-funcionarios`; o spec já está commitado na da API).
- Não renomear nada existente. Mudanças aditivas, exceto: `HackathonService.ToDto` (contagem real), `CompanyService.AcceptInvitationAsync` (nova checagem), `Hackathon` (coleções `Teams`, `Participations`), `DampCodeDbContext`, `Program.cs`, `HackathonsController` (rotas novas), `HackathonPage.jsx`, `ParticipantDashboard.jsx`, mock.
- Estilo C# igual às etapas anteriores: file-scoped namespace, primary constructors, `sealed`, records de saída, classes com DataAnnotations na entrada, `CancellationToken cancellationToken`, mensagens em português, `ProblemDetails { Detail = Message }`.
- EF: `snake_case`, `pk_`/`fk_`/`ux_`/`ix_`/`ck_`, enums `HasConversion<string>().HasMaxLength(32)`.
- `Normalize(value)` = `value.Trim().ToUpperInvariant()`. Convite de equipe expira em `CreatedAt + 24h`; expirado = `Pending && ExpiresAt <= DateTime.UtcNow`.
- Mudanças de inscrição/equipe só na fase `HackathonPhase.Published` (via `HackathonPhases.GetPhase`).
- Máximo de 5 membros ativos por equipe (`MaxTeamSize = 5`); mínimo não é validado.
- Mensagens exatamente como na seção 5.5 do spec (e as duas da seção "Decisões de Claude (depois da revisão)").
- `dotnet ef` com `DOTNET_ROLL_FORWARD=Major`, `ConnectionStrings__PostgreSql="Host=localhost;Database=nao_usado"` e `Jwt__Secret` de 32+ caracteres; testes com `dotnet test DampCode_API.Tests`.
- Front: `fetch` em `https://localhost:7092`, `Authorization: Bearer ${user.token}`, ids em URL com `encodeURIComponent`, efeitos com função async dentro do `useEffect`; `npm run lint` sem problemas novos (base: 19).
- Commits `Feat:`/`Fix:`/`Test:`/`Docs:` em português, **sem linha de coautoria**.

## Review Focus

- Sair e se inscrever de novo no mesmo Hackathon → funciona (o índice único é filtrado por `left_at IS NULL`, e o InMemory não aplica filtro). (Task 2: `Register_AfterCancel_RegistersAgain`)
- E-mail do convite com caixa/espaços diferentes (`" Bruno@Exemplo.com "`) → acha a conta. (Task 3: `Invite_EmailWithSpacesAndCase_FindsAccount`)
- Último integrante sai com convites pendentes → equipe desfeita, convites somem do painel de quem foi convidado, participações antigas ficam com `TeamId = null`. (Task 4: `Leave_LastMember_DissolvesTeam`)
- Hackathon cancelado com inscritos → "Minhas inscrições" mostra com `fase = "Cancelado"`. (Task 5: `GetMine_CancelledHackathon_StillListed`)
- Duas pessoas na última vaga ao mesmo tempo → só uma entra; a outra recebe 409 "Esta equipe já está completa." (Task 4: `AcceptRequest_TeamChangedConcurrently_ReturnsConflict`)

---

## File Structure

**API (`DampCode_API/`)**

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `Models/Participation.cs` | Create | `Participation` |
| `Models/HackathonTeam.cs` | Create | `HackathonTeam`, `TeamInvitation` + `TeamInvitationStatus`, `TeamJoinRequest` + `TeamJoinRequestStatus` |
| `Models/Hackathon.cs` | Modify | coleções `Teams`, `Participations` |
| `Data/Configurations/ParticipationConfiguration.cs` | Create | as quatro configurações |
| `Data/DampCodeDbContext.cs` | Modify | `HackathonTeams`, `Participations`, `TeamInvitations`, `TeamJoinRequests` |
| `Data/Migrations/*_ParticipationsAndTeams.cs` | Create (gerado) | migration |
| `Dto/ParticipationDtos.cs` | Create | records de saída da etapa |
| `Dto/CreateTeamDto.cs`, `UpdateTeamDto.cs`, `InviteTeamMemberDto.cs` | Create | entrada |
| `Services/IParticipationService.cs`, `ParticipationService.cs` | Create | regras; `ParticipationServiceResult<T>`, `ParticipationServiceError` |
| `Services/HackathonService.cs` | Modify | contagem real |
| `Services/CompanyService.cs` | Modify | checagem no aceite de funcionário |
| `Controllers/HackathonsController.cs` | Modify | inscrição e equipes do Hackathon |
| `Controllers/TeamsController.cs`, `ParticipationsController.cs` | Create | rotas de equipe e "minhas inscrições" |
| `Program.cs` | Modify | registrar `IParticipationService` |

**Testes:** `ParticipationServiceTests.cs`, `ParticipationsEndpointTests.cs`, mais casos em `HackathonServiceTests.cs` e `CompanyServiceTests.cs`; helper `ParticipationSeed.cs` (Hackathon publicado direto no banco).

**Front (`Damp-Code-Front/src/`)**

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `mock/seed.js`, `mock/installMockApi.js` | Modify | dados e rotas da etapa |
| `app-components/hackathon-page/JoinHackathon.jsx` | Create | bloco de inscrição na página do Hackathon |
| `Pages/HackathonPage.jsx` | Modify | usa `JoinHackathon` no lugar do botão "Participar" sem ação |
| `app-components/participant-dashboard/MyParticipations.jsx` | Create | convites de equipe, minhas inscrições, pedidos enviados |
| `Pages/participant/ParticipantDashboard.jsx` | Modify | inclui `MyParticipations` |

---

### Task 1: Entidades, configuração e migration

**Files:** Create `Models/Participation.cs`, `Models/HackathonTeam.cs`, `Data/Configurations/ParticipationConfiguration.cs`, migration; Modify `Models/Hackathon.cs`, `Data/DampCodeDbContext.cs`; Test `DampCode_API.Tests/ParticipationServiceTests.cs`, `DampCode_API.Tests/ParticipationSeed.cs`.

**Interfaces:**
- Produces:
  - `Participation { Guid Id; Guid HackathonId; Guid AccountId; Guid? TeamId; DateTime JoinedAt; DateTime? LeftAt; DateTime CreatedAt; DateTime UpdatedAt; Hackathon Hackathon; Account Account; HackathonTeam? Team }`
  - `HackathonTeam { Guid Id; Guid HackathonId; Guid CreatedByAccountId; string Name; string NormalizedName; bool IsOpen; int MemberCount; DateTime CreatedAt; DateTime UpdatedAt; Hackathon Hackathon; ICollection<Participation> Members; ICollection<TeamInvitation> Invitations; ICollection<TeamJoinRequest> JoinRequests }`
  - `TeamInvitation { Guid Id; Guid TeamId; Guid InvitedByAccountId; string Email; string NormalizedEmail; TeamInvitationStatus Status; DateTime ExpiresAt; DateTime? RespondedAt; DateTime CreatedAt; DateTime UpdatedAt; HackathonTeam Team; Account InvitedByAccount }`, `enum TeamInvitationStatus { Pending, Accepted, Declined, Revoked, Cancelled }`
  - `TeamJoinRequest { Guid Id; Guid TeamId; Guid AccountId; TeamJoinRequestStatus Status; Guid? RespondedByAccountId; DateTime? RespondedAt; DateTime CreatedAt; DateTime UpdatedAt; HackathonTeam Team; Account Account }`, `enum TeamJoinRequestStatus { Pending, Accepted, Declined, Cancelled }`
  - `Hackathon.Teams`, `Hackathon.Participations`; `DbSet`s `HackathonTeams`, `Participations`, `TeamInvitations`, `TeamJoinRequests`.
  - `internal static class ParticipationSeed { Task<Hackathon> PublishedHackathonAsync(DampCodeDbContext db, Company company, HackathonParticipationMode mode, DateTime? startsAt = null) }` — grava Hackathon `Scheduled` completo com `StartsAt = startsAt ?? UtcNow + 2d`, `EndsAt = +3d`, `ReviewEndsAt = +6d`.

- [ ] **Step 1: Teste** `Participation_PersistsWithTeam`: empresa + Hackathon de equipe (seed) + participante; grava `HackathonTeam { Name = "Os Rápidos", NormalizedName = "OS RÁPIDOS", MemberCount = 1 }` e uma `Participation` com `TeamId`; recarrega com `Include(Team)` → `Team.Name == "Os Rápidos"`, `LeftAt == null`.
- [ ] **Step 2: Rodar** `dotnet test DampCode_API.Tests --filter Participation_PersistsWithTeam` → falha de compilação.
- [ ] **Step 3: Implementar** entidades e configurações (seção 4.1 do spec):
  - `hackathon_teams`: `name`/`normalized_name` 100, `ux_hackathon_teams_hackathon_name` (`hackathon_id`, `normalized_name`), `ck_hackathon_teams_member_count` (`member_count BETWEEN 0 AND 5`), `MemberCount` com `.IsConcurrencyToken()`, FKs `fk_hackathon_teams_hackathons` (Cascade, `WithMany(hackathon => hackathon.Teams)`) e `fk_hackathon_teams_accounts` (Restrict, sem inversa), `ix_hackathon_teams_created_by_account_id`;
  - `participations`: `ux_participations_active_account` (`hackathon_id`, `account_id`) `.IsUnique().HasFilter("left_at IS NULL")`, `ix_participations_team_id`, `ix_participations_account_id`, FKs `fk_participations_hackathons` (Cascade), `fk_participations_accounts` (Restrict), `fk_participations_hackathon_teams` (Restrict, `WithMany(team => team.Members)`);
  - `team_invitations` e `team_join_requests` como `CompanyInvitationConfiguration` (email 254, `ck_team_invitations_status`, `ck_team_join_requests_status` com os valores dos enums, índices da seção 4.1; `RespondedByAccountId` fica sem FK nem índice), FKs para a equipe com Cascade e para contas com Restrict.
- [ ] **Step 4: Rodar** → PASS; suíte inteira → passa.
- [ ] **Step 5: Migration** `ParticipationsAndTeams`; conferir que o `Up` só cria as quatro tabelas, com os nomes acima e o filtro do índice.
- [ ] **Step 6: Commit** `Feat: entidades de inscrição e equipes com migration`.

### Task 2: Serviço — elegibilidade e inscrição individual

**Files:** Create `Services/IParticipationService.cs`, `Services/ParticipationService.cs`, `Dto/ParticipationDtos.cs`; Test `ParticipationServiceTests.cs`.

**Interfaces:**
- Consumes: `ICompanyAccessService.GetManagedCompanyAsync`, `HackathonPhases.GetPhase` (etapas 1–2).
- Produces:
  - `public sealed record ParticipationServiceResult<T>(bool Succeeded, T? Value = default, ParticipationServiceError Error = ParticipationServiceError.None, string? Message = null);` `public enum ParticipationServiceError { None, NotFound, Forbidden, Conflict, Expired, Validation }`
  - Em `Dto/ParticipationDtos.cs`: `TeamMemberDto(Guid AccountId, string Nome)`, `TeamPendingInvitationDto(Guid Id, string Email, DateTime ExpiraEm)`, `TeamPendingRequestDto(Guid Id, Guid AccountId, string Nome)`, `MyTeamDto(Guid TeamId, string Nome, bool Aberta, IReadOnlyList<TeamMemberDto> Membros, IReadOnlyList<TeamPendingInvitationDto> ConvitesPendentes, IReadOnlyList<TeamPendingRequestDto> PedidosPendentes)`, `MyParticipationDto(Guid HackathonId, string Titulo, string Empresa, string Fase, string Formato, DateTime? DataInicio, MyTeamDto? Equipe)`, `TeamInvitationReceivedDto(Guid Id, Guid TeamId, string Equipe, Guid HackathonId, string Hackathon, string ConvidadoPor, DateTime ExpiraEm)`, `TeamJoinRequestSentDto(Guid Id, Guid TeamId, string Equipe, Guid HackathonId, string Hackathon)`, `OpenTeamDto(Guid TeamId, string Nome, int Membros, int Vagas)`, `ParticipationsOverviewDto(IReadOnlyList<MyParticipationDto> Inscricoes, IReadOnlyList<TeamInvitationReceivedDto> ConvitesRecebidos, IReadOnlyList<TeamJoinRequestSentDto> PedidosEnviados)`.
  - `ParticipationService(DampCodeDbContext dbContext, ICompanyAccessService companyAccessService)` com `Task<ParticipationServiceResult<MyParticipationDto>> RegisterAsync(Guid accountId, Guid hackathonId, CancellationToken)` e `Task<ParticipationServiceResult<bool>> CancelRegistrationAsync(Guid accountId, Guid hackathonId, CancellationToken)`.
  - Helper privado reaproveitado pelas Tasks 3–4: `Task<(Hackathon? Hackathon, ParticipationServiceResult<T>? Failure)> CheckCanJoinAsync<T>(Guid accountId, Guid hackathonId, CancellationToken)` — na ordem: conta participante ativa (403 participantes), Hackathon existe e não é Draft/Cancelled (404), conta não gerencia a empresa organizadora (403), fase Published (409 encerradas), sem participação ativa (409 já inscrito). Helper `Task CancelPendingForAccountAsync(Guid accountId, Guid hackathonId, Guid? exceptRequestId = null)` — passa a `Cancelled` convites recebidos (por `NormalizedEmail` da conta) e pedidos enviados pendentes da pessoa nas equipes desse Hackathon.

- [ ] **Step 1: Testes** (Hackathon individual publicado do iFood verificado; Ana participante):
  - `Register_Individual_CreatesParticipation` → `Succeeded`, `Equipe == null`, `Fase == "Publicado"`.
  - `Register_Twice_ReturnsConflict` → "Você já está inscrito neste Hackathon.".
  - `Register_TeamHackathon_ReturnsConflict` → "Este Hackathon é em equipe. Crie uma equipe ou entre em uma.".
  - `Register_ActivePhase_ReturnsConflict` (`startsAt = UtcNow - 1d`) → "As inscrições deste Hackathon estão encerradas.".
  - `Register_CompanyAccount_ReturnsForbidden` → "Apenas participantes podem se inscrever em Hackathons.".
  - `Register_EmployeeOfOrganizer_ReturnsForbidden` → "Quem organiza o Hackathon não pode participar dele."; `Register_EmployeeOfOtherCompany_Succeeds`.
  - `Register_DraftHackathon_ReturnsNotFound` → "Hackathon não encontrado.".
  - `Cancel_Individual_SetsLeftAt`; `Register_AfterCancel_RegistersAgain` (duas participações no banco, uma com `LeftAt`).
  - `Cancel_TeamHackathon_ReturnsConflict` → "Use sair da equipe.".
- [ ] **Step 2: Rodar** `--filter ParticipationServiceTests` → falha de compilação.
- [ ] **Step 3: Implementar** interface (só estes dois métodos), DTOs e service.
- [ ] **Step 4: Rodar** suíte → passa.
- [ ] **Step 5: Commit** `Feat: inscrição individual em Hackathon`.

### Task 3: Equipes — criar, editar, convidar, responder convite

**Files:** Modify `Services/IParticipationService.cs`, `Services/ParticipationService.cs`; Create `Dto/CreateTeamDto.cs` (`string? Nome`, `bool Aberta`), `Dto/UpdateTeamDto.cs` (`string? Nome`, `bool? Aberta`), `Dto/InviteTeamMemberDto.cs` (`[Required, EmailAddress, StringLength(254)] string Email`, como `InviteEmployeeDto`); Test `ParticipationServiceTests.cs`.

**Interfaces:**
- Produces:
  - `Task<ParticipationServiceResult<MyTeamDto>> CreateTeamAsync(Guid accountId, Guid hackathonId, CreateTeamDto dto, CancellationToken)`
  - `Task<ParticipationServiceResult<MyTeamDto>> UpdateTeamAsync(Guid accountId, Guid teamId, UpdateTeamDto dto, CancellationToken)`
  - `Task<ParticipationServiceResult<TeamPendingInvitationDto>> InviteAsync(Guid accountId, Guid teamId, InviteTeamMemberDto dto, CancellationToken)`
  - `Task<ParticipationServiceResult<bool>> RevokeInvitationAsync(Guid accountId, Guid teamId, Guid invitationId, CancellationToken)`
  - `Task<ParticipationServiceResult<MyParticipationDto>> AcceptInvitationAsync(Guid accountId, Guid invitationId, CancellationToken)`
  - `Task<ParticipationServiceResult<bool>> DeclineInvitationAsync(Guid accountId, Guid invitationId, CancellationToken)`
  - Helper privado `Task<(HackathonTeam? Team, ParticipationServiceResult<T>? Failure)> FindMemberTeamAsync<T>(Guid accountId, Guid teamId, CancellationToken)` — equipe em que a conta tem participação ativa (senão 404 "Equipe não encontrada."), com o Hackathon carregado e fase Published (senão 409 encerradas).
  - Helper privado `Task<ParticipationServiceResult<T>?> JoinTeamAsync<T>(Account account, HackathonTeam team, CancellationToken)` — cria a participação, `MemberCount++`, cancela pendentes da pessoa no Hackathon e, se chegar a 5, cancela os pendentes da equipe; salva tratando `DbUpdateException` (409 já inscrito) e `DbUpdateConcurrencyException` (409 "Esta equipe já está completa."); devolve falha ou `null`.

Regras fixadas pelos testes: nome obrigatório até 100 (400 "Informe um nome de equipe com até 100 caracteres."), único por Hackathon sem diferenciar maiúsculas (409); criar em Hackathon individual → 409 "Este Hackathon é individual."; criar inscreve o criador (`MemberCount = 1`); convite só para conta participante existente (404 "Nenhum participante encontrado com este e-mail."), não para quem já está na equipe (409 "Esta pessoa já está na equipe."), sem pendente repetido (409 "Já existe um convite pendente para este e-mail."), equipe cheia (409 "Esta equipe já está completa."); aceitar: só o dono do e-mail (404 "Convite não encontrado."), respondido (409 "Este convite já foi respondido."), expirado (410 "Este convite expirou."), mesmas checagens de `CheckCanJoinAsync`, mas "já inscrito" com a mensagem "Você já está inscrito neste Hackathon. Saia da equipe atual antes.".

- [ ] **Step 1: Testes:**
  - `CreateTeam_RegistersCreator` → `Membros.Count == 1`, `MemberCount == 1`, participação com `TeamId`.
  - `CreateTeam_DuplicateNameIgnoringCase_ReturnsConflict` ("Os Rápidos" e "os rápidos").
  - `CreateTeam_EmptyName_ReturnsValidation`; `CreateTeam_IndividualHackathon_ReturnsConflict`.
  - `UpdateTeam_ByMember_TogglesOpen`; `UpdateTeam_ByNonMember_ReturnsNotFound`.
  - `Invite_EmailWithSpacesAndCase_FindsAccount` (`" Bruno@Exemplo.com "`).
  - `Invite_UnknownEmail_ReturnsNotFound`; `Invite_PendingTwice_ReturnsConflict`; `Invite_FullTeam_ReturnsConflict` (`MemberCount = 5` gravado direto).
  - `AcceptInvitation_JoinsTeam` → `MemberCount == 2`, convite `Accepted`.
  - `AcceptInvitation_Expired_ReturnsExpired` (`ExpiresAt` no passado).
  - `AcceptInvitation_AlreadyInOtherTeam_ReturnsConflict` → mensagem "… Saia da equipe atual antes.".
  - `AcceptInvitation_CancelsOtherPendingOfAccount` → outro convite recebido no mesmo Hackathon vira `Cancelled`.
  - `AcceptInvitation_FillsTeam_CancelsTeamPending` → equipe com 4 + convite pendente para outra pessoa; aceite leva a 5 e o outro convite vira `Cancelled`.
  - `RevokeInvitation_ByMember_Revokes`; `DeclineInvitation_SetsDeclined`.
- [ ] **Step 2: Rodar** → falha de compilação.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Rodar** suíte → passa.
- [ ] **Step 5: Commit** `Feat: equipes com convite por e-mail`.

### Task 4: Pedidos para entrar, sair da equipe e equipes abertas

**Files:** Modify `Services/IParticipationService.cs`, `Services/ParticipationService.cs`; Test `ParticipationServiceTests.cs`, `TestDb.cs` (helper novo).

**Interfaces:**
- Produces:
  - `Task<ParticipationServiceResult<TeamJoinRequestSentDto>> RequestToJoinAsync(Guid accountId, Guid teamId, CancellationToken)`
  - `Task<ParticipationServiceResult<bool>> CancelRequestAsync(Guid accountId, Guid requestId, CancellationToken)`
  - `Task<ParticipationServiceResult<TeamMemberDto>> AcceptRequestAsync(Guid accountId, Guid requestId, CancellationToken)` (accountId = integrante que responde)
  - `Task<ParticipationServiceResult<bool>> DeclineRequestAsync(Guid accountId, Guid requestId, CancellationToken)`
  - `Task<ParticipationServiceResult<bool>> LeaveTeamAsync(Guid accountId, Guid teamId, CancellationToken)`
  - `Task<ParticipationServiceResult<IReadOnlyList<OpenTeamDto>>> GetOpenTeamsAsync(Guid hackathonId, CancellationToken)` (Hackathon público senão 404; só `IsOpen` com `MemberCount < 5`; `Vagas = 5 - MemberCount`; ordem por nome)
  - Em `TestDb`: `public static DampCodeDbContext CreateShared(string databaseName)` para abrir um segundo contexto no mesmo banco InMemory.

Regras: pedir exige `CheckCanJoinAsync` ok (quem já está inscrito não pede), equipe aberta (409 "Esta equipe não está aceitando pedidos."), não cheia, sem pendente repetido (409 "Você já pediu para entrar nesta equipe."); cancelar só o próprio pendente (404 "Pedido não encontrado."); aceitar/recusar só integrante da equipe do pedido (404 "Pedido não encontrado."), respondido (409 "Este pedido já foi respondido."), aceitar revalida a pessoa com `CheckCanJoinAsync` e usa `JoinTeamAsync`; grava `RespondedByAccountId`. Sair: grava `LeftAt`, `MemberCount--`, cancela os pedidos pendentes **da pessoa**; se `MemberCount == 0`, zera `TeamId` das participações antigas e apaga a equipe (convites e pedidos em cascata).

- [ ] **Step 1: Testes:**
  - `RequestToJoin_OpenTeam_CreatesPending`; `RequestToJoin_ClosedTeam_ReturnsConflict`; `RequestToJoin_Twice_ReturnsConflict`; `RequestToJoin_AlreadyRegistered_ReturnsConflict`.
  - `AcceptRequest_ByAnyMember_JoinsTeam` (quem aceita não é o criador) → `RespondedByAccountId` = quem aceitou.
  - `AcceptRequest_ByNonMember_ReturnsNotFound`; `AcceptRequest_FullTeam_ReturnsConflict`.
  - `AcceptRequest_TeamChangedConcurrently_ReturnsConflict`: equipe com `MemberCount = 4` e um pedido pendente; o teste rastreia a equipe no contexto do serviço (`_dbContext.HackathonTeams.Find(id)`), um segundo contexto (`TestDb.CreateShared` no mesmo banco) grava `MemberCount = 5`, e `AcceptRequestAsync` — que reaproveita a entidade rastreada com o valor antigo — recebe `DbUpdateConcurrencyException` → 409 "Esta equipe já está completa.".
  - `CancelRequest_ByRequester_Cancels`; `DeclineRequest_SetsDeclined`.
  - `Leave_Member_TeamContinues` → `MemberCount` cai, criador saindo não apaga a equipe.
  - `Leave_LastMember_DissolvesTeam` → equipe e convites pendentes somem; participações antigas com `TeamId == null`.
  - `GetOpenTeams_ListsOnlyOpenWithVacancies`.
  - `Leave_ActivePhase_ReturnsConflict`.
- [ ] **Step 2: Rodar** → falha de compilação.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Rodar** suíte → passa.
- [ ] **Step 5: Commit** `Feat: pedidos para entrar e saída de equipe`.

### Task 5: "Minhas inscrições", contagem real e aceite de funcionário

**Files:** Modify `Services/IParticipationService.cs`, `Services/ParticipationService.cs`, `Services/HackathonService.cs`, `Services/CompanyService.cs`; Test `ParticipationServiceTests.cs`, `HackathonServiceTests.cs`, `CompanyServiceTests.cs`.

**Interfaces:**
- Produces: `Task<ParticipationsOverviewDto> GetMineAsync(Guid accountId, CancellationToken)` (seção 5.3: participações ativas em qualquer fase ordenadas por `StartsAt`; equipe com membros, convites pendentes não expirados e pedidos pendentes; convites recebidos pendentes e não expirados; pedidos enviados pendentes; `Nome` de `UserProfile.Name`, `ConvidadoPor` = nome do perfil ou e-mail de quem convidou).
- `HackathonService.ToDto` ganha o parâmetro `int participants` e gera `"1 pessoa já entrou"` ou `"{N} pessoas já entraram"`; contagem por `Participations.Where(LeftAt == null)` agrupada por `HackathonId` numa consulta para listas e `CountAsync` para um item.
- `CompanyService.AcceptInvitationAsync`: depois da checagem de vínculo, 409 "Você está inscrito em um Hackathon desta empresa. Saia da inscrição antes de aceitar." se houver participação ativa em Hackathon da empresa do convite cuja fase (`GetPhase`) não seja `Closed` nem `Cancelled`.

- [ ] **Step 1: Testes:**
  - `GetMine_ListsTeamWithMembersInvitesAndRequests`; `GetMine_HidesExpiredInvitations`; `GetMine_CancelledHackathon_StillListed` (`Fase == "Cancelado"`); `GetMine_IgnoresLeftParticipations`.
  - `HackathonServiceTests`: `Participants_CountsActiveOnly` (2 ativos + 1 saído → "2 pessoas já entraram"), `Participants_One_UsesSingular` ("1 pessoa já entrou"), `Participants_None` ("0 pessoas já entraram").
  - `CompanyServiceTests`: `AcceptInvitation_RegisteredInCompanyHackathon_ReturnsConflict`; `AcceptInvitation_RegisteredInClosedCompanyHackathon_Succeeds`.
- [ ] **Step 2: Rodar** → falham.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Rodar** suíte → passa.
- [ ] **Step 5: Commit** `Feat: minhas inscrições e contagem real de participantes`.

### Task 6: Rotas

**Files:** Modify `Controllers/HackathonsController.cs`, `Program.cs`; Create `Controllers/TeamsController.cs`, `Controllers/ParticipationsController.cs`; Test `ParticipationsEndpointTests.cs`.

**Interfaces:**
- Consumes: todos os métodos de `IParticipationService`.
- Produces: rotas da seção 5.2 do spec. Em `HackathonsController` (construtor ganha `IParticipationService participationService`): `POST "{id:guid}/inscricao"` (201), `DELETE "{id:guid}/inscricao"` (204), `[AllowAnonymous] GET "{id:guid}/equipes"` (200), `POST "{id:guid}/equipes"` (201). `TeamsController` (`api/Teams`, `[Authorize]`): `PATCH "{teamId:guid}"`, `POST "{teamId:guid}/convites"` (201), `DELETE "{teamId:guid}/convites/{inviteId:guid}"` (204), `POST "convites/{inviteId:guid}/aceitar"` (200), `POST "convites/{inviteId:guid}/recusar"` (204), `POST "{teamId:guid}/pedidos"` (201), `DELETE "pedidos/{requestId:guid}"` (204), `POST "pedidos/{requestId:guid}/aceitar"` (200), `POST "pedidos/{requestId:guid}/recusar"` (204), `DELETE "{teamId:guid}/membros/me"` (204). `ParticipationsController` (`api/Participations`, `[Authorize]`): `GET "me"`. Mapeamento de erro igual ao `CompaniesController` (com `Expired` → 410). Registrar `builder.Services.AddScoped<IParticipationService, ParticipationService>();`.

- [ ] **Step 1: Testes** (helpers copiados do `HackathonsEndpointTests`; Hackathon publicado criado via API com empresa verificada):
  - `Register_WithoutToken_Returns401`; `Register_Company_Returns403`.
  - `OpenTeams_Anonymous_Returns200`.
  - `InviteFlow_ParticipationsMeShowsTeam`: Ana cria equipe → convida Bruno → Bruno vê o convite em `GET /api/Participations/me` → aceita (200) → Ana vê 2 membros.
  - `Participants_CountAppearsInHackathonJson` → depois de uma inscrição, `GET /api/Hackathons/{id}` traz `"participantes": "1 pessoa já entrou"`.
- [ ] **Step 2: Rodar** → falham (404/405).
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Rodar** suíte → passa; `dotnet build DampCode_API` sem warnings novos.
- [ ] **Step 5: Commit** `Feat: rotas de inscrição e equipes`.

### Task 7: API simulada

**Files:** Modify `src/mock/seed.js`, `src/mock/installMockApi.js`.

**Interfaces:**
- Produces: listas novas no banco simulado `teams` (`teamId`, `hackathonId`, `nome`, `aberta`, `criadoPor`), `participations` (`id`, `hackathonId`, `accountId`, `teamId`, `leftAt`), `teamInvitations`, `teamJoinRequests` e contadores `nextTeamId`, `nextParticipationId`, `nextTeamInvitationId`, `nextTeamJoinRequestId`; `handleParticipations(path, method, body, database, user)` para `/api/Teams`, `/api/Participations` e as rotas `inscricao`/`equipes` do Hackathon (chamado por `handleHackathons` antes do casamento genérico de `/{id}`); `toHackathonResponse` passa a contar inscritos ativos. Mesmas mensagens e status da API.

- [ ] **Step 1: Seed:** Hackathon 4 ("App que cabe no bolso", Nubank, individual, publicado) com Ana inscrita; Hackathon 3 ("Limite em tempo real", Nubank, equipe, publicado) com equipe aberta "Os Rápidos" de Bruno. `loadDatabase` acrescenta as listas novas em bancos antigos (mesmo laço de chaves da etapa 1).
- [ ] **Step 2: Rotas e regras** espelhando as Tasks 2–5 (elegibilidade, fase, máximo 5, cancelamentos automáticos, desfazer equipe vazia, organizadora barrada via `managedCompany`).
- [ ] **Step 3: Verificar** lint (≤ 19, nenhum novo) e build; script Node no scratchpad (como na etapa 2) cobrindo: inscrição individual e repetida, equipe criada, convite → aceite, pedido → aceite por integrante, equipe cheia, sair/desfazer, `GET me`, contagem em `GET /api/Hackathons`, empresa barrada.
- [ ] **Step 4: Commit** `Feat: inscrição e equipes na API simulada`.

### Task 8: Bloco de inscrição na página do Hackathon

**Files:** Create `src/app-components/hackathon-page/JoinHackathon.jsx`; Modify `src/Pages/HackathonPage.jsx`.

**Interfaces:**
- Produces: `JoinHackathon({ hackathon, onChanged })` — recebe o Hackathon carregado pela página; `onChanged()` faz a página recarregar (contagem).

- [ ] **Step 1: `JoinHackathon.jsx`** (spec 6.1): lê `user` do `AuthContext`; sem login → botão leva a `/loginUser`; conta empresa → nada; fase diferente de "Publicado" → "Inscrições encerradas". Carrega `GET /api/Participations/me` e acha a inscrição deste Hackathon.
  - Individual: "Participar" (POST inscrição) / "Inscrito ✓" + "Cancelar inscrição" (com `window.confirm`).
  - Equipe: inscrito → "Sua equipe: <nome>" + link "Gerenciar no painel" (`/dashboard/participante`); não inscrito → formulário "Criar equipe" (nome + checkbox "Aceitar pedidos") e lista de `GET /api/Hackathons/{id}/equipes` com "Pedir para entrar" (pedido já enviado aparece como "Pedido enviado").
  - `detail` de erro abaixo dos botões. Botões usam `hackathon.corPrincipal` como o botão atual.
- [ ] **Step 2: `HackathonPage.jsx`**: troca o botão "Participar" sem ação por `<JoinHackathon hackathon={hackathon} onChanged={...} />`; o recarregamento usa um contador no `useEffect` existente.
- [ ] **Step 3: Verificar** lint e build. Com o mock, como Ana: Hackathon 4 (individual, inscrita no seed) mostra "Inscrito ✓"; cancelar faz a contagem cair e "Participar" a faz subir de novo; Hackathon 1 (fase Ativo) mostra "Inscrições encerradas"; no Hackathon 3 "Os Rápidos" aparece na lista e "Pedir para entrar" vira "Pedido enviado".
- [ ] **Step 4: Commit** `Feat: inscrição na página do Hackathon`.

### Task 9: "Minhas inscrições" no painel do participante

**Files:** Create `src/app-components/participant-dashboard/MyParticipations.jsx`; Modify `src/Pages/participant/ParticipantDashboard.jsx`.

- [ ] **Step 1: `MyParticipations.jsx`** (spec 6.2), no estilo do `ReceivedInvitations.jsx`: carrega `GET /api/Participations/me`; seções "Convites de equipe" (Aceitar/Recusar), "Minhas inscrições" (título com link para `/hackathon/{id}`, fase, data de início; equipe com membros, chave "Aceitando pedidos" via `PATCH`, campo "Convidar por e-mail", convites pendentes com Revogar, pedidos recebidos com Aceitar/Recusar, "Sair da equipe" com `window.confirm`; individual com "Cancelar inscrição") e "Pedidos enviados" (Cancelar). Ações de mudança só com `fase === "Publicado"`. Recarrega após cada ação (contador `reload`). Some se não houver nada a mostrar.
- [ ] **Step 2: `ParticipantDashboard.jsx`**: inclui `<MyParticipations />` logo abaixo de `<ReceivedInvitations />`.
- [ ] **Step 3: Verificar** lint e build. Roteiro com o mock (depois de `localStorage.removeItem("damp-mock-db")`): Bruno abre "Os Rápidos" → Ana pede para entrar → Bruno aceita no painel → Ana vê a equipe com 2 membros → Bruno convida `ana@dampcode.dev` de novo e vê "Esta pessoa já está na equipe." → Ana sai e a equipe continua com Bruno → Bruno sai e a equipe some; contagem pública acompanha.
- [ ] **Step 4: Commit** `Feat: minhas inscrições no painel do participante`.
