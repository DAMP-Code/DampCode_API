# Design — Ciclo de vida do Hackathon (terceira entrega)

## 1. Objetivo

Segunda das seis etapas combinadas (empresas e funcionários → **ciclo de vida do Hackathon** → inscrição e equipes → submissões → avaliação e premiação → perfil e dashboards). Cobre os requisitos RF05, RF06, RF07, RF09 e a base do RF17 da documentação (`DAMPCode-Base/README.md`).

Esta etapa entrega:

- Hackathon no PostgreSQL, ligado à empresa que o criou;
- criação como rascunho, configuração posterior, publicação (só empresa verificada), cancelamento e encerramento;
- fases calculadas pelas datas: publicado (inscrições abertas) → ativo → em avaliação → encerrado;
- listagem pública só de Hackathons publicados, e listagem da empresa com rascunhos;
- telas do front ligadas a esse fluxo, inclusive para funcionários.

### Restrições

- Mesmas da entrega anterior (`2026-10-28-empresas-e-funcionarios-design.md`, seção 1): não renomear nada existente, seguir o estilo atual da API e do front, mudanças aditivas.
- **Rotas atuais continuam com o mesmo caminho e o mesmo formato de JSON** que o front lê: `GET /api/Hackathons`, `GET /api/Hackathons/{id}`, `POST /api/Hackathons/register/Hackathon`, `PUT /api/Hackathons/{id}`, `DELETE /api/Hackathons/{id}`.
- `CreateHackathonDto` (schema Swagger `CreateHackathonDTO`) continua sendo a entrada de criação e edição; ganha campos novos, não perde nenhum.
- Base do trabalho: as branches `feat/empresas-e-funcionarios` (API e front), porque esta etapa usa o `ICompanyAccessService` e as telas da etapa anterior, ainda não mescladas.

### Fora do escopo

- Inscrição, equipes e contagem real de participantes (etapa 3). O campo `participantes` continua vindo como hoje.
- Submissões (etapa 4), avaliação e vencedores (etapa 5).
- Object Storage: o logo continua salvo como hoje (data URL em texto).
- Migração dos Hackathons que estão no MongoDB: são dados de teste e ficam para trás.

## 2. Estado atual relevante

- `HackathonsController` grava `HackathonDocument` no MongoDB, sem autenticação nem vínculo com empresa; `PUT` e `DELETE` funcionam para qualquer pessoa.
- `Models/Hackathon.cs` existe (fora do `DbContext`) com `CompanyId`, `CreatedByAccountId`, `Title`, `Slug`, `Description`, `Area`, `ParticipationMode`, `Status`, `EvaluationMethod`, `StartsAt`, `EndsAt`, `ReviewEndsAt`, `ScheduledAt`, `ClosedAt`, `CancelledAt`, cores, `LogoStorageKey`, datas de auditoria. Enums `HackathonParticipationMode { Individual, Team }` e `HackathonStatus { Draft, Scheduled, Active, InReview, Closed, Cancelled }`.
- O front lê do Hackathon: `hackathonId`, `titulo`, `descricao`, `empresa`, `area`, `tecnologias`, `metodo`, `ranking`, `premiacao`, `corPrincipal`, `corSecundaria`, `corFundo`, `logo`, `dataFinal`, `status` (booleano: `true` = card "Ativo"), `participantes`.
- O formulário (`FormSection.jsx`) envia: `Titulo`, `Descricao`, `Empresa` (texto livre), `Area` (`tech`, `web`, `dados`, `ia`), `Tecnologias`, `Metodo` (`Avaliação IA`, `Autodata`, `Avaliação Manual`), `Ranking` (`Top 1`, `Top 10`, `Top 15`, `Top 20`, `Top 50`, `Top 100`), `Premiacao`, as três cores, `Logo` (data URL do arquivo), `DataCriacao`, `DataFinal`, `status` (`ativo`/`inativo`).
- `ManageChallenges.jsx` filtra Hackathons por `h.empresa === user.name`; os botões Delete/Edit não fazem nada. Na etapa anterior, `CreateChallenge` e `ManageChallenges` ficaram ocultos para funcionários.

## 3. Regras de negócio (decididas com o time)

