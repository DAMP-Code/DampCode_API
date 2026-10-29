# Design — Submissões (quinta entrega)

## 1. Objetivo

Quarta das seis etapas combinadas (empresas e funcionários → ciclo de vida do Hackathon → inscrição e equipes → **submissões** → avaliação e premiação → perfil e dashboards).

Esta etapa entrega:

- entrega de **código** por quem está inscrito, digitado/colado num editor ou enviado como arquivo de texto, como no LeetCode;
- **várias versões**, valendo sempre a **última aceita**;
- **solução de referência** opcional da empresa, que só ela vê;
- tela da empresa para **comparar manualmente**, lado a lado, a referência e o código entregue.

### Restrições

- Mesmas das entregas anteriores: não renomear nada existente, seguir o estilo atual da API e do front, mudanças aditivas.
- **Identidade visual mantida** nas telas novas: paleta roxa (`surface`, `surface-light`, `brand`, `highlight`), fontes Dangrek/Exo e os mesmos cartões, bordas e botões das telas atuais.
- Base: branches `feat/inscricao-e-equipes` (API e front), que já contêm as etapas 1–3.

### Fora do escopo

- Executar o código ou casos de teste automáticos (pode vir depois; no MVP a empresa compara manualmente).
- Nota, ranking e vencedores (etapa 5).
- Arquivos binários e zip, e Object Storage: arquivo só de texto, lido como texto.
- Quiz e links como formato de entrega.

## 2. Regras de negócio (decididas com o usuário)

1. **Formato do MVP:** código. A pessoa escolhe a linguagem e digita/cola num editor **ou** envia um arquivo de texto; o conteúdo do arquivo aparece no editor antes de enviar.
2. **A empresa compara manualmente**, vendo a solução de referência dela ao lado do código entregue.
3. **Entrega em página própria** (`/hackathon/:id/entrega`), aberta pelo botão "Entregar código" na página do Hackathon e em "Minhas inscrições".

### Decisões de Claude (aprovadas na conversa)

- **Quem entrega:** só quem tem inscrição ativa. No individual, a pessoa; em equipe, a entrega é **da equipe** e qualquer integrante envia versão nova; cada versão mostra quem enviou.
- **Quando:** só na fase **Ativo** (do início ao fim das submissões). Antes, a página diz quando abre; depois, mostra as versões só para leitura.
- **Linguagens:** JavaScript, Python, C#, Java, SQL, Outra.
- **Arquivo:** um arquivo de texto de até **100 KB** (`.js`, `.py`, `.cs`, `.java`, `.sql`, `.txt`); o mesmo limite vale para o código digitado.
- **Versão aceita:** passou nas regras (dentro do prazo, não vazia, dentro do limite, linguagem válida). Envio recusado não vira versão. Vale sempre a última versão aceita.
- **Solução de referência:** opcional, cadastrada no formulário do Hackathon (linguagem + código, mesmo limite de 100 KB), editável mesmo depois de publicado, **nunca** enviada a quem participa.
- **Empresa vê as entregas desde o início do Hackathon**; a nota fica para a etapa 5.
- **Armazenamento:** o código de cada versão fica no **MongoDB** (coleção `SubmissionVersions`), que já estava reservado para isso; o PostgreSQL guarda o resumo.

### Decisões de Claude (depois da revisão do documento)

- **O PostgreSQL é o dono da lista de versões** (tabela `submission_versions`); o MongoDB guarda só o código. Se o envio cair entre gravar no MongoDB e no PostgreSQL, sobra um documento órfão que ninguém lê, e a entrega nunca fica travada.
- **Dados de exemplo:** "Os Rápidos" continua no Hackathon 3 (para o roteiro de convites e pedidos da etapa 3). Para as entregas, o seed ganha a participante Carla Dias e a equipe "Turma do Bit" (Ana + Carla) no Hackathon 1 (iFood, fase Ativo), com duas versões (uma de cada) e solução de referência no Hackathon 1.

## 3. Estado atual relevante

- `Models/Submission.cs` existe fora do `DbContext`: `Id`, `ParticipationId`, `StageId` (para "etapas", que não existem), `Status` (string), `CurrentVersion`, `CurrentMongoVersionId`, `FirstSubmittedAt`, `LastSubmittedAt`, `FinalizedAt`, `CreatedAt`, `UpdatedAt`. O comentário diz que o conteúdo versionado vai para o MongoDB.
- `MongoDbService` (singleton) abre o banco pela connection string `DbConnection` (`mongodb://localhost/damp_code`).
- `Participation` e `HackathonTeam` (etapa 3): uma inscrição ativa por pessoa por Hackathon; equipe e membros congelados depois do início.
- `HackathonPhases.GetPhase`: `Active` = entre `StartsAt` e `EndsAt`.

## 4. Modelo de dados

### 4.1 PostgreSQL — `Submission` entra no `DbContext`

