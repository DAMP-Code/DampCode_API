# Ciclo de vida do Hackathon — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hackathon no PostgreSQL ligado à empresa, com rascunho → publicação (empresa verificada) → fases calculadas pelas datas → cancelamento/encerramento, e as telas do front (criar/editar, gerenciar, detalhes) ligadas a esse fluxo.

**Architecture:** A entidade `Hackathon` existente entra no `DbContext` com tabela de tecnologias. `HackathonPhases.GetPhase` é a única regra de fase. `HackathonService` concentra criação, edição, publicação, cancelamento, encerramento e listagens, devolvendo `HackathonServiceResult<T>`; `HackathonsController` fica fino e deixa de usar o MongoDB. No front, `CreateHackathon` vira criar/editar, `ManageChallenges` passa a usar `GET /api/Hackathons/empresa`, e a API simulada ganha as mesmas regras.

**Tech Stack:** ASP.NET Core 8 (net8.0), EF Core 8.0.11 + Npgsql, xUnit + EF InMemory + `Microsoft.AspNetCore.Mvc.Testing`; React 19 + Vite + Tailwind.

**Spec:** `docs/superpowers/specs/2026-10-28-ciclo-de-vida-hackathon-design.md`

## Global Constraints

- Branch `feat/ciclo-de-vida-hackathon` nos dois repositórios, criada a partir de `feat/empresas-e-funcionarios` (já criadas; a da API já tem o spec commitado).
- Não renomear nada existente (classes, métodos, rotas, DTOs, propriedades, componentes). Mudanças aditivas, exceto: `HackathonsController` (troca o MongoDB pelo service), `HackathonDocument` (removido), `CreateHackathonDto` (`Premiacao` → `decimal?`, `DataFinal` → `DateTime?`, campos novos), comentário de `Hackathon.cs`.
- Estilo C#: file-scoped namespace (exceto `CreateHackathonDto`, que mantém o namespace com chaves), primary constructors, `sealed`, `CancellationToken cancellationToken`, records para saída, mensagens em português, `ProblemDetails { Detail = Message }`.
- EF: `snake_case`; `pk_`/`fk_`/`ux_`/`ix_`/`ck_`; enums `HasConversion<string>().HasMaxLength(32)`.
- `Normalize(value)` = `value.Trim().ToUpperInvariant()` (mesmo do `AuthService`).
- Datas: toda data que entra é convertida para UTC (`Kind == Utc` mantém; `Local` → `ToUniversalTime()`; `Unspecified` → `DateTime.SpecifyKind(value, DateTimeKind.Utc)`). "Agora" = `DateTime.UtcNow`.
- Textos de fase exatamente: `Rascunho`, `Publicado`, `Ativo`, `Em avaliação`, `Encerrado`, `Cancelado`. Formato: `Individual`/`Equipe`. Premiação: `Dinheiro`/`Entrevista`.
- Mensagens de erro exatamente como na seção 5.4 do spec.
- SDK só .NET 10: `dotnet ef` com `DOTNET_ROLL_FORWARD=Major` e as variáveis `ConnectionStrings__PostgreSql="Host=localhost;Database=nao_usado"` e `Jwt__Secret` (qualquer valor com 32+ caracteres); testes com `dotnet test DampCode_API.Tests`.
- Front: `fetch` direto em `https://localhost:7092`; `Authorization: Bearer ${user.token}` nas chamadas de Hackathon que precisam de conta; efeitos com função async dentro do `useEffect` (padrão do `HackathonPage`); `npm run lint` fica nos 22 problemas da base.
- Commits no padrão do repositório (`Feat:`, `Fix:`, `Test:`, `Docs:`), em português, **sem linha de coautoria** (pedido do usuário).

## Review Focus

- Datas enviadas com fuso (`"2026-11-10T09:00:00-03:00"`) ou sem fuso → gravadas em UTC, sem erro do Npgsql. (Task 3: `Create_DatesWithOffset_StoredAsUtc`)
- Rascunho salvo com datas fora de ordem → 400 com a mensagem de ordem, nunca 500 pela `ck_hackathons_dates`. (Task 3: `Update_DraftDatesOutOfOrder_ReturnsValidation`)
- Edição de rascunho trocando tecnologias com sobreposição (React, Node → Node, SQL) → fica Node, SQL, sem conflito de rastreamento do EF. (Task 3: `Update_Draft_ReplacesTechnologies`)
- Funcionário removido (membership encerrado) tentando editar → 403. (Task 4: `Update_RemovedEmployee_ReturnsForbidden`)
- Front com banco simulado salvo antes desta etapa (Hackathons sem `storedStatus`) → lista de Hackathons trocada pela do seed, telas não quebram. (Task 6)

---

## File Structure

