# Submissões — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Quem está inscrito entrega código (editor ou arquivo de texto) em várias versões, valendo a última aceita, e a empresa compara manualmente com a solução de referência dela, lado a lado.

**Architecture:** `Submission` (já existente) entra no `DbContext` como resumo por participante individual ou equipe; a nova `SubmissionVersion` (PostgreSQL) é dona da lista de versões; o código de cada versão fica no MongoDB atrás de `ISubmissionVersionStore`. `SubmissionService` concentra as regras e devolve `SubmissionServiceResult<T>`; as rotas ficam em `HackathonsController`. No front, duas páginas novas (entrega e revisão da empresa), campos de referência no formulário e a API simulada com as mesmas regras.

**Tech Stack:** ASP.NET Core 8, EF Core 8.0.11 + Npgsql, MongoDB.Driver 2.25, xUnit + EF InMemory + `Microsoft.AspNetCore.Mvc.Testing`; React 19 + Vite + Tailwind.

**Spec:** `docs/superpowers/specs/2026-10-29-submissoes-design.md`

## Global Constraints

- Branch `feat/submissoes` nos dois repositórios (já criadas a partir de `feat/inscricao-e-equipes`; o spec já está commitado na da API).
- Não renomear nada existente. Mudanças aditivas, exceto: `Submission` (`ParticipationId` e `StageId` passam a `Guid?`, propriedades novas, comentário), `Hackathon` (referência), `CreateHackathonDto` (dois campos novos), `HackathonService` (aplicar/validar referência), `HackathonsController` (rotas novas, construtor), `DampCodeDbContext`, `Program.cs`, `ApiFactory` (troca do store), `FormSection.jsx`, `CreateHackathon.jsx`, `ManageChallenges.jsx`, `JoinHackathon.jsx`, `MyParticipations.jsx`, `App.jsx`, mock.
- Estilo igual às etapas anteriores (file-scoped namespace, primary constructors, `sealed`, records de saída, `CancellationToken cancellationToken`, mensagens em português, `ProblemDetails { Detail = Message }`); EF com `snake_case` e `pk_`/`fk_`/`ux_`/`ix_`/`ck_`.
- Linguagens aceitas, exatamente: `JavaScript`, `Python`, `C#`, `Java`, `SQL`, `Outra`.
- Limite de código (versão e referência): `100 * 1024` caracteres.
- Enviar só na fase `HackathonPhase.Active`.
- Mensagens exatamente como na seção 5.3 do spec.
- Coleção MongoDB `SubmissionVersions`; documento `{ _id, submissionId, code, createdAt }`.
- Identidade visual: classes Tailwind atuais (`bg-surface`, `bg-surface-light`, `border-brand`, `bg-highlight`, `text-content`, `text-content-secondary`), `Title`/`HackathonTitle`/`HackathonSubtitle` existentes para títulos.
- `dotnet ef` com `DOTNET_ROLL_FORWARD=Major`, `ConnectionStrings__PostgreSql="Host=localhost;Database=nao_usado"`, `Jwt__Secret` de 32+ caracteres; testes com `dotnet test DampCode_API.Tests`.
- Front: `fetch` em `https://localhost:7092`, `Authorization: Bearer ${user.token}`, ids em URL com `encodeURIComponent`, efeitos com função async dentro do `useEffect`; `npm run lint` sem problemas novos (base 19).
- Commits `Feat:`/`Fix:`/`Test:`/`Docs:` em português, **sem linha de coautoria**.

## Review Focus

- Documento órfão no MongoDB (envio caiu entre Mongo e PostgreSQL) → o envio seguinte funciona e a lista de versões não mostra o órfão. (Task 3: `Submit_AfterOrphanDocument_Succeeds`)
- Código com exatamente 100 × 1024 caracteres → aceito; com um a mais → 400. (Task 3: `Submit_SizeLimit_Boundary`)
- Quem saiu da equipe antes do início (sem inscrição ativa) tenta ver ou enviar → 403. (Task 3: `GetMine_LeftParticipation_ReturnsForbidden`)
- Solução de referência nunca vaza para quem participa: JSON de `GET /api/Hackathons/{id}` e da listagem sem campos de referência. (Task 5: `HackathonJson_DoesNotExposeReference`)
- Envio exatamente em `EndsAt` → fase Em avaliação → 409 "As submissões deste Hackathon estão fechadas." (Task 3: `Submit_AtEndsAt_ReturnsClosed`)