1. **Criar é criar um rascunho.** Só o básico é obrigatório para criar: título, descrição, área e formato (individual ou equipe). Qualquer empresa cria rascunhos, verificada ou não.
2. **Rascunho só aparece para a empresa dona** (conta principal e funcionários ativos). Pode ser editado e apagado à vontade.
3. **Publicar exige todo o conteúdo crucial preenchido** e empresa **verificada**. Conteúdo crucial:
   - título e descrição;
   - área e tecnologias (pelo menos uma);
   - formato (individual ou equipe);
   - data de início, fim das submissões e fim da avaliação;
   - premiação e quantos ganham;
   - método de avaliação.
4. **Publicado = visível e com inscrições abertas.** Aparece no Explorar e na Home logo após publicar; começa de fato na data de início.
5. **Depois de publicado, o conteúdo crucial fica travado.** Só o visual (cores e logo) continua editável. Para mudar algo crucial, a empresa cancela.
6. **Fases andam sozinhas pelas datas:** publicado → ativo (no início) → em avaliação (no fim das submissões) → encerrado (no fim da avaliação).
7. **Cancelar** é possível em qualquer fase antes de encerrado. Cancelado some das listagens públicas.
8. **Encerrar manualmente** é possível durante a avaliação (a empresa terminou de avaliar antes do prazo).
9. **Quem gerencia:** conta principal e funcionários ativos da empresa (regra 7 da etapa anterior), via `ICompanyAccessService`.

## 4. Modelo de dados

### 4.1 `Hackathon` entra no `DbContext`

Usa a entidade existente. Mudanças na classe (aditivas, sem renomear):

| Propriedade | Situação | Observação |
|---|---|---|
| `EvaluationMethod` | já existe, `string?` | recebe o nome do método (`Avaliação IA`, `Autodata`, `Avaliação Manual`) |
| `Technologies` | **nova**, `ICollection<HackathonTechnology>` | tabela de ligação com `technologies`, igual a `CompanyTechnology` |
| `PrizeAmount` | **nova**, `decimal?` | valor total; obrigatório para publicar quando `RewardType == Money` |
| `RewardType` | **nova**, `HackathonRewardType?` | `Money` ou `Interview` |
| `WinnersCount` | **nova**, `int?` | 1, 10, 15, 20, 50 ou 100 |
| `Logo` | **nova**, `string?` | data URL como hoje; máx. 2 MB de texto. `LogoStorageKey` fica reservado para o Object Storage |
| `Company` | **nova** navegação | `Company.Hackathons` como coleção inversa |
| `ScheduledAt` | já existe | gravado com o instante da publicação |

`public enum HackathonRewardType { Money, Interview }`

Para publicar com `RewardType == Interview`, `PrizeAmount` pode ser nulo ou zero.

**`Status` gravado** só assume `Draft`, `Scheduled`, `Closed` (encerramento manual) e `Cancelled`. As fases `Active`, `InReview` e o `Closed` automático são **calculadas** (seção 4.3). Assim não há job em segundo plano, como na expiração dos convites.

### 4.2 Configuração `HackathonConfiguration`

Tabela `hackathons`, no padrão das outras:

- colunas em `snake_case`; `title` 200, `slug` 220, `description` 4000, `area` 100, `evaluation_method` 100, cores 7, `logo` sem limite de tipo (`text`), `prize_amount` `numeric(12,2)`;
- enums como string (`HasConversion<string>().HasMaxLength(32)`);
- `ck_hackathons_status`, `ck_hackathons_participation_mode`, `ck_hackathons_reward_type`, `ck_hackathons_winners_count` (`winners_count IN (1, 10, 15, 20, 50, 100)`), `ck_hackathons_dates` (`starts_at < ends_at AND ends_at < review_ends_at` quando os três não forem nulos);
- `ux_hackathons_slug` único; `ix_hackathons_company_id`; `ix_hackathons_status`;
- FKs `fk_hackathons_companies` e `fk_hackathons_accounts` (`created_by_account_id`), `Restrict`.

`HackathonTechnology` (`hackathon_technologies`), chave composta `(hackathon_id, technology_id)`, igual a `CompanyTechnology`. Tecnologias são resolvidas pelo mesmo critério do `AuthService` (nome normalizado; cria se não existir).