**API (`DampCode_API/`)**

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `Models/Hackathon.cs` | Modify | `Technologies`, `PrizeAmount`, `RewardType`, `WinnersCount`, `Logo`, `Company`; `HackathonRewardType`; `HackathonTechnology` |
| `Models/HackathonPhase.cs` | Create | `HackathonPhase` + `HackathonPhases.GetPhase/ToText` |
| `Models/Company.cs`, `Models/Technology.cs` | Modify | coleções inversas `Hackathons` |
| `Data/Configurations/HackathonConfiguration.cs` | Create | `hackathons` e `hackathon_technologies` |
| `Data/DampCodeDbContext.cs` | Modify | `Hackathons`, `HackathonTechnologies` |
| `Data/Migrations/*_Hackathons.cs` | Create (gerado) | migration |
| `Dto/CreateHackathonDto.cs` | Modify | campos novos, tipos anuláveis |
| `Dto/HackathonResponseDto.cs` | Create | saída com os nomes de JSON do front |
| `Services/IHackathonService.cs`, `HackathonService.cs` | Create | regras; `HackathonServiceResult<T>`, `HackathonServiceError` |
| `Controllers/HackathonsController.cs` | Modify | rotas da seção 5.3 via service |
| `Models/HackathonDocument.cs` | Delete | — |
| `Program.cs` | Modify | registrar `IHackathonService` |

**Testes (`DampCode_API.Tests/`)**: `HackathonPhaseTests.cs`, `HackathonServiceTests.cs`, `HackathonsEndpointTests.cs`.

**Front (`Damp-Code-Front/src/`)**

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `mock/seed.js` | Modify | Hackathons com ciclo; rascunho do iFood |
| `mock/installMockApi.js` | Modify | `handleHackathons` com as regras da API |
| `app-components/utilities/hackathonDates.js` | Create | ISO ↔ `datetime-local` |
| `Pages/company/CreateHackathon.jsx` | Modify | criar/editar rascunho, publicar |
| `app-components/hackathon-creation/FormSection.jsx` | Modify | campos novos, travas, botões |
| `App.jsx` | Modify | rota `/cadastroHackaton/:id` |
| `app-components/company-dashboard/ManageChallenges.jsx` | Modify | lista da empresa, fases, ações |
| `Pages/company/CompanyDashboard.jsx` | Modify | funcionários voltam a ver criar/gerenciar |
| `Pages/HackathonPage.jsx` | Modify | token opcional, 404, fase e três datas |

---

### Task 1: Entidade no `DbContext`, configuração e migration

**Files:**
- Create: `DampCode_API/Data/Configurations/HackathonConfiguration.cs`, migration gerada
- Modify: `DampCode_API/Models/Hackathon.cs`, `Models/Company.cs`, `Models/Technology.cs`, `Data/DampCodeDbContext.cs`
- Test: `DampCode_API.Tests/HackathonServiceTests.cs` (primeiro teste)

**Interfaces:**
- Produces: em `Hackathon`: `ICollection<HackathonTechnology> Technologies`, `decimal? PrizeAmount`, `HackathonRewardType? RewardType`, `int? WinnersCount`, `string? Logo`, `Company Company`; `public enum HackathonRewardType { Money, Interview }`; `public sealed class HackathonTechnology { Guid HackathonId; Guid TechnologyId; DateTime CreatedAt; Hackathon Hackathon; Technology Technology }` (mesmo formato de `CompanyTechnology`, no `Models/Hackathon.cs`); `Company.Hackathons`, `Technology.Hackathons`; `DampCodeDbContext.Hackathons`, `DampCodeDbContext.HackathonTechnologies`.