---

## File Structure

**API (`DampCode_API/`)**

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `Models/Submission.cs` | Modify | resumo da entrega (props novas, anuláveis, comentário) |
| `Models/SubmissionVersion.cs` | Create | linha de versão no PostgreSQL |
| `Models/Hackathon.cs` | Modify | `ReferenceLanguage`, `ReferenceCode`, `Submissions` |
| `Data/Configurations/SubmissionConfiguration.cs` | Create | `submissions` e `submission_versions` |
| `Data/Configurations/HackathonConfiguration.cs` | Modify | colunas da referência |
| `Data/DampCodeDbContext.cs` | Modify | `Submissions`, `SubmissionVersions` |
| `Data/Migrations/*_Submissions.cs` | Create (gerado) | migration |
| `Data/ISubmissionVersionStore.cs`, `Data/MongoSubmissionVersionStore.cs` | Create | código das versões no MongoDB |
| `Dto/SubmissionDtos.cs`, `Dto/SubmitCodeDto.cs` | Create | saída e entrada |
| `Dto/CreateHackathonDto.cs` | Modify | `LinguagemReferencia`, `SolucaoReferencia` |
| `Services/ISubmissionService.cs`, `SubmissionService.cs` | Create | regras; `SubmissionServiceResult<T>`, `SubmissionServiceError` |
| `Services/HackathonService.cs` | Modify | salvar e validar a referência |
| `Controllers/HackathonsController.cs` | Modify | rotas 5.2 |
| `Program.cs` | Modify | registrar store e serviço |

**Testes:** `SubmissionServiceTests.cs`, `SubmissionsEndpointTests.cs`, `InMemorySubmissionVersionStore.cs` (fake), casos novos em `HackathonServiceTests.cs`; `ApiFactory.cs` troca o store.

**Front (`Damp-Code-Front/src/`)**

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `mock/seed.js`, `mock/installMockApi.js` | Modify | dados e rotas da etapa |
| `Pages/SubmissionPage.jsx` | Create | entrega de código (`/hackathon/:id/entrega`) |
| `Pages/company/SubmissionsReview.jsx` | Create | comparação da empresa (`/dashboard/Empresa/entregas/:id`) |
| `App.jsx` | Modify | as duas rotas |
| `app-components/hackathon-page/JoinHackathon.jsx`, `participant-dashboard/MyParticipations.jsx` | Modify | link "Entregar código" |
| `app-components/hackathon-creation/FormSection.jsx`, `Pages/company/CreateHackathon.jsx` | Modify | solução de referência |
| `app-components/company-dashboard/ManageChallenges.jsx` | Modify | ação "Entregas" |

---

### Task 1: Entidades, configuração e migration

**Files:** Modify `Models/Submission.cs`, `Models/Hackathon.cs`, `Data/Configurations/HackathonConfiguration.cs`, `Data/DampCodeDbContext.cs`; Create `Models/SubmissionVersion.cs`, `Data/Configurations/SubmissionConfiguration.cs`, migration; Test `DampCode_API.Tests/SubmissionServiceTests.cs`.

**Interfaces:**
- Produces:
  - `Submission`: + `Guid HackathonId`, `Guid? TeamId`, `Hackathon Hackathon`, `HackathonTeam? Team`, `Participation? Participation`, `ICollection<SubmissionVersion> Versions`; `ParticipationId` e `StageId` → `Guid?`; `CurrentVersion` token de concorrência.
  - `SubmissionVersion { Guid Id; Guid SubmissionId; int Version; string Language; string? FileName; string MongoDocumentId; Guid SubmittedByAccountId; DateTime SubmittedAt; Submission Submission; Account SubmittedByAccount }`.
  - `Hackathon`: + `string? ReferenceLanguage`, `string? ReferenceCode`, `ICollection<Submission> Submissions`.
  - `DbSet`s `Submissions`, `SubmissionVersions`.