**Slug:** gerado do título (minúsculas, sem acento, espaços viram `-`), com sufixo `-2`, `-3`… se já existir. Não muda quando o título muda no rascunho.

Migration nova: `Hackathons`.

### 4.3 Fase efetiva

```
Draft      → "Rascunho"
Cancelled  → "Cancelado"
Closed     → "Encerrado"
Scheduled  e agora < StartsAt       → "Publicado"
Scheduled  e StartsAt ≤ agora < EndsAt       → "Ativo"
Scheduled  e EndsAt ≤ agora < ReviewEndsAt   → "Em avaliação"
Scheduled  e agora ≥ ReviewEndsAt   → "Encerrado"
```

Um método único (`HackathonPhase GetPhase(Hackathon, DateTime now)`, com `public enum HackathonPhase { Draft, Published, Active, InReview, Closed, Cancelled }`) é a única fonte dessa regra. Listagens e ações usam ele.

## 5. API

`HackathonsController` passa a usar um novo `IHackathonService`/`HackathonService`, com o mesmo formato de resultado das etapas anteriores (`HackathonServiceResult<T>`, `HackathonServiceError { None, NotFound, Forbidden, Conflict, Validation }`), e deixa de usar o MongoDB. `MongoDbService` continua registrado para a etapa 4.

### 5.1 Resposta (`HackathonResponseDto`)

Mesmos nomes de JSON que o front já lê, mais campos novos:

| JSON | Origem |
|---|---|
| `hackathonId` | `Id` (Guid em texto) |
| `titulo`, `descricao`, `area` | `Title`, `Description`, `Area` |
| `empresa` | `Company.Name` |
| `tecnologias` | nomes das tecnologias |
| `metodo` | `EvaluationMethod` |
| `ranking` | `"Top {WinnersCount}"` ou `null` |
| `premiacao` | `PrizeAmount ?? 0` |
| `corPrincipal`, `corSecundaria`, `corFundo`, `logo` | cores e `Logo` |
| `dataCriacao` | `CreatedAt` |
| `dataFinal` | `EndsAt` |
| `status` | `true` quando a fase é `Publicado` ou `Ativo` (mantém os cards "Ativo/Encerrado" de hoje) |
| `participantes` | `"0 pessoas já entraram"` até a etapa 3 |
| **`fase`** (novo) | texto da seção 4.3 |
| **`dataInicio`**, **`dataFimAvaliacao`** (novos) | `StartsAt`, `ReviewEndsAt` |
| **`formato`** (novo) | `"Individual"` ou `"Equipe"` |
| **`tipoPremiacao`** (novo) | `"Dinheiro"` ou `"Entrevista"` |
| **`companyId`** (novo) | `CompanyId` |

### 5.2 Entrada (`CreateHackathonDto`, campos novos)

Campos atuais permanecem. Ganha: `DataInicio` (`DateTime?`), `DataFimAvaliacao` (`DateTime?`), `Formato` (`"Individual"`/`"Equipe"`), `TipoPremiacao` (`"Dinheiro"`/`"Entrevista"`).

- `Empresa` é **ignorado**: a empresa vem do token (`ICompanyAccessService`).
- `status`, `DataCriacao` são **ignorados**: o ciclo controla o status; a criação usa a hora do servidor.
- `DataFinal` = fim das submissões (`EndsAt`).
- `Ranking` `"Top N"` → `WinnersCount = N`.
- `Premiacao` passa a `decimal?` e `DataFinal` a `DateTime?` (mesmos nomes; só aceitam vazio), para o rascunho poder ficar sem eles.
- **Datas são instantes exatos (data e hora).** O front usa `<input type="datetime-local">`, que é o horário local de quem preenche, e envia em ISO com fuso (`new Date(valor).toISOString()`). A API guarda em UTC. Início e fim das submissões podem cair no mesmo dia, desde que o início venha antes.
- **Rascunho:** o `PUT` substitui todos os campos editáveis pelo que veio (campo vazio limpa o valor).
- **Publicado:** o `PUT` aplica só `corPrincipal`, `corSecundaria`, `corFundo` e `Logo`, e **ignora** os demais campos, sem dar erro. O front já os desabilita; assim a edição visual nunca falha por diferença de formato (ordem das tecnologias, `0` × vazio, fuso das datas).