- [ ] **Step 1: Teste de persistência** em `HackathonServiceTests` (classe com `_dbContext = TestDb.Create()`): `Hackathon_PersistsWithCompanyAndTechnologies` — cria empresa com `CompanySeed.CompanyAsync`, adiciona `Hackathon` (`Title`, `Slug`, `Description`, `Area`, `CompanyId`, `CreatedByAccountId = company.PrimaryAccountId`) com uma `HackathonTechnology` para uma `Technology { Name = "React", NormalizedName = "REACT" }`; após salvar, `Include(Company).Include(Technologies)` → `Company.Name == "Empresa Exemplo"`, uma tecnologia, `Status == Draft`.
- [ ] **Step 2: Rodar** `dotnet test DampCode_API.Tests --filter Hackathon_PersistsWithCompanyAndTechnologies` → falha de compilação (`Hackathons` não existe no contexto).
- [ ] **Step 3: Implementar** as propriedades, as coleções inversas, os `DbSet` e `HackathonConfiguration` + `HackathonTechnologyConfiguration` (mesmo arquivo, como em `TechnologyConfiguration.cs`), com os valores da seção 4.2 do spec:
  - colunas `snake_case`; `title` 200, `slug` 220, `description` 4000, `area` 100, `evaluation_method` 100, cores 7, `logo_storage_key` 500, `logo` `text`, `prize_amount` `HasPrecision(12, 2)`;
  - `ck_hackathons_status` (`status IN ('Draft', 'Scheduled', 'Active', 'InReview', 'Closed', 'Cancelled')`), `ck_hackathons_participation_mode` (`'Individual', 'Team'`), `ck_hackathons_reward_type` (`reward_type IS NULL OR reward_type IN ('Money', 'Interview')`), `ck_hackathons_winners_count` (`winners_count IS NULL OR winners_count IN (1, 10, 15, 20, 50, 100)`), `ck_hackathons_dates` (`starts_at IS NULL OR ends_at IS NULL OR review_ends_at IS NULL OR (starts_at < ends_at AND ends_at < review_ends_at)`);
  - `ux_hackathons_slug`, `ix_hackathons_company_id`, `ix_hackathons_status`, `ix_hackathons_created_by_account_id`; FKs `fk_hackathons_companies` (`WithMany(company => company.Hackathons)`) e `fk_hackathons_accounts` (sem coleção inversa), `Restrict`;
  - `hackathon_technologies`: `pk_hackathon_technologies` (`hackathon_id`, `technology_id`), `ix_hackathon_technologies_technology_id`, `fk_hackathon_technologies_hackathons` (`Cascade`), `fk_hackathon_technologies_technologies` (`Restrict`).
  - Trocar o comentário de `Hackathon.cs` ("Ainda não faz parte do DbContext…") por um que diga que o Hackathon fica no PostgreSQL, ligado à empresa, e que `Status` gravado é só Draft/Scheduled/Closed/Cancelled (o resto é calculado).
- [ ] **Step 4: Rodar o teste** → PASS; `dotnet test DampCode_API.Tests` → todos passam.
- [ ] **Step 5: Gerar migration** `Hackathons` (mesmo comando da etapa anterior, com as variáveis da seção Global Constraints). Conferir que o `Up` só cria `hackathons` e `hackathon_technologies` com os nomes acima (nenhum `IX_` automático).
- [ ] **Step 6: Commit** `Feat: Hackathon no PostgreSQL com tecnologias e migration`.

### Task 2: Fase calculada

**Files:**
- Create: `DampCode_API/Models/HackathonPhase.cs`
- Test: `DampCode_API.Tests/HackathonPhaseTests.cs`

**Interfaces:**
- Produces: `public enum HackathonPhase { Draft, Published, Active, InReview, Closed, Cancelled }`; `public static class HackathonPhases { public static HackathonPhase GetPhase(Hackathon hackathon, DateTime now); public static string ToText(HackathonPhase phase); }`.

- [ ] **Step 1: Testes** (`[Theory]` onde couber, com `StartsAt = T`, `EndsAt = T+2d`, `ReviewEndsAt = T+4d`):
  - `GetPhase_StoredStatus` → `Draft`→Draft, `Cancelled`→Cancelled, `Closed`→Closed (independente das datas).
  - `GetPhase_Scheduled_FollowsDates`: `now = T-1s` → Published; `now = T` → Active; `now = T+2d-1s` → Active; `now = T+2d` → InReview; `now = T+4d-1s` → InReview; `now = T+4d` → Closed.
  - `ToText_ReturnsPortugueseNames` → os seis textos da Global Constraints.
- [ ] **Step 2: Rodar** `dotnet test DampCode_API.Tests --filter HackathonPhaseTests` → falha de compilação.
- [ ] **Step 3: Implementar** conforme a tabela 4.3. `Scheduled` com `StartsAt` nulo → Published; `EndsAt`/`ReviewEndsAt` nulos são tratados como "ainda não chegou".
- [ ] **Step 4: Rodar** → PASS.
- [ ] **Step 5: Commit** `Feat: fase do Hackathon calculada pelas datas`.

### Task 3: `HackathonService` — criar, editar, apagar

**Files:**
- Create: `Services/IHackathonService.cs`, `Services/HackathonService.cs`, `Dto/HackathonResponseDto.cs`
- Modify: `Dto/CreateHackathonDto.cs`
- Test: `DampCode_API.Tests/HackathonServiceTests.cs`