- [ ] **Step 1: Teste** `Submission_PersistsWithVersions`: Hackathon de equipe (`ParticipationSeed`), equipe + participação; grava `Submission { HackathonId, TeamId, Status = "Submitted", CurrentVersion = 1 }` com uma `SubmissionVersion { Version = 1, Language = "Python", MongoDocumentId = "abc", SubmittedByAccountId }`; recarrega com `Include(Versions)` → uma versão, `ParticipationId == null`, `StageId == null`.
- [ ] **Step 2: Rodar** `--filter Submission_PersistsWithVersions` → falha de compilação.
- [ ] **Step 3: Implementar** (spec 4.1):
  - `submissions`: colunas `snake_case` das props existentes e novas (`status` 32), `ck_submissions_owner` (`(participation_id IS NULL) <> (team_id IS NULL)`), `ux_submissions_participation` (`participation_id`, filtro `participation_id IS NOT NULL`), `ux_submissions_team` (`team_id`, filtro `team_id IS NOT NULL`), `ix_submissions_hackathon_id`; FKs `fk_submissions_hackathons` (Cascade, `WithMany(h => h.Submissions)`), `fk_submissions_hackathon_teams` e `fk_submissions_participations` (`DeleteBehavior.NoAction`, sem inversa); `CurrentVersion` `.IsConcurrencyToken()`; `CurrentMongoVersionId` 24; `stage_id` sem FK.
  - `submission_versions`: `language` 32, `file_name` 255, `mongo_document_id` 24, `ux_submission_versions_version` (`submission_id`, `version`), FKs `fk_submission_versions_submissions` (Cascade, `WithMany(s => s.Versions)`) e `fk_submission_versions_accounts` (Restrict, sem inversa), `ix_submission_versions_submitted_by_account_id`.
  - `hackathons`: `reference_language` 32, `reference_code` `text`.
  - Comentário de `Submission.cs`: entrega de um participante individual ou de uma equipe; o código de cada versão fica no MongoDB, a lista de versões no PostgreSQL; `StageId` reservado.
- [ ] **Step 4: Rodar** suíte → passa.
- [ ] **Step 5: Migration** `Submissions`; conferir que só cria `submissions`, `submission_versions` e adiciona as duas colunas em `hackathons`, com os nomes acima.
- [ ] **Step 6: Commit** `Feat: entidades de submissão com migration`.

### Task 2: Código no MongoDB

**Files:** Create `Data/ISubmissionVersionStore.cs`, `Data/MongoSubmissionVersionStore.cs`, `DampCode_API.Tests/InMemorySubmissionVersionStore.cs`; Modify `Program.cs`, `DampCode_API.Tests/ApiFactory.cs`; Test `SubmissionServiceTests.cs`.

**Interfaces:**
- Produces: `public interface ISubmissionVersionStore { Task<string> InsertAsync(Guid submissionId, string code, CancellationToken cancellationToken); Task<string?> GetCodeAsync(string documentId, CancellationToken cancellationToken); Task DeleteAsync(string documentId, CancellationToken cancellationToken); }`; `MongoSubmissionVersionStore(MongoDbService mongoDbService)` sobre a coleção `SubmissionVersions` (documento `SubmissionCodeDocument` com `[BsonId][BsonRepresentation(ObjectId)] string? Id`, `SubmissionId`, `Code`, `CreatedAt`, no mesmo arquivo); `internal sealed class InMemorySubmissionVersionStore : ISubmissionVersionStore` (dicionário; `_id` = `ObjectId` de 24 caracteres hex gerado; propriedade `Documents` para os testes; `FailNextDelete` opcional não é necessário). Registro `builder.Services.AddSingleton<ISubmissionVersionStore, MongoSubmissionVersionStore>();`; `ApiFactory` troca por `InMemorySubmissionVersionStore` (singleton).

- [ ] **Step 1: Teste** `InMemoryStore_RoundTrip` (insere, lê o código, apaga, lê nulo) — garante o fake usado nas próximas tasks.
- [ ] **Step 2: Rodar** → falha de compilação.
- [ ] **Step 3: Implementar** interface, Mongo (`InsertOneAsync`, `Find(...).FirstOrDefaultAsync`, `DeleteOneAsync`) e o fake; registrar; trocar no `ApiFactory` (`services.RemoveAll<ISubmissionVersionStore>(); services.AddSingleton<ISubmissionVersionStore, InMemorySubmissionVersionStore>();`).
- [ ] **Step 4: Rodar** suíte → passa (inclusive os testes de endpoint, que não podem tentar conectar no Mongo).
- [ ] **Step 5: Commit** `Feat: armazenamento do código das versões no MongoDB`.