### 5.3 Rotas

| Método e rota | Quem pode | O que faz |
|---|---|---|
| `GET /api/Hackathons` | anônimo | Públicos: fase `Publicado`, `Ativo`, `Em avaliação` ou `Encerrado`. Rascunhos e cancelados nunca aparecem. Ordem: `StartsAt` crescente, encerrados por último. |
| `GET /api/Hackathons/{id}` | anônimo | Público se não for rascunho nem cancelado; rascunho/cancelado só para quem gerencia a empresa, senão 404. `{id}` aceita Guid **ou** slug. |
| `GET /api/Hackathons/empresa` | conta principal ou funcionário | Todos os Hackathons da empresa, inclusive rascunhos e cancelados. |
| `POST /api/Hackathons/register/Hackathon` | conta principal ou funcionário | Cria rascunho. 201 com `HackathonResponseDto`. |
| `PUT /api/Hackathons/{id}` | quem gerencia | Edita. Rascunho: tudo. Depois de publicado: só cores e logo; os outros campos são ignorados (seção 5.2). Cancelado/encerrado → 409. |
| `DELETE /api/Hackathons/{id}` | quem gerencia | Apaga **só rascunho** (remoção física). Outros → 409 "Só é possível apagar rascunhos. Use cancelar." |
| `POST /api/Hackathons/{id}/publicar` | quem gerencia | Valida crucial + empresa verificada + datas no futuro → `Scheduled`, `ScheduledAt = agora`. |
| `POST /api/Hackathons/{id}/cancelar` | quem gerencia | Fase diferente de `Encerrado`/`Cancelado` → `Cancelled`, `CancelledAt = agora`. |
| `POST /api/Hackathons/{id}/encerrar` | quem gerencia | Só na fase `Em avaliação` → `Closed`, `ClosedAt = agora`. |

Rotas de escrita têm `[Authorize]`; as duas de leitura pública, `[AllowAnonymous]` com leitura opcional do `sub` quando houver token.

O `PUT` mantém a assinatura de rota, mas o corpo passa a ser `CreateHackathonDto` em vez de `HackathonDocument` (o `HackathonDocument` deixa de existir na API; o front nunca chamou o `PUT`).

### 5.4 Validações e mensagens

- Sem empresa (participante comum, admin) → 403 "Apenas empresas e seus funcionários podem gerenciar Hackathons."
- Hackathon de outra empresa → 404 "Hackathon não encontrado." (não revela existência).
- Criar sem título, descrição, área ou formato → 400 com a lista do que falta.
- Publicar:
  - empresa não verificada → 403 "Sua empresa precisa ser verificada para publicar.";
  - faltando crucial → 400 "Preencha antes de publicar: <campos>." (nomes em português, separados por vírgula);
  - `DataInicio` no passado → 400 "A data de início precisa ser no futuro.";
  - ordem das datas errada → 400 "As datas precisam seguir: início, fim das submissões, fim da avaliação.";
  - já publicado/cancelado/encerrado → 409 "Este Hackathon já foi publicado."
- Editar Hackathon cancelado ou encerrado → 409 "Este Hackathon já foi encerrado ou cancelado."
- Cancelar encerrado/cancelado → 409 "Este Hackathon já foi encerrado ou cancelado."
- Encerrar fora da avaliação → 409 "Só é possível encerrar durante a avaliação."
- Logo maior que 2 MB → 400 "O logo deve ter no máximo 2 MB."

## 6. Front (`Damp-Code-Front`)

### 6.1 Criação e edição

- `CreateHackathon.jsx` envia `Authorization` e passa a criar **rascunho**. Depois de criar, vai para a edição do rascunho (`/cadastroHackaton/:id`, nova rota que reaproveita a mesma página).
- `FormSection.jsx`:
  - remove o campo "Empresa Parceira" (a empresa vem da conta) e o select "Status";
  - adiciona: formato (Individual/Equipe), data de início, data de fim da avaliação (a "Data Final" atual vira "Fim das submissões"), tipo de premiação (Dinheiro/Entrevista; o valor só aparece para Dinheiro);
  - na edição de Hackathon publicado, os campos cruciais ficam desabilitados com o aviso "Depois de publicado, só cores e logo podem ser alterados.".