**Interfaces:**
- Consumes: `ICompanyAccessService.GetManagedCompanyAsync` (etapa anterior); `HackathonPhases` (Task 2).
- Produces:
  - `CreateHackathonDto`: `Premiacao` → `decimal?`, `DataFinal` → `DateTime?`; novos `DateTime? DataInicio`, `DateTime? DataFimAvaliacao`, `string? Formato`, `string? TipoPremiacao`. Nenhum campo removido.
  - `public sealed record HackathonResponseDto(string HackathonId, string Titulo, string Descricao, string Area, string Empresa, IReadOnlyList<string> Tecnologias, string? Metodo, string? Ranking, decimal Premiacao, string? CorPrincipal, string? CorSecundaria, string? CorFundo, string? Logo, DateTime DataCriacao, DateTime? DataFinal, bool Status, string Participantes, string Fase, DateTime? DataInicio, DateTime? DataFimAvaliacao, string Formato, string? TipoPremiacao, Guid CompanyId);` (camelCase padrão gera exatamente os nomes da seção 5.1).
  - `public sealed record HackathonServiceResult<T>(bool Succeeded, T? Value = default, HackathonServiceError Error = HackathonServiceError.None, string? Message = null);` `public enum HackathonServiceError { None, NotFound, Forbidden, Conflict, Validation }` (em `IHackathonService.cs`, como no `ICompanyService.cs`).
  - `HackathonService(DampCodeDbContext dbContext, ICompanyAccessService companyAccessService)` com:
    - `Task<HackathonServiceResult<HackathonResponseDto>> CreateAsync(Guid accountId, CreateHackathonDto dto, CancellationToken cancellationToken)`
    - `Task<HackathonServiceResult<HackathonResponseDto>> UpdateAsync(Guid accountId, Guid hackathonId, CreateHackathonDto dto, CancellationToken cancellationToken)`
    - `Task<HackathonServiceResult<bool>> DeleteAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken)`

Regras que os testes fixam (o resto segue o spec 5.2/5.4):
- Sem empresa gerenciada → `Forbidden` "Apenas empresas e seus funcionários podem gerenciar Hackathons.". Hackathon inexistente ou de outra empresa → `NotFound` "Hackathon não encontrado.".
- Básico faltando (título, descrição, área, formato) → `Validation` "Preencha: título, descrição, área, formato." (só os que faltam, nessa ordem). `Formato`, `TipoPremiacao` e `Ranking` com valor fora da lista contam como vazios (`"Top N"` só para N em 1, 10, 15, 20, 50, 100).
- `Logo.Length > 2 * 1024 * 1024` → `Validation` "O logo deve ter no máximo 2 MB.".
- Três datas presentes e fora de ordem → `Validation` "As datas precisam seguir: início, fim das submissões, fim da avaliação." (vale para criar e editar rascunho; protege a `ck_hackathons_dates`).
- Slug: minúsculas, sem acento (`NormalizationForm.FormD` sem `NonSpacingMark`), tudo que não for letra/dígito vira `-`, sem `-` repetido nem nas pontas, até 200 caracteres, `"hackathon"` se ficar vazio; se existir, `-2`, `-3`…. Não muda na edição.
- Tecnologias: resolver como o `ResolveTechnologiesAsync` do `AuthService` (cópia privada no service; o `AuthService` não muda). Na edição, remover as que saíram e adicionar só as novas (nunca limpar e readicionar a mesma chave).
- Edição: fase `Draft` → substitui todos os campos editáveis; `Published`/`Active`/`InReview` → aplica só `corPrincipal`, `corSecundaria`, `corFundo`, `Logo` e ignora o resto; `Closed`/`Cancelled` → `Conflict` "Este Hackathon já foi encerrado ou cancelado.".
- Apagar: só `Draft` (remoção física) → senão `Conflict` "Só é possível apagar rascunhos. Use cancelar.".
- Resposta: `Empresa = Company.Name`, `Ranking = WinnersCount is null ? null : $"Top {WinnersCount}"`, `Premiacao = PrizeAmount ?? 0`, `Status = fase is Published or Active`, `Participantes = "0 pessoas já entraram"`, `Formato` `Individual`/`Equipe`, `TipoPremiacao` `Dinheiro`/`Entrevista`/null, `Tecnologias` em ordem alfabética.