### Task 3: Serviço — entrega de quem participa

**Files:** Create `Services/ISubmissionService.cs`, `Services/SubmissionService.cs`, `Dto/SubmissionDtos.cs`, `Dto/SubmitCodeDto.cs`; Test `SubmissionServiceTests.cs`.

**Interfaces:**
- Consumes: `ISubmissionVersionStore` (Task 2), `HackathonPhases`, `Participation` (etapa 3).
- Produces:
  - `SubmitCodeDto { string? Linguagem; string? Codigo; string? NomeArquivo }`.
  - Em `SubmissionDtos.cs`: `SubmissionVersionDto(int Versao, string Linguagem, string? NomeArquivo, string EnviadoPor, DateTime EnviadoEm, bool Vale)`, `SubmissionCodeDto(int Versao, string Linguagem, string? NomeArquivo, string Codigo)`, `MySubmissionDto(bool Aberta, DateTime? DataInicio, DateTime? DataFinal, string? Equipe, IReadOnlyList<SubmissionVersionDto> Versoes, SubmissionCodeDto? Atual)`, `SubmissionEntryDto(Guid? SubmissionId, string Nome, int? VersaoAtual, DateTime? EnviadoEm)`, `SubmissionDetailDto(Guid SubmissionId, string Nome, IReadOnlyList<SubmissionVersionDto> Versoes, SubmissionCodeDto? Atual)`, `ReferenceSolutionDto(string? Linguagem, string? Codigo)`.
  - `SubmissionServiceResult<T>(bool Succeeded, T? Value = default, SubmissionServiceError Error = None, string? Message = null)`, `enum SubmissionServiceError { None, NotFound, Forbidden, Conflict, Validation }`.
  - `SubmissionService(DampCodeDbContext dbContext, ICompanyAccessService companyAccessService, ISubmissionVersionStore versionStore)` com:
    - `Task<SubmissionServiceResult<MySubmissionDto>> GetMineAsync(Guid accountId, Guid hackathonId, CancellationToken)`
    - `Task<SubmissionServiceResult<SubmissionCodeDto>> GetMyVersionAsync(Guid accountId, Guid hackathonId, int version, CancellationToken)`
    - `Task<SubmissionServiceResult<SubmissionCodeDto>> SubmitAsync(Guid accountId, Guid hackathonId, SubmitCodeDto dto, CancellationToken)`
  - Helper estático público para reuso no `HackathonService`: `public static class SubmissionRules { public const int MaxCodeLength = 100 * 1024; public static readonly string[] Languages = ["JavaScript", "Python", "C#", "Java", "SQL", "Outra"]; }` (em `SubmissionService.cs`).

Regras: Hackathon inexistente/rascunho/cancelado → 404; sem participação ativa → 403 "Você não está inscrito neste Hackathon."; a entrega é a `Submission` da participação (individual) ou da equipe da participação; `EnviadoPor` = `UserProfile.Name` ou e-mail; `Vale` = versão == `CurrentVersion`; `Aberta` = fase Active. Enviar: ordem de validação fase (409) → linguagem (400 "Escolha uma linguagem.") → vazio/só espaços (400 "Envie algum código.") → tamanho (400); depois o fluxo da seção 4.3 do spec, tratando `DbUpdateConcurrencyException` e `DbUpdateException` (409 "Outra versão foi enviada ao mesmo tempo. Tente novamente." e `DeleteAsync` do documento em try/catch).