- Botões na página: "Salvar rascunho" (sempre), "Publicar" (só rascunho; desabilitado com o aviso de verificação quando `canPublish` for falso). Mensagens de erro da API (`detail`) aparecem abaixo dos botões.

### 6.2 Gerenciar (`ManageChallenges.jsx`)

- Lista vem de `GET /api/Hackathons/empresa` (fim do filtro por `user.name`).
- Coluna "Status" mostra a `fase` com cor: Rascunho (neutro), Publicado/Ativo (sucesso), Em avaliação (aviso), Encerrado/Cancelado (perigo).
- Ações por fase: Rascunho → Editar, Publicar, Apagar; Publicado/Ativo → Editar (só visual), Cancelar; Em avaliação → Encerrar, Cancelar; sempre → Detalhes. Apagar e Cancelar pedem confirmação.
- O PDF continua com as mesmas colunas, usando a `fase`.

### 6.3 Funcionários

`CreateChallenge` e `ManageChallenges` voltam a aparecer para funcionários no `CompanyDashboard` (fim da regra provisória da etapa anterior).

### 6.4 Público

- Explorar, Home e dashboard do participante continuam chamando `GET /api/Hackathons`; agora recebem só publicados. `HackathonCard` continua usando `status`.
- `HackathonPage` mostra "Inscrições abertas · começa em dd/mm" na fase Publicado e as três datas.

### 6.5 API simulada

`installMockApi.js`/`seed.js` ganham as mesmas rotas e regras. Os quatro Hackathons do seed viram publicados (com datas, formato e tipo de premiação coerentes), e o iFood ganha um rascunho para testar a publicação.

## 7. Testes

- `HackathonPhaseTests`: cada linha da tabela 4.3, nos limites exatos (`agora == StartsAt` é Ativo; `agora == EndsAt` é Em avaliação; `agora == ReviewEndsAt` é Encerrado).
- `HackathonServiceTests`: criar rascunho (empresa não verificada também), criar sem crucial básico, editar rascunho, campos cruciais ignorados depois de publicado (valor no banco não muda), editar cor depois de publicado, apagar só rascunho, publicar (verificada, não verificada, faltando campos, data passada, datas fora de ordem, Interview sem prêmio), cancelar, encerrar fora e dentro da avaliação, funcionário gerenciando, outra empresa recebendo 404, slug repetido.
- `HackathonsEndpointTests`: listagem anônima sem rascunho e sem cancelado; rascunho por id anônimo → 404, pela empresa → 200; criar sem token → 401; participante criando → 403; JSON com os nomes antigos (`hackathonId`, `titulo`, `empresa`, `status`, `dataFinal`).
- Front: lint igual à base, build, e roteiro no navegador com o mock.

## 8. Decisões tomadas por Claude (para o time revisar)

- Hackathon no PostgreSQL usando a entidade reservada; MongoDB fica para as submissões (aprovado pelo usuário).
- Fases `Ativo`/`Em avaliação`/`Encerrado` calculadas pelas datas, sem job.
- Encerramento automático no fim da avaliação, além do manual.
- **Tipo de premiação (Dinheiro ou Entrevista) entra agora como campo crucial**, porque a etapa 5 depende dele e ele fica travado depois de publicado.
- Datas como data e hora (`datetime-local`), guardadas em UTC.
- `PUT` em Hackathon publicado ignora os campos travados em vez de responder 409.
- `Premiacao` e `DataFinal` do `CreateHackathonDto` passam a aceitar vazio.
- Logo continua como data URL em texto (limite 2 MB) até existir Object Storage.
- `DELETE` apaga só rascunho; Hackathon publicado só pode ser cancelado.
- Cancelado some do público; encerrado continua visível (base do RF17).
- `GET /{id}` aceita slug além do Guid.
- `status` booleano mantido para os cards atuais; `fase` é o campo novo com o estado real.
- `participantes` continua fixo até a etapa 3.
- Dados de Hackathon no MongoDB não são migrados.

## 9. Deixado para o builder

- Algoritmo exato do slug (desde que estável, sem acento e único).
- Layout dos novos campos e botões, seguindo os vizinhos.
- Divisão em commits.