- [ ] **Step 1: Testes** (helper `CompleteDto()` com todos os cruciais: `Titulo = "Desafio de Rotas"`, descrição, `Area = "web"`, `Tecnologias = ["React", "Node"]`, `Metodo = "Avaliação Manual"`, `Ranking = "Top 10"`, `Premiacao = 5000`, `Formato = "Equipe"`, `TipoPremiacao = "Dinheiro"`, datas `UtcNow + 2d / 5d / 8d`; helper `PublishedAsync(Company company, DateTime startsAt)` grava direto um Hackathon `Scheduled` completo com `EndsAt = startsAt + 3d`, `ReviewEndsAt = startsAt + 6d`):
  - `Create_UnverifiedCompany_CreatesDraft` → `Fase == "Rascunho"`, `Empresa == "Empresa Exemplo"`, `CreatedByAccountId` = conta, `Ranking == "Top 10"`, `Formato == "Equipe"`.
  - `Create_Employee_CreatesDraftForCompany` → membership ativo cria no `CompanyId` da empresa.
  - `Create_Participant_ReturnsForbidden`.
  - `Create_MissingBasics_ReturnsValidation` → dto só com `Titulo` → mensagem "Preencha: descrição, área, formato.".
  - `Create_DuplicateTitle_GeneratesSuffixedSlug` → dois "Desafio de Rotas" → slugs `desafio-de-rotas` e `desafio-de-rotas-2`.
  - `Create_SlugWithoutAccents` → "Café & Código!" → `cafe-codigo`; "!!!" → `hackathon`.
  - `Create_DatesWithOffset_StoredAsUtc` → `DataInicio = new DateTimeOffset(2030, 1, 10, 9, 0, 0, TimeSpan.FromHours(-3)).UtcDateTime.ToLocalTime()` (`Kind` Local) e `DataFinal = new DateTime(2030, 1, 12, 12, 0, 0, DateTimeKind.Unspecified)` → gravados `2030-01-10 12:00` e `2030-01-12 12:00`, ambos com `Kind == Utc`.
  - `Create_LogoOver2Mb_ReturnsValidation`.
  - `Update_Draft_ReplacesAllFields` → muda título, `Premiacao = null` limpa `PrizeAmount`; slug igual.
  - `Update_Draft_ReplacesTechnologies` → `["React", "Node"]` → `["Node", "SQL"]` → `["Node", "SQL"]`.
  - `Update_DraftDatesOutOfOrder_ReturnsValidation`.
  - `Update_Published_IgnoresCrucialFields` → título e prêmio no banco não mudam; `corPrincipal` muda.
  - `Update_Cancelled_ReturnsConflict`.
  - `Update_OtherCompany_ReturnsNotFound`.
  - `Delete_Draft_RemovesIt`; `Delete_Published_ReturnsConflict`.
- [ ] **Step 2: Rodar** `dotnet test DampCode_API.Tests --filter HackathonServiceTests` → falha de compilação.
- [ ] **Step 3: Implementar** DTOs, interface (só os três métodos desta task por enquanto) e service. Helpers privados no estilo do `CompanyService`: `Fail<T>`, `Normalize`, `ToUtc(DateTime?)`, `ToDto(Hackathon, DateTime now)`, `FindManagedAsync` (empresa da conta + Hackathon dela com `Company` e `Technologies.Technology`).
- [ ] **Step 4: Rodar** → PASS; `dotnet test DampCode_API.Tests` → todos passam.
- [ ] **Step 5: Commit** `Feat: criar, editar e apagar rascunho de Hackathon`.

### Task 4: `HackathonService` — publicar, cancelar, encerrar e listagens

**Files:**
- Modify: `Services/IHackathonService.cs`, `Services/HackathonService.cs`
- Test: `DampCode_API.Tests/HackathonServiceTests.cs`

**Interfaces:**
- Produces (no `IHackathonService`):
  - `Task<HackathonServiceResult<HackathonResponseDto>> PublishAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken)`
  - `Task<HackathonServiceResult<HackathonResponseDto>> CancelAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken)`
  - `Task<HackathonServiceResult<HackathonResponseDto>> CloseAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken)`
  - `Task<IReadOnlyList<HackathonResponseDto>> GetPublicAsync(CancellationToken cancellationToken)`
  - `Task<HackathonServiceResult<IReadOnlyList<HackathonResponseDto>>> GetCompanyHackathonsAsync(Guid accountId, CancellationToken cancellationToken)`
  - `Task<HackathonServiceResult<HackathonResponseDto>> GetByIdAsync(Guid? accountId, string id, CancellationToken cancellationToken)` (`id` = Guid ou slug)

Ordem das checagens ao publicar: fase ≠ Draft → `Conflict` "Este Hackathon já foi publicado."; empresa não verificada (`CanPublish == false`) → `Forbidden` "Sua empresa precisa ser verificada para publicar."; cruciais faltando → `Validation` "Preencha antes de publicar: <campos>." na ordem `título, descrição, área, tecnologias, formato, data de início, fim das submissões, fim da avaliação, tipo de premiação, premiação, quantos ganham, método de avaliação` (`premiação` só exigida, e > 0, quando `RewardType == Money`); `StartsAt <= agora` → "A data de início precisa ser no futuro."; ordem → mensagem de ordem. Sucesso: `Status = Scheduled`, `ScheduledAt = agora`.

Cancelar: fase `Closed`/`Cancelled` → `Conflict` "Este Hackathon já foi encerrado ou cancelado."; senão `Cancelled`, `CancelledAt`. Encerrar: só fase `InReview` → `Closed`, `ClosedAt`; senão `Conflict` "Só é possível encerrar durante a avaliação.".