- [ ] **Step 1: Testes** (Hackathon individual e de equipe com `startsAt = UtcNow - 1h` para ficar Ativo; helpers locais para inscrever direto no banco):
  - `Submit_FirstVersion_CreatesSubmission` (individual) → `Versao == 1`, `Submission.ParticipationId` preenchido, código no store.
  - `Submit_SecondVersion_BecomesCurrent` → `GetMineAsync` mostra 2 versões, só a v2 com `Vale`, `Atual.Codigo` = segundo código.
  - `Submit_TeamMembers_ShareSubmission` → Ana envia v1, Bruno envia v2 na mesma `Submission` (`TeamId`), `EnviadoPor` "Ana Souza" e "Bruno Lima".
  - `Submit_NotRegistered_ReturnsForbidden`; `GetMine_LeftParticipation_ReturnsForbidden`.
  - `Submit_BeforeStart_ReturnsClosed` (`startsAt` futuro) e `Submit_AtEndsAt_ReturnsClosed` (`StartsAt = UtcNow - 3d`, `EndsAt = UtcNow`) → 409 "As submissões deste Hackathon estão fechadas.".
  - `Submit_InvalidLanguage_ReturnsValidation`; `Submit_EmptyCode_ReturnsValidation` (`"   "`).
  - `Submit_SizeLimit_Boundary` (`[Theory]` 102400 → ok, 102401 → 400 "O código deve ter no máximo 100 KB.").
  - `Submit_ConcurrentVersion_ReturnsConflictAndDeletesDocument`: rastreia a `Submission` no contexto do serviço com `CurrentVersion = 1`, outro contexto (`TestDb.CreateShared`) grava `CurrentVersion = 2`; `SubmitAsync` → 409 e o store volta a ter só o documento da v1.
  - `Submit_AfterOrphanDocument_Succeeds`: insere um documento direto no store para a submissão (órfão); `SubmitAsync` → ok, `Versoes.Count == 2` (v1 + nova), órfão não listado.
  - `GetMyVersion_Existing_ReturnsCode`; `GetMyVersion_Unknown_ReturnsNotFound` ("Versão não encontrada.").
  - `GetMine_NoSubmission_ReturnsEmpty` → `Versoes` vazio, `Atual == null`, `Equipe` = nome da equipe (Hackathon de equipe).
- [ ] **Step 2: Rodar** `--filter SubmissionServiceTests` → falha de compilação.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Rodar** suíte → passa.
- [ ] **Step 5: Commit** `Feat: entrega de código com versões`.

### Task 4: Serviço — visão da empresa e solução de referência

**Files:** Modify `Services/ISubmissionService.cs`, `Services/SubmissionService.cs`, `Services/HackathonService.cs`, `Dto/CreateHackathonDto.cs`; Test `SubmissionServiceTests.cs`, `HackathonServiceTests.cs`.

**Interfaces:**
- Produces:
  - `Task<SubmissionServiceResult<IReadOnlyList<SubmissionEntryDto>>> GetEntriesAsync(Guid accountId, Guid hackathonId, CancellationToken)` — uma linha por equipe com membro ativo (equipe) ou por participação ativa (individual), ordenada por nome; `SubmissionId`/`VersaoAtual`/`EnviadoEm` nulos para quem não entregou.
  - `Task<SubmissionServiceResult<SubmissionDetailDto>> GetEntryAsync(Guid accountId, Guid hackathonId, Guid submissionId, CancellationToken)`
  - `Task<SubmissionServiceResult<SubmissionCodeDto>> GetEntryVersionAsync(Guid accountId, Guid hackathonId, Guid submissionId, int version, CancellationToken)`
  - `Task<SubmissionServiceResult<ReferenceSolutionDto>> GetReferenceAsync(Guid accountId, Guid hackathonId, CancellationToken)`
  - Acesso da empresa: mesma regra do `HackathonService` (sem empresa → 403 "Apenas empresas e seus funcionários podem gerenciar Hackathons."; Hackathon de outra empresa → 404 "Hackathon não encontrado.").
  - `CreateHackathonDto`: `string? LinguagemReferencia`, `string? SolucaoReferencia`. `HackathonService`: `ApplyVisual` (aplicada em rascunho e publicado) passa a gravar `ReferenceLanguage`/`ReferenceCode` (`Blank`); `ValidateVisual` valida linguagem da referência (se houver código: 400 "Escolha uma linguagem.") e tamanho (400 "O código deve ter no máximo 100 KB."). `HackathonResponseDto` **não** muda.

- [ ] **Step 1: Testes:**
  - `GetEntries_ListsTeamsWithAndWithoutSubmission` (duas equipes, uma entregou) → 2 linhas, uma com `VersaoAtual == 1`, outra com `SubmissionId == null`.
  - `GetEntries_Individual_ListsParticipants`.
  - `GetEntries_OtherCompany_ReturnsNotFound`; `GetEntries_Participant_ReturnsForbidden`.
  - `GetEntry_ReturnsVersionsAndCurrentCode`; `GetEntryVersion_ReturnsOldCode`.
  - `GetReference_ReturnsCompanyCode`.
  - `HackathonServiceTests`: `Update_Published_SavesReference` (referência muda, título não); `Create_ReferenceOver100Kb_ReturnsValidation`; `Create_ReferenceWithoutLanguage_ReturnsValidation`.
