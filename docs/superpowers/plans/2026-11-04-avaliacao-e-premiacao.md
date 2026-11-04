# Avaliação e premiação — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A empresa define prêmios por posição ao criar o Hackathon, dá nota às entregas com uma sugestão automática por semelhança com a referência, escolhe livremente os vencedores e publica o resultado, que o participante vê no próprio painel.

**Architecture:** `HackathonPrize` (tabela nova) guarda os prêmios; `Submission` ganha nota, comentário, prêmio e classificação final; `Hackathon` ganha `ResultsPublishedAt`. `SimilarityScore` (classe estática pura) calcula a sugestão. `EvaluationService` concentra ranking, nota, vencedores e publicação; `HackathonService` cuida da lista de prêmios; `ParticipationService` mostra o resultado. No front: lista de prêmios no formulário, painel de nota na tela de comparação, página nova de ranking e o resultado em "Minhas inscrições", tudo também na API simulada.

**Tech Stack:** ASP.NET Core 8, EF Core 8.0.11 + Npgsql, MongoDB.Driver 2.25, xUnit + EF InMemory + `Microsoft.AspNetCore.Mvc.Testing`; React 19 + Vite + Tailwind.

**Spec:** `docs/superpowers/specs/2026-11-04-avaliacao-e-premiacao-design.md`

## Global Constraints

- Branch `feat/avaliacao-e-premiacao` nos dois repositórios (já criadas a partir de `master`/`main`; o spec já está commitado na da API).
- Não renomear nada existente (classes, rotas, DTOs, campos de JSON). Mudanças aditivas; records de saída ganham parâmetros novos **no fim**.
- Estilo igual às etapas anteriores (file-scoped namespace, primary constructors, `sealed`, records de saída, `CancellationToken cancellationToken`, mensagens em português, `ProblemDetails { Detail = Message }`); EF com `snake_case` e `pk_`/`fk_`/`ux_`/`ix_`.
- Prêmios: 1 a 10, posições 1..N consecutivas, tipo `Dinheiro`/`Entrevista`, valor > 0 e ≤ 9.999.999.999,99 só para dinheiro, descrição ≤ 120. Aplicados **só na fase Rascunho** (mesmo ramo de `ApplyContent`).
- Nota inteira 0–100; comentário ≤ 1000.
- Avaliar/escolher vencedores/publicar só quando `GetPhase` é `InReview` ou `Closed` **e** `ResultsPublishedAt == null` **e** `Status != Cancelled`.
- Mensagens exatamente como na seção 6.4 do spec.
- Identidade visual: classes Tailwind atuais (`bg-surface`, `bg-surface-light`, `border-brand`, `bg-highlight`, `text-content`, `text-content-secondary`), `panelClass` de `SubmissionsReview.jsx`, `Title` para títulos.
- `dotnet ef` com `DOTNET_ROLL_FORWARD=Major`, `ConnectionStrings__PostgreSql="Host=localhost;Database=nao_usado"`, `Jwt__Secret` de 32+ caracteres; testes com `dotnet test DampCode_API.Tests`.
- Front: `fetch` em `https://localhost:7092`, `Authorization: Bearer ${user.token}`, ids em URL com `encodeURIComponent`, efeitos com função async dentro do `useEffect`; `npm run lint` sem problemas novos (anotar a contagem base antes da Task 7).
- Commits `Feat:`/`Fix:`/`Test:`/`Docs:` em português, **sem linha de coautoria**.

## Review Focus

- Empresa troca dois vencedores de posição (A tinha o 1º, B o 2º; agora o inverso) → salva sem erro 500 no PostgreSQL (índice único não é adiável). (Task 4: `SetWinners_SwapPositions_Succeeds`; implementação em duas gravações na mesma transação)
- Hackathon encerrado pelas datas (passou de `ReviewEndsAt`) sem resultado → ainda dá para avaliar e publicar, e publicar **não** muda `Status`. (Task 4: `Publish_AfterReviewEndsAt_KeepsStatusAndPublishes`)
- Empate de nota → quem enviou a versão que vale primeiro fica na frente; entregas sem nota ficam no fim. (Task 4: `Ranking_OrdersByScoreThenSubmittedAt`)
- Integrante que saiu da equipe antes do início não vê resultado da equipe (só inscrições ativas aparecem); os demais integrantes veem o mesmo resultado. (Task 5: `Overview_TeamResult_SameForActiveMembers`)
- Cliente antigo que só manda `TipoPremiacao`/`Premiacao` → rascunho continua publicável e tem prêmio de 1º lugar. (Task 3: `Create_WithoutPremios_CreatesFirstPlaceFromLegacy`)