Listagens: públicas = `Status` `Scheduled` ou `Closed`, ordenadas por (fase == Closed) e depois `StartsAt` crescente; empresa = todos dela, `CreatedAt` decrescente; por id = público se a fase não for Draft/Cancelled, senão só para quem gerencia a empresa dona, senão `NotFound`. Fase calculada em memória depois da consulta.

- [ ] **Step 1: Testes**:
  - `Publish_VerifiedComplete_SchedulesIt` → `Fase == "Publicado"`, `ScheduledAt` preenchido.
  - `Publish_UnverifiedCompany_ReturnsForbidden`.
  - `Publish_MissingFields_ListsThem` → rascunho só com o básico → mensagem começa com "Preencha antes de publicar: tecnologias, data de início".
  - `Publish_StartInPast_ReturnsValidation`; `Publish_DatesOutOfOrder_ReturnsValidation` (gravar datas fora de ordem direto no banco).
  - `Publish_InterviewWithoutPrize_Succeeds` (`TipoPremiacao = "Entrevista"`, `Premiacao = null`).
  - `Publish_AlreadyPublished_ReturnsConflict`.
  - `Publish_ByEmployee_Succeeds`.
  - `Update_RemovedEmployee_ReturnsForbidden` (membership com `EndedAt`).
  - `Cancel_Published_CancelsIt`; `Cancel_Cancelled_ReturnsConflict`.
  - `Close_OutsideReview_ReturnsConflict` (publicado futuro); `Close_InReview_ClosesIt` (`PublishedAsync(company, UtcNow - 4d)`).
  - `GetPublic_HidesDraftAndCancelled_ClosedLast` → um rascunho, um cancelado, um encerrado automático (`UtcNow - 10d`), dois publicados → retorna três, encerrado por último, publicados por `StartsAt`.
  - `GetCompanyHackathons_IncludesDrafts`.
  - `GetById_DraftForAnonymous_ReturnsNotFound`; `GetById_DraftForManager_ReturnsIt`; `GetById_BySlug_ReturnsPublished`.
- [ ] **Step 2: Rodar** → falha de compilação.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Rodar** `dotnet test DampCode_API.Tests` → todos passam.
- [ ] **Step 5: Commit** `Feat: publicar, cancelar, encerrar e listar Hackathons`.

### Task 5: `HackathonsController` no service

**Files:**
- Modify: `Controllers/HackathonsController.cs`, `Program.cs`
- Delete: `Models/HackathonDocument.cs`
- Test: `DampCode_API.Tests/HackathonsEndpointTests.cs`

**Interfaces:**
- Consumes: todos os métodos de `IHackathonService` (Tasks 3–4).
- Produces: rotas da seção 5.3 — `GET` `""` e `"{id}"` com `[AllowAnonymous]`; `GET "empresa"`, `POST "register/Hackathon"` (201 + `CreatedAtAction(nameof(GetHackathonById))`), `PUT "{id:guid}"` (200 + dto), `DELETE "{id:guid}"` (204), `POST "{id:guid}/publicar"`, `"{id:guid}/cancelar"`, `"{id:guid}/encerrar"` (200 + dto) com `[Authorize]`. Nomes de método atuais mantidos (`GetAllHackathons`, `CreateHackathon`, `GetHackathonById`, `UpdateHackathon`, `DeleteHackathon`); novos: `GetCompanyHackathons`, `PublishHackathon`, `CancelHackathon`, `CloseHackathon`.

- [ ] **Step 1: Testes** (helpers privados copiados do `CompaniesEndpointTests`: `NewEmail`, `RegisterParticipantAsync`, `RegisterCompanyAsync`, `Request`, `UpdateDatabaseAsync`):
  - `Create_WithoutToken_Returns401`.
  - `Create_Participant_Returns403`.
  - `Create_Company_Returns201WithOldJsonNames` → JSON tem `hackathonId`, `titulo`, `empresa`, `status` (false), `dataFinal`, `fase` ("Rascunho").
  - `GetAll_Anonymous_HidesDraftAndCancelled` → empresa verificada cria e publica um, cria um rascunho e cancela outro publicado; lista anônima só tem o publicado com `status == true`.
  - `GetById_Draft_AnonymousIs404_CompanyIs200`.
  - `Publish_UnverifiedCompany_Returns403WithDetail`.
  - `Delete_Published_Returns409`.