Uma `Submission` por **participante individual** ou por **equipe** em cada Hackathon. Mudanças aditivas na classe:

| Propriedade | Situação | Observação |
|---|---|---|
| `HackathonId` | **nova** | FK `fk_submissions_hackathons` (Cascade) |
| `TeamId` (`Guid?`) | **nova** | preenchida em Hackathon de equipe; FK `fk_submissions_hackathon_teams` (NoAction) |
| `ParticipationId` | passa a `Guid?` | preenchida no individual; FK `fk_submissions_participations` (NoAction) |
| `StageId` | passa a `Guid?` | reservado para etapas futuras; sempre nulo nesta etapa |
| `Status` | já existe | `"Submitted"` depois da primeira versão |
| `CurrentVersion` | já existe | número da versão que vale; **token de concorrência** |
| `CurrentMongoVersionId` | já existe | `_id` do documento da versão que vale |
| `FirstSubmittedAt`, `LastSubmittedAt` | já existem | |
| `FinalizedAt` | já existe | não usado nesta etapa |

Tabela `submissions`: `pk_submissions`; `ux_submissions_participation` único em `participation_id` (filtro `participation_id IS NOT NULL`); `ux_submissions_team` único em `team_id` (filtro `team_id IS NOT NULL`); `ix_submissions_hackathon_id`; `ck_submissions_owner` (`(participation_id IS NULL) <> (team_id IS NULL)`). Comentário da classe atualizado.

**`SubmissionVersion`** (nova) — tabela `submission_versions`: `Id`, `SubmissionId` (FK `fk_submission_versions_submissions`, Cascade), `Version` (int), `Language` (32), `FileName` (`string?`, 255), `MongoDocumentId` (24, o `_id` do código no MongoDB), `SubmittedByAccountId` (FK `fk_submission_versions_accounts`, Restrict), `SubmittedAt`. `ux_submission_versions_version` único em (`submission_id`, `version`). **O PostgreSQL é o dono da lista de versões**: uma versão existe quando a linha existe.

**`Hackathon`** ganha `ReferenceLanguage` (`string?`, 32) e `ReferenceCode` (`string?`, `text`).

Migration: `Submissions`.

### 4.2 MongoDB — coleção `SubmissionVersions`

```
{ _id: ObjectId, submissionId: string (Guid), code: string, createdAt: Date }
```

Só o código. Acesso por uma interface `ISubmissionVersionStore` com `Task<string> InsertAsync(Guid submissionId, string code, CancellationToken)` (devolve o `_id`), `Task<string?> GetCodeAsync(string documentId, CancellationToken)` e `Task DeleteAsync(string documentId, CancellationToken)`, implementada sobre o `MongoDbService` (`MongoSubmissionVersionStore`). Os testes usam uma implementação em memória. Documento sem linha correspondente em `submission_versions` é órfão: ninguém lê, e não atrapalha envios seguintes.

### 4.3 Enviar uma versão (ordem)

1. Validar (inscrição ativa, fase Ativo, linguagem, código não vazio, ≤ 100 KB).
2. Achar ou criar a `Submission` da pessoa (individual) ou da equipe.
3. Gravar o código no MongoDB (`InsertAsync`) e guardar o `_id`.
4. Num único `SaveChanges` no PostgreSQL: inserir a linha `SubmissionVersion` com `Version = CurrentVersion + 1` e atualizar a `Submission` (`CurrentVersion`, `CurrentMongoVersionId`, datas, `Status`).
5. Se o passo 4 falhar — concorrência no `CurrentVersion`, índice `ux_submission_versions_version` ou, na primeira versão, os índices únicos de `submissions` (dois integrantes enviando ao mesmo tempo) — tentar apagar o documento do passo 3 e responder 409. Se o apagar falhar, o documento fica órfão e o próximo envio funciona normalmente, porque o número da versão vem do PostgreSQL.

## 5. API

### 5.1 Serviço

`ISubmissionService` / `SubmissionService(DampCodeDbContext, ICompanyAccessService, ISubmissionVersionStore)`, com `SubmissionServiceResult<T>` e `SubmissionServiceError { None, NotFound, Forbidden, Conflict, Validation }`. Controllers finos, erros em `ProblemDetails { Detail }`.

### 5.2 Rotas (em `HackathonsController`, todas `[Authorize]`)