- [ ] **Step 2: Rodar** → falham.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Rodar** suíte → passa.
- [ ] **Step 5: Commit** `Feat: entregas e solução de referência para a empresa`.

### Task 5: Rotas

**Files:** Modify `Controllers/HackathonsController.cs`, `Program.cs`; Test `SubmissionsEndpointTests.cs`.

**Interfaces:**
- Consumes: `ISubmissionService` (Tasks 3–4).
- Produces: rotas da seção 5.2 do spec em `HackathonsController` (construtor ganha `ISubmissionService submissionService`): `GET "{id:guid}/entrega"`, `GET "{id:guid}/entrega/versoes/{versao:int}"`, `POST "{id:guid}/entrega"` (201), `GET "{id:guid}/entregas"`, `GET "{id:guid}/entregas/{submissionId:guid}"`, `GET "{id:guid}/entregas/{submissionId:guid}/versoes/{versao:int}"`, `GET "{id:guid}/referencia"`; métodos `GetMySubmission`, `GetMySubmissionVersion`, `SubmitCode`, `GetSubmissions`, `GetSubmission`, `GetSubmissionVersion`, `GetReferenceSolution`; mapeamento de erro como os outros (`ToActionResult` para `SubmissionServiceResult<T>`). Registrar `AddScoped<ISubmissionService, SubmissionService>()`.

- [ ] **Step 1: Testes** (helpers copiados dos `ParticipationsEndpointTests`; Hackathon publicado via API e depois `UpdateDatabaseAsync` muda `StartsAt` para o passado):
  - `Submit_WithoutToken_Returns401`.
  - `Submit_ThenGetMine_ShowsVersion` → 201, `GET /entrega` traz `atual.codigo`.
  - `Company_SeesEntriesAndReference` → `GET /entregas` 200 com 1 linha, `GET /referencia` 200.
  - `Participant_GetEntries_Returns403`.
  - `HackathonJson_DoesNotExposeReference` → `GET /api/Hackathons/{id}` e `GET /api/Hackathons` sem as chaves `solucaoReferencia`, `linguagemReferencia`, `referenceCode`.
- [ ] **Step 2: Rodar** → falham.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Rodar** suíte → passa; build sem warnings novos.
- [ ] **Step 5: Commit** `Feat: rotas de submissão`.

### Task 6: API simulada

**Files:** Modify `src/mock/seed.js`, `src/mock/installMockApi.js`.

- [ ] **Step 1: Seed:** usuária `{ id: 5, name: "Carla Dias", email: "carla@dampcode.dev", password: "123456", role: "participante" }` (e `MOCK_ACCOUNTS`; `nextUserId: 6`); equipe `t-2` "Turma do Bit" no Hackathon 1 com Ana e Carla (participações `p-3`, `p-4`); `submissions: [{ submissionId: "s-1", hackathonId: 1, teamId: "t-2", participationId: null, currentVersion: 2 }]`, `submissionVersions` com v1 (Ana, Python) e v2 (Carla, Python) com `code` direto no objeto; Hackathon 1 com `referenceLanguage: "Python"` e `referenceCode`. Contadores `nextSubmissionId`, `nextTeamId: 3`, `nextParticipationId: 5`. `loadDatabase` acrescenta as listas novas em bancos antigos e insere Carla se faltar (laço de usuários já existente).
- [ ] **Step 2: `handleSubmissions(path, method, body, database, user)`** roteado em `handle` antes de `handleHackathons` para `/api/Hackathons/{id}/(entrega|entregas|referencia)…`, com as regras e mensagens das Tasks 3–5; `PUT` do Hackathon (em `handleHackathons`) passa a gravar `LinguagemReferencia`/`SolucaoReferencia` em qualquer fase antes de encerrado; `toHackathonResponse` remove `referenceLanguage`/`referenceCode` do objeto devolvido.
- [ ] **Step 3: Verificar** lint (≤ 19, nenhum novo), build e script Node no scratchpad: entrega da Turma do Bit com 2 versões; Ana envia v3; Bruno (não inscrito no 1) → 403; Hackathon 3 (Publicado) → 409 fechado; iFood vê entregas e referência; JSON público sem referência.
- [ ] **Step 4: Commit** `Feat: submissões na API simulada`.