- [ ] **Step 2: Rodar** `dotnet test DampCode_API.Tests --filter HackathonsEndpointTests` → falham (rotas ainda no MongoDB / 405).
- [ ] **Step 3: Implementar**: controller com `HackathonsController(IHackathonService hackathonService)`, `TryGetAccountId` e `ToActionResult` como no `CompaniesController` (sem o caso `Expired`); comentário do topo atualizado ("Hackathons ficam no PostgreSQL; o MongoDB fica para as submissões."). Registrar `builder.Services.AddScoped<IHackathonService, HackathonService>();`. Apagar `HackathonDocument.cs`; `MongoDbService` continua registrado.
- [ ] **Step 4: Rodar** `dotnet test DampCode_API.Tests` → todos passam; `dotnet build DampCode_API` sem warnings novos.
- [ ] **Step 5: Commit** `Feat: rotas de Hackathon no PostgreSQL com ciclo de vida`.

### Task 6: API simulada com o ciclo

**Files:**
- Modify: `Damp-Code-Front/src/mock/seed.js`, `src/mock/installMockApi.js`

**Interfaces:**
- Produces: Hackathon do mock guardado com `hackathonId` (número), `companyId`, `storedStatus` (`Draft`/`Scheduled`/`Closed`/`Cancelled`), `slug`, campos do front (`titulo`, …, `dataFinal`) e `dataInicio`, `dataFimAvaliacao`, `formato`, `tipoPremiacao`, `ranking`, `dataCriacao`; a resposta (`toHackathonResponse`) acrescenta `fase`, `status`, `empresa` (nome da empresa) e `participantes`. Rotas iguais às da Task 5, com `{ detail }` e os mesmos status e mensagens.

- [ ] **Step 1: Seed**: os quatro Hackathons ganham `companyId` (`c-ifood`/`c-nubank`), `storedStatus: "Scheduled"`, formato/tipo coerentes e datas: 1 → início `2026-10-01`, fim `2026-11-20`, avaliação `2026-12-05` (Ativo); 2 → `2026-09-01` / `2026-10-10` / `2026-10-25` (Encerrado); 3 → `2026-11-10` / `2026-12-15` / `2027-01-05` (Publicado); 4 → `2026-12-01` / `2027-01-10` / `2027-01-31` (Publicado). Novo 5: rascunho do iFood "API de cupons" completo e com início futuro (`2027-02-01` / `2027-02-20` / `2027-03-10`). `nextHackathonId: 6`.
- [ ] **Step 2: `loadDatabase`**: se algum Hackathon salvo não tiver `storedStatus`, troca `hackathons` e `nextHackathonId` pelos do seed (mesmo comentário curto dos outros ajustes de banco antigo).
- [ ] **Step 3: `handleHackathons(path, method, body, database, user)`** no lugar dos ramos atuais de Hackathon (remover `isActive`/`toHackathon`, que ficam sem uso), com `phaseOf(hackathon)` espelhando `GetPhase`, `managedCompany` da etapa anterior para permissão, e `GET /api/Hackathons/empresa` testado antes de `/{id}`. `handle` resolve `currentUser` também para `/api/Hackathons`.
- [ ] **Step 4: Verificar** `npm run lint` (22 problemas) e `npm run build`. No console do navegador com o mock: anônimo `GET /api/Hackathons` → 3 itens (sem o rascunho), encerrado por último; iFood `POST /api/Hackathons/5/publicar` → 200 `fase: "Publicado"`; Nubank publicando um rascunho novo → 403 com o `detail` de verificação.
- [ ] **Step 5: Commit** `Feat: ciclo de vida do Hackathon na API simulada`.

### Task 7: Criar e editar rascunho, publicar

**Files:**
- Create: `src/app-components/utilities/hackathonDates.js`
- Modify: `src/Pages/company/CreateHackathon.jsx`, `src/app-components/hackathon-creation/FormSection.jsx`, `src/App.jsx`

**Interfaces:**
- Consumes: rotas da Task 5/6.
- Produces: `export function toDateTimeLocal(isoDate)` (ISO → `"YYYY-MM-DDTHH:mm"` no horário local, `""` se vazio) e `export function toIsoDate(localValue)` (`""` → `null`, senão `new Date(localValue).toISOString()`); rota `/cadastroHackaton/:id` → `CreateHackathon`.