| Método e rota | Quem | O que faz |
|---|---|---|
| `GET /api/Hackathons/{id}/entrega` | inscrito | A entrega da pessoa/equipe: `aberta` (fase Ativo), `dataInicio`, `dataFinal`, `equipe` (nome ou nulo), `versoes` (`versao`, `linguagem`, `nomeArquivo`, `enviadoPor`, `enviadoEm`, `vale`), `atual` (`versao`, `linguagem`, `codigo`, `nomeArquivo`) ou nulo. |
| `GET /api/Hackathons/{id}/entrega/versoes/{versao}` | inscrito | Código de uma versão da própria entrega. |
| `POST /api/Hackathons/{id}/entrega` | inscrito | Envia versão `{ linguagem, codigo, nomeArquivo? }`. 201 com a versão criada. |
| `GET /api/Hackathons/{id}/entregas` | quem gerencia | Lista: uma linha por equipe (Hackathon de equipe) ou pessoa (individual) inscrita, com `submissionId` (ou nulo), `nome`, `versaoAtual`, `enviadoEm`. Quem não entregou aparece com `submissionId` nulo. |
| `GET /api/Hackathons/{id}/entregas/{submissionId}` | quem gerencia | Versões (metadados) e o código da versão que vale. |
| `GET /api/Hackathons/{id}/entregas/{submissionId}/versoes/{versao}` | quem gerencia | Código de uma versão. |
| `GET /api/Hackathons/{id}/referencia` | quem gerencia | `{ linguagem, codigo }` da solução de referência. |

`CreateHackathonDto` ganha `LinguagemReferencia` e `SolucaoReferencia`; o `PUT` aplica os dois **em qualquer fase** antes de encerrado/cancelado (como cores e logo). `HackathonResponseDto` **não** recebe esses campos.

### 5.3 Validações e mensagens

| Situação | Resposta |
|---|---|
| Hackathon inexistente, rascunho ou cancelado (participante) | 404 "Hackathon não encontrado." |
| Sem inscrição ativa | 403 "Você não está inscrito neste Hackathon." |
| Enviar fora da fase Ativo | 409 "As submissões deste Hackathon estão fechadas." |
| Linguagem fora da lista | 400 "Escolha uma linguagem." |
| Código vazio | 400 "Envie algum código." |
| Código ou referência > 100 KB (100 × 1024 caracteres) | 400 "O código deve ter no máximo 100 KB." |
| Envio simultâneo na mesma entrega | 409 "Outra versão foi enviada ao mesmo tempo. Tente novamente." |
| Versão inexistente | 404 "Versão não encontrada." |
| Empresa sem acesso ao Hackathon | 403 "Apenas empresas e seus funcionários podem gerenciar Hackathons." / 404 "Hackathon não encontrado." (mesmas regras da etapa 2) |

## 6. Front (`Damp-Code-Front`)

- **`Pages/SubmissionPage.jsx`** (rota `/hackathon/:id/entrega`, opção B do rascunho): cabeçalho com título e cores do Hackathon; seletor de linguagem; editor (`textarea` monoespaçado, sem biblioteca nova); "Escolher arquivo" lê o arquivo como texto (`FileReader.readAsText`) para o editor e preenche `nomeArquivo`; "Enviar versão"; lista de versões (clicar mostra o código daquela versão, só leitura). Fora da fase Ativo, editor e botão desabilitados com "As submissões abrem em dd/mm às hh:mm" ou "As submissões foram encerradas".
- **Acesso:** `JoinHackathon` mostra "Entregar código" para quem está inscrito (inclusive depois do início); `MyParticipations` ganha o mesmo link em cada inscrição.
- **Empresa:**
  - `FormSection` ganha "Solução de referência (só a empresa vê)": linguagem + código, sempre editável enquanto não encerrado/cancelado; o formulário carrega o valor de `GET /referencia`.
  - `ManageChallenges` ganha a ação "Entregas" nas fases Ativo, Em avaliação e Encerrado.
  - **`Pages/company/SubmissionsReview.jsx`** (rota `/dashboard/Empresa/entregas/:id`): lista à esquerda (entregou / sem entrega); à direita, referência e código lado a lado, com seletor de versão.
- **API simulada:** mesmas rotas e regras; seed conforme as decisões depois da revisão (equipe "Turma do Bit", Ana + Carla, no Hackathon 1).

## 7. Testes

- `SubmissionServiceTests` (EF InMemory + store em memória): primeira versão cria a submissão (individual e equipe); segunda versão vira a que vale; integrante diferente da mesma equipe envia na mesma submissão; fora da fase Ativo → 409; não inscrito → 403; linguagem inválida, vazio e > 100 KB → 400; concorrência (`CurrentVersion` mudado por outro contexto) → 409 e documento do Mongo apagado; documento órfão no store não impede o envio seguinte; ler versão de outra entrega → 404; empresa lista entregas com quem não entregou; empresa de outro Hackathon → 404; referência nunca aparece em `HackathonResponseDto`.
- `HackathonServiceTests`: referência salva no rascunho e depois de publicado; > 100 KB → 400.
- `SubmissionsEndpointTests`: enviar sem token → 401; participante envia e lê a entrega; empresa vê a lista e a referência; participante chamando `/entregas` → 403.
- Front: lint sem problemas novos, build, roteiro no navegador com o mock.

## 8. Deixado para o builder

- Layout exato das duas páginas novas, seguindo o rascunho aprovado e os componentes atuais.
- Divisão em commits.