### Task 7: Página de entrega

**Files:** Create `src/Pages/SubmissionPage.jsx`; Modify `src/App.jsx`, `src/app-components/hackathon-page/JoinHackathon.jsx`, `src/app-components/participant-dashboard/MyParticipations.jsx`.

- [ ] **Step 1: `SubmissionPage.jsx`** (spec 6, opção B): `Header`/`Footer`, `BackButton`, fundo `corFundo` e `HackathonTitle` como `HackathonPage`; carrega `GET /api/Hackathons/{id}` (título/cores) e `GET /entrega`. Seletor de linguagem (lista da Global Constraints), `textarea` monoespaçada (`font-mono`, mín. 20 linhas), input de arquivo (`accept=".js,.py,.cs,.java,.sql,.txt"`, `FileReader.readAsText`, recusa no navegador acima de 100 KB com a mesma mensagem), "Enviar versão" (POST, mostra `detail` de erro, recarrega), lista de versões (clicar carrega `GET /entrega/versoes/{n}` em modo só leitura; "Voltar à versão atual"). Fora da fase: editor/botão desabilitados com "As submissões abrem em dd/mm às hh:mm" (antes do início) ou "As submissões foram encerradas" (depois). 403 da API → mensagem "Você não está inscrito neste Hackathon." com link para a página do Hackathon.
- [ ] **Step 2: Rota** `/hackathon/:id/entrega` em `App.jsx`.
- [ ] **Step 3: Links:** `JoinHackathon` mostra o botão "Entregar código" (Link, cor principal) para quem tem `participation`, em qualquer fase; `MyParticipations` mostra "Entregar código" ao lado de cada inscrição que não esteja cancelada.
- [ ] **Step 4: Verificar** lint e build. Com o mock (após `localStorage.removeItem("damp-mock-db")`), como Ana: Hackathon 1 → "Entregar código" → página com v1/v2 da Turma do Bit, v2 valendo; envia v3 digitando; carrega arquivo `.py` e envia v4; vê v1 só leitura; Hackathon 4 (Publicado) → editor desabilitado com a data de abertura.
- [ ] **Step 5: Commit** `Feat: página de entrega de código`.

### Task 8: Empresa — referência, entregas e comparação

**Files:** Create `src/Pages/company/SubmissionsReview.jsx`; Modify `src/App.jsx`, `src/app-components/hackathon-creation/FormSection.jsx`, `src/Pages/company/CreateHackathon.jsx`, `src/app-components/company-dashboard/ManageChallenges.jsx`.

- [ ] **Step 1: Referência no formulário:** estado `LinguagemReferencia`/`SolucaoReferencia` em `CreateHackathon` (carregados de `GET /referencia` na edição); `FormSection` ganha o bloco "Solução de referência (só a empresa vê)" com select de linguagem e `textarea` monoespaçada, **não** desabilitado em Hackathon publicado.
- [ ] **Step 2: `ManageChallenges`:** ação "Entregas" (navega para `/dashboard/Empresa/entregas/{id}`) nas fases Ativo, Em avaliação e Encerrado.
- [ ] **Step 3: `SubmissionsReview.jsx`** (rota `/dashboard/Empresa/entregas/:id`): `Header`/`Footer`, `Title` com o nome do Hackathon; à esquerda, lista de `GET /entregas` separando "Entregaram" e "Sem entrega"; à direita, dois painéis lado a lado (`grid lg:grid-cols-2`, empilhados no celular): "Solução de referência" (`GET /referencia`, ou "Nenhuma referência cadastrada") e a entrega selecionada (`GET /entregas/{submissionId}`), com select de versão (`GET …/versoes/{n}`), linguagem, quem enviou e quando.
- [ ] **Step 4: Rota** em `App.jsx`.
- [ ] **Step 5: Verificar** lint e build. Roteiro com o mock como iFood: editar Hackathon 1 mostra a referência e salva alteração; "Entregas" lista "Turma do Bit" (v2) e "Sem entrega" vazio; comparar lado a lado, trocar para v1; Hackathon 4 de outra empresa não aparece.
- [ ] **Step 6: Commit** `Feat: entregas e comparação para a empresa`.