- [ ] **Step 1: `App.jsx`**: nova rota ao lado de `/cadastroHackaton`.
- [ ] **Step 2: `CreateHackathon.jsx`**:
  - estado inicial sem `Empresa` (só para a prévia, preenchido com `companyAccess.name`), `status` e `DataCriacao`; `Area: "tech"` e `Formato: "Individual"` (os valores que os selects mostram); `DataInicio`, `DataFinal`, `DataFimAvaliacao`, `TipoPremiacao` vazios;
  - `useParams().id`: se houver, carrega `GET /api/Hackathons/{id}` com token e preenche o formulário (datas via `toDateTimeLocal`, `ranking` → `Ranking`, `logo` → `Logo`, guarda `fase`);
  - carrega `GET /api/Companies/me` para `canPublish` e o nome da empresa;
  - corpo enviado: sem `logoFile`/`fase`/`Empresa`; datas via `toIsoDate`; `Premiacao` vazio → `null`;
  - "Salvar rascunho" (submit): sem id → `POST register/Hackathon` e `navigate(`/cadastroHackaton/${data.hackathonId}`)` com a mensagem "Rascunho salvo."; com id → `PUT` e "Alterações salvas.";
  - "Publicar": salva (`PUT`) e chama `POST /{id}/publicar`; sucesso → `/dashboard/Empresa`;
  - erros mostram `detail` da resposta no `message` existente.
- [ ] **Step 3: `FormSection.jsx`** (props novas: `isPublished`, `isDraft`, `canPublish`, `handlePublish`, `loading`):
  - remover "Empresa Parceira" e o select "Status";
  - corrigir o `value` do nome para `hackathon.Titulo` (hoje lê `hackathon.Title`, que não existe, e a edição não mostraria o título);
  - adicionar select Formato (Individual/Equipe), "Data de início", "Fim das submissões" (o antigo "Data Final") e "Fim da avaliação" como `datetime-local`, select "Tipo de premiação" (Dinheiro/Entrevista); "Premiação Total" só aparece com Dinheiro;
  - com `isPublished`, todo campo crucial (inclusive os botões de ranking, método e checkboxes) fica `disabled` e aparece o aviso "Depois de publicado, só cores e logo podem ser alterados.";
  - título da seção "Criar" ou "Editar" conforme há rascunho/id;
  - botões: submit "Salvar rascunho" (ou "Salvar alterações" quando publicado); "Publicar" só com `isDraft`, desabilitado sem `canPublish`, com o texto "Sua empresa precisa ser verificada para publicar." abaixo.
- [ ] **Step 4: Verificar** lint (22) e build. Com o mock: iFood cria rascunho → URL vira `/cadastroHackaton/6` → completa e publica → volta ao painel; Nubank vê "Publicar" desabilitado com o aviso; abrir um publicado mostra cruciais desabilitados e salvar cor funciona.
- [ ] **Step 5: Commit** `Feat: criar, editar e publicar rascunho de Hackathon`.

### Task 8: Gerenciar, painel da empresa e página do Hackathon

**Files:**
- Modify: `src/app-components/company-dashboard/ManageChallenges.jsx`, `src/Pages/company/CompanyDashboard.jsx`, `src/Pages/HackathonPage.jsx`

- [ ] **Step 1: `ManageChallenges.jsx`**: busca `GET /api/Hackathons/empresa` com o token do `localStorage` (como o arquivo já lê `user`), no padrão async-dentro-do-effect com contador `reload`. Coluna Status mostra `fase` com cor: Rascunho `bg-white/10 text-content-secondary`; Publicado/Ativo `bg-success/20 text-success`; Em avaliação `bg-warning/20 text-warning`; Encerrado/Cancelado `bg-danger/20 text-danger`. Ações: Rascunho → Editar (`/cadastroHackaton/{id}`), Publicar, Apagar; Publicado/Ativo → Editar, Cancelar; Em avaliação → Encerrar, Cancelar; sempre Detalhes. Apagar e Cancelar com `window.confirm`. Erros (`detail`) numa linha acima da tabela. Coluna "Data Final" mostra `dataFinal` ou "—". PDF usa `fase`.
- [ ] **Step 2: `CompanyDashboard.jsx`**: remover `isEmployee` e o comentário provisório; `CreateChallenge` e `ManageChallenges` sempre visíveis.
- [ ] **Step 3: `HackathonPage.jsx`**: envia o token se houver (`AuthContext`); resposta não ok → "Hackathon não encontrado." no lugar do "Carregando"; na fase Publicado, mostra "Inscrições abertas · começa em dd/mm"; troca o "Termina em" por "Início", "Fim das submissões" e "Fim da avaliação" (`toLocaleDateString("pt-BR")`, sem forçar UTC, pois agora são instantes).
- [ ] **Step 4: Verificar** lint (22) e build. Roteiro com o mock, depois de `localStorage.removeItem("damp-mock-db")` (as Tasks 6 e 7 publicam o rascunho 5): iFood vê os Hackathons dele com as fases; apaga um rascunho, cancela um publicado (some do Explorar); Bruno (após aceitar convite do iFood, se ainda não aceito) vê criar/gerenciar; anônimo abrindo `/hackathon/5` (rascunho) vê "Hackathon não encontrado.".
- [ ] **Step 5: Commit** `Feat: gerenciar Hackathons por fase e funcionários no painel`.