---

## File Structure

**API (`DampCode_API/`)**

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `Services/SimilarityScore.cs` | Create | semelhança 0–100 (seção 4 do spec) |
| `Models/HackathonPrize.cs` | Create | prêmio por posição |
| `Models/Submission.cs`, `Models/Hackathon.cs` | Modify | campos de avaliação; `Prizes`, `ResultsPublishedAt` |
| `Data/Configurations/HackathonConfiguration.cs`, `SubmissionConfiguration.cs` | Modify | `hackathon_prizes`, colunas novas, índices |
| `Data/DampCodeDbContext.cs` | Modify | `HackathonPrizes` |
| `Data/Migrations/*_Evaluation.cs` | Create (gerado + SQL) | migration com o insert dos prêmios antigos |
| `Dto/PrizeDto.cs` | Create | entrada e saída de prêmio |
| `Dto/EvaluationDtos.cs` | Create | entradas/saídas da avaliação e do resultado |
| `Dto/CreateHackathonDto.cs`, `HackathonResponseDto.cs`, `SubmissionDtos.cs`, `ParticipationDtos.cs` | Modify | campos novos no fim |
| `Services/HackathonService.cs` | Modify | lista de prêmios, sincronia, publicação |
| `Services/IEvaluationService.cs`, `EvaluationService.cs` | Create | ranking, nota, vencedores, publicação |
| `Services/SubmissionService.cs` | Modify | `GetEntryAsync` com semelhança/nota |
| `Services/ParticipationService.cs` | Modify | `Resultado` em `MyParticipationDto` |
| `Controllers/HackathonsController.cs`, `Program.cs` | Modify | rotas 6.3, registro |

**Testes:** `SimilarityScoreTests.cs`, `EvaluationServiceTests.cs`, `EvaluationEndpointTests.cs` (novos); casos novos em `HackathonServiceTests.cs`, `SubmissionServiceTests.cs`, `ParticipationServiceTests.cs`.

**Front (`Damp-Code-Front/src/`)**

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `mock/seed.js`, `mock/installMockApi.js` | Modify | prêmios, avaliação, resultado |
| `app-components/hackathon-creation/FormSection.jsx`, `PreviewSection.jsx`, `Pages/company/CreateHackathon.jsx` | Modify | "Prêmios por posição" |
| `app-components/HackathonCard.jsx`, `Pages/HackathonPage.jsx` | Modify | 1º prêmio + "+N prêmios" / lista |
| `Pages/company/SubmissionsReview.jsx` | Modify | painel de nota |
| `Pages/company/EvaluationRanking.jsx` | Create | ranking, vencedores, publicar (`/dashboard/Empresa/avaliacao/:id`) |
| `App.jsx`, `app-components/company-dashboard/ManageChallenges.jsx` | Modify | rota e ação "Avaliação" |
| `app-components/participant-dashboard/MyParticipations.jsx` | Modify | destaque do resultado |

---

### Task 1: Semelhança com a referência

**Files:** Create `Services/SimilarityScore.cs`; Test `DampCode_API.Tests/SimilarityScoreTests.cs`.

**Interfaces:**
- Produces: `public static class SimilarityScore { public static int Compute(string reference, string code, string language); }` (namespace `DampCode_API.Services`). `language` é um de `SubmissionRules.Languages`; valor desconhecido = "Outra".

- [ ] **Step 1: Testes**
  - `Identical_Returns100`: `Compute("let soma = a + b;", "let soma = a + b;", "JavaScript") == 100`.
  - `RenamedAndReformatted_Returns100`: referência `"function soma(a, b) {\n  return a + b; // soma\n}"`, código `"function total(x,y){return x+y;}"` em JavaScript → 100.
  - `PythonHashComment_Ignored`: `"def f(x):\n    return x * 2"` vs `"# dobra\ndef g(y):\n    return y * 2  # fim"` → 100.
  - `SqlCaseInsensitive`: `"SELECT nome FROM clientes"` vs `"select nome from clientes"` em SQL → 100.
  - `Unrelated_IsLow`: um `for` em C# somando um array vs uma classe Java com `System.out.println` → `< 40`.
  - `EmptyCode_Returns0`: `Compute("x = 1", "", "Python") == 0` e `Compute("", "", "Python") == 0`.
  - `ShortCode_UsesUnigrams`: `Compute("x", "y", "Python") == 100` (os dois viram `ID`).
- [ ] **Step 2: Rodar** `dotnet test DampCode_API.Tests --filter SimilarityScore` → falha de compilação.
- [ ] **Step 3: Implementar** conforme spec seção 4. Comentários: `//` e `/* */` (JavaScript, C#, Java), `#` (Python), `--` e `/* */` (SQL), nenhum (Outra); remoção sem tocar em strings entre aspas simples/duplas. Tokens por regex `[A-Za-z_][A-Za-z0-9_]*|\d+(\.\d+)?|\S`. Palavras reservadas: `HashSet<string>` por linguagem (só as comuns: controle de fluxo, declarações, tipos primitivos, `return`, `class`, `new`, `true/false/null` e equivalentes); SQL e Outra com `StringComparer.OrdinalIgnoreCase` e tokens em maiúsculas. Identificador não reservado → `ID`, número → `NUM`. Dice sobre multiconjuntos de trigramas (unigramas se algum lado tiver < 3 tokens): `Math.Round(200.0 * comuns / (|A| + |B|))`.
- [ ] **Step 4: Rodar** → passa.
- [ ] **Step 5: Commit** `Feat: semelhança do código com a solução de referência`.

### Task 2: Entidades, configuração e migration

**Files:** Create `Models/HackathonPrize.cs`, migration `Evaluation`; Modify `Models/Submission.cs`, `Models/Hackathon.cs`, `Data/Configurations/HackathonConfiguration.cs`, `Data/Configurations/SubmissionConfiguration.cs`, `Data/DampCodeDbContext.cs`; Test `EvaluationServiceTests.cs`.

**Interfaces:**
- Produces:
  - `HackathonPrize { Guid Id; Guid HackathonId; int Position; HackathonRewardType RewardType; decimal? Amount; string? Description; DateTime CreatedAt; Hackathon Hackathon }`.
  - `Hackathon`: + `DateTime? ResultsPublishedAt`, `ICollection<HackathonPrize> Prizes`.
  - `Submission`: + `int? Score`, `string? Feedback`, `DateTime? EvaluatedAt`, `Guid? EvaluatedByAccountId`, `int? PrizePosition`, `int? FinalPosition`.
  - `DbSet<HackathonPrize> HackathonPrizes`.

- [ ] **Step 1: Teste** `Prizes_PersistWithHackathon` (em `EvaluationServiceTests`, seguindo o construtor de `SubmissionServiceTests`): Hackathon de `ParticipationSeed` + dois `HackathonPrize` (1º dinheiro 1000, 2º entrevista com descrição); recarrega com `Include(Prizes)` → 2 itens ordenados por `Position`, `Amount` do 2º nulo.
- [ ] **Step 2: Rodar** → falha de compilação.
- [ ] **Step 3: Implementar** (spec seção 5): `hackathon_prizes` (`pk_hackathon_prizes`, `reward_type` como string 32 igual ao de `hackathons`, `amount` precisão (12,2), `description` 120, `ux_hackathon_prizes_position` em (`hackathon_id`, `position`), FK `fk_hackathon_prizes_hackathons` Cascade `WithMany(h => h.Prizes)`); `hackathons.results_published_at`; `submissions`: `score`, `feedback` 1000, `evaluated_at`, `evaluated_by_account_id` (FK `fk_submissions_evaluated_by_accounts` Restrict, sem inversa), `prize_position`, `final_position`, `ux_submissions_prize_position` em (`hackathon_id`, `prize_position`) com filtro `prize_position IS NOT NULL`.
- [ ] **Step 4: Rodar** suíte → passa.
- [ ] **Step 5: Migration** `Evaluation`; acrescentar no `Up`, depois do `CreateTable`, o `migrationBuilder.Sql` que insere o 1º lugar dos Hackathons existentes:
  ```sql
  INSERT INTO hackathon_prizes (id, hackathon_id, position, reward_type, amount, description, created_at)
  SELECT gen_random_uuid(), id, 1, reward_type, CASE WHEN reward_type = 'Money' THEN prize_amount END, NULL, now()
  FROM hackathons
  WHERE reward_type = 'Interview' OR (reward_type = 'Money' AND prize_amount > 0);
  ```
  Conferir que a migration só cria/altera o listado acima e **não** mexe em `winners_count`.
- [ ] **Step 6: Commit** `Feat: entidades de avaliação e prêmios com migration`.

### Task 3: Prêmios no Hackathon

**Files:** Create `Dto/PrizeDto.cs`; Modify `Dto/CreateHackathonDto.cs`, `Dto/HackathonResponseDto.cs`, `Services/HackathonService.cs`; Test `HackathonServiceTests.cs`.

**Interfaces:**
- Consumes: `HackathonPrize`, `Hackathon.Prizes`, `Hackathon.ResultsPublishedAt` (Task 2).
- Produces:
  - `public sealed class PrizeDto { int Posicao; string? Tipo; decimal? Valor; string? Descricao }` (classe com setters, como `CreateHackathonDto`, para entrada e saída).
  - `CreateHackathonDto` + `List<PrizeDto>? Premios`.
  - `HackathonResponseDto` + no fim: `IReadOnlyList<PrizeDto> Premios`, `bool ResultadoPublicado`.
  - `HackathonService.WithDetails` passa a incluir `Prizes`.

- [ ] **Step 1: Testes**
  - `Create_WithPremios_SyncsLegacyFields`: 3 prêmios (1º Dinheiro 1000, 2º Dinheiro 500, 3º Entrevista) → `WinnersCount == 3`, `RewardType == Money`, `PrizeAmount == 1000`; resposta com `Ranking == "Top 3"`, `Premiacao == 1000`, `Premios.Count == 3` em ordem.
  - `Create_PremiosInvalid_ReturnsValidation` (Theory): posições `[1, 3]` → "As posições dos prêmios devem ser 1, 2, 3... sem pular."; 11 prêmios → "Defina no máximo 10 prêmios."; 2º Dinheiro sem valor → "Informe o valor do prêmio do 2º lugar."; tipo "Viagem" → "Escolha o tipo do prêmio do 1º lugar."; descrição com 121 caracteres → "A descrição do prêmio do 1º lugar deve ter no máximo 120 caracteres.".
  - `Create_WithoutPremios_CreatesFirstPlaceFromLegacy`: só `TipoPremiacao = "Entrevista"`, `Ranking = "Top 10"` → um prêmio (1º, Interview), `WinnersCount == 10`; e o rascunho completo publica.
  - `Update_AfterPublish_IgnoresPremios`: Hackathon publicado com 1 prêmio; `PUT` com 3 prêmios → continua 1.
  - `Publish_WithoutPrizes_ListsMissing`: rascunho sem tipo e sem `Premios` → mensagem contém "premiação".
  - `Response_InterviewPrize_HasNullValue`: prêmio de entrevista com `Valor = 50` na entrada → saída `Valor == null`.
- [ ] **Step 2: Rodar** → falha.
- [ ] **Step 3: Implementar** em `HackathonService`:
  - `ValidatePrizes(List<PrizeDto>?) → string?` chamada em `ValidateDraft` (mensagens acima; aceita `Tipo` pelo mesmo `ParseRewardType`).
  - `ApplyPrizes(Hackathon hackathon, CreateHackathonDto dto)` chamada logo após `ApplyContent` em `CreateAsync` e no ramo Rascunho de `UpdateAsync`: com `Premios` preenchido, substitui `hackathon.Prizes` (remover as linhas antigas via `dbContext.HackathonPrizes.Remove` como em `SetTechnologiesAsync`) e sincroniza `WinnersCount = Count`, `RewardType`/`PrizeAmount` = 1º; com `Premios` nulo, recria só o 1º a partir de `RewardType`/`PrizeAmount` já aplicados (nenhum se `RewardType` nulo ou dinheiro sem valor > 0). `Premios` vazio (`[]`) = sem prêmios e campos antigos zerados.
  - `ParseWinnersCount` continua igual para o caminho antigo.
  - `MissingCrucialFields`: troca as três checagens de tipo/valor/quantos ganham por `if (hackathon.Prizes.Count == 0) missing.Add("premiação");`.
  - `ToDto`: `Premios` ordenados por `Position` (`Tipo` "Dinheiro"/"Entrevista", `Valor` nulo para entrevista), `ResultadoPublicado = hackathon.ResultsPublishedAt is not null`.
- [ ] **Step 4: Rodar** suíte → passa (ajustar testes antigos de publicação que esperavam "tipo de premiação"/"quantos ganham", se houver, para "premiação").
- [ ] **Step 5: Commit** `Feat: prêmios por posição no Hackathon`.

### Task 4: Serviço de avaliação

**Files:** Create `Services/IEvaluationService.cs`, `Services/EvaluationService.cs`, `Dto/EvaluationDtos.cs`; Test `EvaluationServiceTests.cs`.

**Interfaces:**
- Consumes: `SimilarityScore.Compute` (Task 1), entidades (Task 2), `PrizeDto` (Task 3), `ISubmissionVersionStore.GetCodeAsync`, `ICompanyAccessService.GetManagedCompanyAsync`, `HackathonPhases`.
- Produces:
  - `EvaluateDto { int? Nota; string? Comentario }`, `WinnerDto { int Posicao; Guid SubmissionId }`, `SetWinnersDto { List<WinnerDto>? Vencedores }`.
  - `EvaluationEntryDto(Guid SubmissionId, string Nome, string? Linguagem, int? Semelhanca, int? Nota, string? Comentario, int? PosicaoPremio, DateTime? EnviadoEm)`.
  - `EvaluationRankingDto(bool PodeAvaliar, bool ResultadoPublicado, IReadOnlyList<PrizeDto> Premios, IReadOnlyList<EvaluationEntryDto> Entregas)`.
  - `EvaluationServiceResult<T>(bool Succeeded, T? Value = default, EvaluationServiceError Error = None, string? Message = null)`, `enum EvaluationServiceError { None, NotFound, Forbidden, Conflict, Validation }`.
  - `EvaluationService(DampCodeDbContext dbContext, ICompanyAccessService companyAccessService, ISubmissionVersionStore versionStore)`:
    - `Task<EvaluationServiceResult<EvaluationRankingDto>> GetRankingAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken)`
    - `Task<EvaluationServiceResult<EvaluationEntryDto>> EvaluateAsync(Guid accountId, Guid hackathonId, Guid submissionId, EvaluateDto dto, CancellationToken cancellationToken)`
    - `Task<EvaluationServiceResult<EvaluationRankingDto>> SetWinnersAsync(Guid accountId, Guid hackathonId, SetWinnersDto dto, CancellationToken cancellationToken)`
    - `Task<EvaluationServiceResult<bool>> PublishResultsAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken)` (o controller devolve o `HackathonResponseDto` chamando `IHackathonService.GetByIdAsync` depois).
    - `public static bool CanEvaluate(Hackathon hackathon, DateTime now)` (regra do Global Constraints) e `public static async Task<int?> SuggestAsync(Hackathon hackathon, Submission submission, string? language, ISubmissionVersionStore store, CancellationToken cancellationToken)` (nulo sem referência ou sem código), usados também pela Task 5.

- [ ] **Step 1: Testes** (helper: Hackathon individual com `StartsAt` há 4 dias → fase Em avaliação; três participantes com `Submission` + `SubmissionVersion` + código no store; referência cadastrada)
  - `Evaluate_BeforeSubmissionsEnd_ReturnsConflict` → 409 "A avaliação abre quando as submissões terminam.".
  - `Evaluate_OutOfRange_ReturnsValidation` (Theory −1, 101, nulo) → "A nota deve ficar entre 0 e 100."; comentário com 1001 caracteres → "O comentário deve ter no máximo 1000 caracteres.".
  - `Evaluate_SavesScoreAndEvaluator`: grava `Score`, `Feedback` aparado, `EvaluatedAt`, `EvaluatedByAccountId`.
  - `Ranking_OrdersByScoreThenSubmittedAt`: notas 80, 80 (enviado depois), sem nota → ordem correta, sem nota no fim.
  - `Ranking_SuggestionNullWithoutReference` e `Ranking_SuggestionFromReference` (código igual à referência → 100).
  - `Ranking_OtherCompany_ReturnsNotFound`.
  - `SetWinners_OutOfScoreOrder_Saves`: prêmio 1 para o 3º colocado.
  - `SetWinners_Invalid_ReturnsValidation` (posição 5 inexistente, posição repetida, entrega repetida) → "Escolha um vencedor diferente para cada prêmio.".
  - `SetWinners_SwapPositions_Succeeds`.
  - `Publish_WithUngraded_ReturnsConflict` → "Dê nota a todas as entregas antes de publicar.".
  - `Publish_SetsFinalPositions`: 3 prêmios, 5 entregas; prêmio 1 para quem tem a 3ª maior nota, prêmio 2 vazio, prêmio 3 para a maior nota → `FinalPosition`: premiado do 1 = 1, premiado do 3 = 3, demais em ordem de nota = 4, 5, 6; `Status == "Evaluated"`; Hackathon `Status == Closed`, `ClosedAt` e `ResultsPublishedAt` preenchidos.
  - `Publish_AfterReviewEndsAt_KeepsStatusAndPublishes`: Hackathon com `ReviewEndsAt` no passado e `Status == Scheduled` → publica, `Status` continua `Scheduled`.
  - `AfterPublish_EverythingLocked`: avaliar, vencedores e publicar de novo → 409 "O resultado já foi publicado.".
  - `Cancelled_ReturnsConflict` → 409 "Este Hackathon já foi encerrado ou cancelado.".
- [ ] **Step 2: Rodar** → falha de compilação.
- [ ] **Step 3: Implementar**:
  - Acesso igual a `SubmissionService.FindManagedHackathonAsync` (mesmas mensagens), carregando `Prizes`.
  - Ranking: submissões do Hackathon com `CurrentVersion > 0`; nome como em `SubmissionService.GetEntryAsync`; linguagem da versão que vale (`SubmissionVersions`); ordem `Score` desc (nulos no fim) e `LastSubmittedAt` asc.
  - Ordem de checagem nas escritas: Cancelado → `ResultsPublishedAt` → `CanEvaluate`.
  - `SetWinnersAsync`: validar contra `Prizes` e as submissões do Hackathon; numa `BeginTransactionAsync` (ignorada no InMemory), primeiro zerar `PrizePosition` de todas e `SaveChanges`, depois atribuir e `SaveChanges`; `DbUpdateException` → 409 "Os vencedores mudaram. Recarregue a página.".
  - `PublishResultsAsync`: `FinalPosition` = `PrizePosition` para os premiados; os demais, em ordem do ranking, recebem `Prizes.Count + 1`, `+ 2`... Uma posição de prêmio sem vencedor fica vazia (ninguém não escolhido ocupa uma posição premiada), como no teste `Publish_SetsFinalPositions`.
  - Se `GetPhase == InReview`: `Status = Closed`, `ClosedAt = now`.
- [ ] **Step 4: Rodar** suíte → passa.
- [ ] **Step 5: Commit** `Feat: avaliação, vencedores e publicação do resultado`.

### Task 5: Nota na entrega e resultado do participante

**Files:** Modify `Dto/SubmissionDtos.cs`, `Services/SubmissionService.cs`, `Dto/ParticipationDtos.cs`, `Dto/EvaluationDtos.cs`, `Services/ParticipationService.cs`; Test `SubmissionServiceTests.cs`, `ParticipationServiceTests.cs`.

**Interfaces:**
- Consumes: `EvaluationService.CanEvaluate`, `EvaluationService.SuggestAsync` (Task 4).
- Produces:
  - `SubmissionDetailDto` + no fim: `int? Semelhanca`, `int? Nota`, `string? Comentario`, `bool PodeAvaliar`.
  - `ParticipationResultDto(int? Posicao, int TotalClassificados, int? Nota, string? Comentario, PrizeDto? Premio)` em `EvaluationDtos.cs`.
  - `MyParticipationDto` + no fim: `ParticipationResultDto? Resultado`.

- [ ] **Step 1: Testes**
  - `GetEntry_IncludesSuggestionAndScore` (SubmissionServiceTests): referência = código → `Semelhanca == 100`; nota salva aparece; `PodeAvaliar` falso na fase Ativo.
  - `Overview_NoResultBeforePublish` (ParticipationServiceTests): `Resultado == null`.
  - `Overview_ResultAfterPublish`: `Posicao`, `TotalClassificados` (submissões com `FinalPosition`), `Nota`, `Comentario`, `Premio` (com tipo/valor/descrição) para a premiada; `Premio == null` para a não premiada.
  - `Overview_NoSubmission_ResultWithoutPosition`: inscrita sem entrega → `Resultado` com `Posicao == null`, `Nota == null`.
  - `Overview_TeamResult_SameForActiveMembers`: dois integrantes ativos veem o mesmo resultado; quem saiu antes do início não tem a inscrição na lista.
- [ ] **Step 2: Rodar** → falha.
- [ ] **Step 3: Implementar**: `GetEntryAsync` carrega o Hackathon com referência e chama `SuggestAsync` com a linguagem da versão que vale; `ParticipationService.GetMineAsync` incluir `Prizes` e, quando `ResultsPublishedAt` não é nulo, buscar a `Submission` por `ParticipationId` (individual) ou `TeamId` (equipe) e montar o resultado; `ToDto` ganha o parâmetro `ParticipationResultDto? result` (outros chamadores passam `null`).
- [ ] **Step 4: Rodar** suíte → passa.
- [ ] **Step 5: Commit** `Feat: nota na entrega e resultado no painel do participante`.

### Task 6: Rotas

**Files:** Modify `Controllers/HackathonsController.cs`, `Program.cs`; Create `DampCode_API.Tests/EvaluationEndpointTests.cs`.

**Interfaces:**
- Consumes: `IEvaluationService` (Task 4), `IHackathonService.GetByIdAsync`.
- Produces: rotas da seção 6.3 do spec: `GET {id:guid}/avaliacao`, `PUT {id:guid}/entregas/{submissionId:guid}/avaliacao`, `PUT {id:guid}/vencedores`, `POST {id:guid}/resultado` (este devolve o `HackathonResponseDto`). Mapeamento de erro igual às rotas de submissão (`NotFound`→404, `Forbidden`→403, `Conflict`→409, `Validation`→400). Registro `builder.Services.AddScoped<IEvaluationService, EvaluationService>();`.

- [ ] **Step 1: Testes**
  - `Ranking_WithoutToken_Returns401`.
  - `Ranking_AsParticipant_Returns403`.
  - `FullFlow_GradeChooseWinnerPublish`: empresa avalia todas → define vencedor → publica (200, `resultadoPublicado == true`, `fase == "Encerrado"`) → participante em `GET /api/Participations/me` vê `resultado.posicao == 1` e `resultado.premio.posicao == 1`.
  - `HackathonJson_HasPremios`: `GET /api/Hackathons/{id}` tem `premios` e `resultadoPublicado`.
- [ ] **Step 2: Rodar** → falha.
- [ ] **Step 3: Implementar** rotas e registro.
- [ ] **Step 4: Rodar** suíte completa → passa.
- [ ] **Step 5: Commit** `Feat: rotas de avaliação e resultado`.

### Task 7: API simulada

**Files:** Modify `mock/installMockApi.js`, `mock/seed.js`.

**Interfaces:**
- Produces: as mesmas rotas e JSON das Tasks 3–6: `premios` e `resultadoPublicado` em `toHackathonResponse`; `premios` aceito em criar/editar (só em rascunho) com a mesma sincronia e as mesmas mensagens; `GET/PUT` de avaliação, `PUT /vencedores`, `POST /resultado`; `semelhanca`, `nota`, `comentario`, `podeAvaliar` no detalhe da entrega; `resultado` em cada item de `inscricoes` de `/api/Participations/me`. Semelhança no mock: versão simplificada em JS (mesma tokenização e Dice; palavras reservadas só as mais comuns).

- [ ] **Step 1: Anotar** a contagem base de `npm run lint`.
- [ ] **Step 2: Implementar** rotas, no mesmo estilo de `handleSubmissions` (função `handleEvaluation(path, method, body, database, user)` chamada antes de `handleHackathons`).
- [ ] **Step 3: Seed:** Hackathon 1 (iFood) com três prêmios (1º R$ 1.000, 2º R$ 500, 3º Entrevista "Entrevista para vaga júnior"); um Hackathon em "Em avaliação" com 4 entregas (2 com nota) e referência; um Hackathon encerrado com resultado publicado em que Ana ficou em 2º com prêmio; demais Hackathons com o 1º prêmio derivado dos campos antigos.
- [ ] **Step 4: Verificar** `npm run lint` (sem problemas novos) e `npm run build`.
- [ ] **Step 5: Commit** `Feat: avaliação e premiação na API simulada`.

### Task 8: Prêmios no formulário e nas páginas

**Files:** Modify `app-components/hackathon-creation/FormSection.jsx`, `PreviewSection.jsx`, `Pages/company/CreateHackathon.jsx`, `app-components/HackathonCard.jsx`, `Pages/HackathonPage.jsx`.

- [ ] **Step 1: Implementar** no formulário a seção "Prêmios por posição" (spec 7): estado `Premios: [{ Posicao: 1, Tipo: "", Valor: "", Descricao: "" }]`, "Adicionar posição" até 10, "Remover" só na última (mínimo 1), valor só quando `Tipo === "Dinheiro"`; envia `Premios` com `Valor` numérico ou `null`; edição carrega `data.premios`; desativada com `isPublished` como os demais campos de regra. Remove da tela o seletor "Top N" e os campos únicos de tipo/valor (o estado antigo `Ranking/TipoPremiacao/Premiacao` deixa de ser enviado). `PreviewSection` mostra os prêmios no lugar de `ranking`.
- [ ] **Step 2: Implementar** `HackathonCard`: prêmio do 1º (`formatPrize` para dinheiro, "Entrevista" para entrevista) + "+N prêmios" quando `premios.length > 1`, caindo no `premiacao` atual se `premios` vier vazio. `HackathonPage`: lista completa "1º lugar — R$ 1.000,00 · descrição".
- [ ] **Step 3: Verificar** lint, build e no navegador com o mock: criar rascunho com 3 prêmios, editar, publicar; card e página mostram certo.
- [ ] **Step 4: Commit** `Feat: prêmios por posição no formulário do Hackathon`.

### Task 9: Nota na tela de comparação

**Files:** Modify `Pages/company/SubmissionsReview.jsx`.

- [ ] **Step 1: Implementar** abaixo do código, quando `detail.podeAvaliar`: "Semelhança com a referência: N%" (ou "Sem referência"), aviso "Linguagem diferente da referência" quando a linguagem da versão que vale ≠ `reference.linguagem`, campo **Nota (0–100)** (pré-preenchido com `semelhanca` se `nota` nulo), **Comentário**, "Salvar nota" (`PUT .../entregas/{id}/avaliacao`, mostra o erro da API em `ProblemDetails.detail`). Sem `podeAvaliar` e com nota existente: só leitura. Lista à esquerda: nota ou "Sem nota" em cada entrega (vem do detalhe já salvo ou de `GET /avaliacao` quando a fase permitir). Link "Ver ranking" para `/dashboard/Empresa/avaliacao/:id`.
- [ ] **Step 2: Verificar** lint, build e no navegador (mock): dar nota, recarregar, nota persiste; erro de nota 101 aparece.
- [ ] **Step 3: Commit** `Feat: nota da entrega na tela de comparação`.

### Task 10: Página de ranking e vencedores

**Files:** Create `Pages/company/EvaluationRanking.jsx`; Modify `App.jsx`, `app-components/company-dashboard/ManageChallenges.jsx`.

- [ ] **Step 1: Implementar** a página (spec 7): `GET /avaliacao`; tabela com posição no ranking, nome, semelhança, nota ("Sem nota"), "Ver código" (`/dashboard/Empresa/entregas/:id?entrega=<submissionId>` — `SubmissionsReview` passa a selecionar a entrega pelo query string se vier); seletor "Prêmio" por linha com as posições livres + a da própria linha + "Nenhum"; "Salvar vencedores" (`PUT /vencedores`); "Publicar resultado" com `window.confirm` listando posições sem vencedor ("A posição 3 ficou sem vencedor. Publicar assim mesmo?") e o aviso de que encerra o Hackathon e não dá para mudar depois; depois de publicado, tudo só leitura e ordenado pela classificação final. Rota `/dashboard/Empresa/avaliacao/:id` em `App.jsx`; ação "Avaliação" em `ManageChallenges` nas fases "Em avaliação" e "Encerrado".
- [ ] **Step 2: Verificar** lint, build e no navegador (mock): escolher vencedor fora da ordem, publicar, ver só leitura.
- [ ] **Step 3: Commit** `Feat: ranking, vencedores e publicação do resultado`.

### Task 11: Resultado em "Minhas inscrições"

**Files:** Modify `app-components/participant-dashboard/MyParticipations.jsx`.

- [ ] **Step 1: Implementar** o destaque (spec 7) quando `participation.resultado` existe: "Você ficou em 2º de 14 · Nota 87" (ou "Sem entrega" quando `posicao` nulo), prêmio "🏆 2º lugar — R$ 500,00" / "🏆 1º lugar — Entrevista · descrição", e o comentário da empresa; selo "Resultado publicado". Para equipe, "Sua equipe ficou em...".
- [ ] **Step 2: Verificar** lint, build e roteiro completo no navegador com o mock: empresa cria Hackathon com prêmios → (seed) avalia, escolhe vencedor fora da ordem e publica → Ana vê o resultado.
- [ ] **Step 3: Commit** `Feat: resultado no painel do participante`.
