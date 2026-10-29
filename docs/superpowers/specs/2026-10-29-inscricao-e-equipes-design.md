# Design — Inscrição e equipes (quarta entrega)

## 1. Objetivo

Terceira das seis etapas combinadas (empresas e funcionários → ciclo de vida do Hackathon → **inscrição e equipes** → submissões → avaliação e premiação → perfil e dashboards).

Esta etapa entrega:

- inscrição de participantes em Hackathon publicado, no formato que ele escolheu (individual ou equipe);
- equipes específicas de cada Hackathon, com convite por e-mail e pedido para entrar;
- contagem real de inscritos no lugar do texto fixo "0 pessoas já entraram";
- telas do front ligadas a esse fluxo (página do Hackathon, painel do participante, gerenciar da empresa).

### Restrições

- Mesmas das entregas anteriores: não renomear nada existente, seguir o estilo atual da API e do front, mudanças aditivas.
- Rotas e campos de JSON atuais continuam iguais; o campo `participantes` só passa a vir com o número real.
- Base: branches `feat/empresas-e-funcionarios` (API e front), que já contêm as etapas 1 e 2 e ainda não foram mescladas na principal.

### Fora do escopo

- Submissões (etapa 4): esta etapa só cria a inscrição (`Participation`) que a `Submission` já referencia por `ParticipationId`.
- Lista detalhada de inscritos para a empresa (etapa 5, avaliação).
- Notificação por e-mail: "convidar pelo e-mail" significa achar a conta pelo e-mail; o convite aparece dentro da plataforma, como na etapa 1.

## 2. Regras de negócio (decididas com o usuário)

1. **Quem se inscreve:** só contas de participante, em Hackathon na fase **Publicado**.
2. **Janela:** inscrever, cancelar inscrição, criar equipe, convidar, pedir para entrar, aceitar, recusar e sair só são possíveis **até a data de início** (fase Publicado). Depois disso tudo fica congelado.
3. **Formato individual:** "Participar" inscreve; "Cancelar inscrição" sai.
4. **Formato equipe:**
   - um participante **cria a equipe** (fica inscrito nela) e **convida outros pelo e-mail**; o convidado aceita ou recusa na plataforma;
   - a equipe pode ficar **aberta a pedidos**: qualquer participante vê a equipe e pede para entrar; **qualquer integrante** aceita ou recusa;
   - **máximo de 5 pessoas** (trava rígida). O mínimo de 2 é só recomendação: **equipe com 1 pessoa participa** normalmente.
   - a equipe existe só naquele Hackathon.
5. **Uma inscrição por pessoa por Hackathon:** quem está numa equipe não entra em outra no mesmo Hackathon sem sair da atual.
6. **Sair da equipe** = deixar de estar inscrito. Se o criador sai, a equipe continua com os demais; se o último sai, a equipe é desfeita.
7. **Contagem pública:** "X pessoas já entraram" conta pessoas inscritas.

### Decisões de Claude (aprovadas na conversa)

- Empresa e funcionários **da empresa organizadora** não podem participar do próprio Hackathon (conflito de interesse). Funcionário de outra empresa pode.
- Convite de equipe expira em **24h** e só vale para quem já tem conta de participante (mesma regra dos convites de funcionário).
- Com 5 pessoas, novos convites e pedidos ficam bloqueados.
- Ao entrar numa equipe (por qualquer caminho), os outros pedidos pendentes da pessoa no mesmo Hackathon são cancelados.

### Decisões de Claude (depois da revisão do documento)

- **Nada fica pendente sem poder ser aceito.** Quando alguém se inscreve (individual, cria equipe, aceita convite ou tem pedido aceito), todos os **convites recebidos e pedidos enviados** dessa pessoa no mesmo Hackathon passam a `Cancelled`. Quando uma equipe chega a 5, os **convites e pedidos pendentes dela** passam a `Cancelled`. Assim o painel nunca mostra "Aceitar" que só daria erro.
- **Organizadora depois da inscrição:** aceitar convite de funcionário (etapa 1) passa a ser recusado com 409 "Você está inscrito em um Hackathon desta empresa. Saia da inscrição antes de aceitar." enquanto a pessoa tiver inscrição ativa em Hackathon dessa empresa que não esteja encerrado nem cancelado. É uma checagem a mais no `CompanyService.AcceptInvitationAsync`, sem mudar nome nem rota.
- **Trava de 5 contra aceites simultâneos:** `HackathonTeam` ganha `MemberCount` (int), atualizado em toda entrada e saída e marcado como token de concorrência (`IsConcurrencyToken`). Dois aceites ao mesmo tempo atualizam a mesma linha; o segundo recebe `DbUpdateConcurrencyException`, que vira 409 "Esta equipe já está completa." se a equipe encheu, ou "Tente novamente." nos outros casos. Funciona igual no PostgreSQL e no InMemory.
- **`GET /api/Participations/me` lista inscrições ativas em qualquer fase** (Publicado, Ativo, Em avaliação, Encerrado e Cancelado), com a `fase` de cada uma, ordenadas pela data de início. A pessoa precisa saber que um Hackathon em que entrou foi cancelado, e a etapa 4 vai chegar nas submissões por essa lista.
- **Ninguém remove integrante no MVP.** Todos os integrantes têm os mesmos poderes; deixar qualquer um expulsar os outros abriria espaço para abuso. Quem sai é a própria pessoa.
- **Branch:** `feat/inscricao-e-equipes` nos dois repositórios, criada a partir de `feat/empresas-e-funcionarios`, como na etapa 2.

## 3. Estado atual relevante

- `Submission` (fora do `DbContext`) tem `ParticipationId`: a inscrição se chama `Participation`.
- `HackathonResponseDto.Participantes` vem fixo como `"0 pessoas já entraram"`; `HackathonPage` tem um botão "Participar" sem ação.
- `ICompanyAccessService.GetManagedCompanyAsync` diz qual empresa a conta gerencia (usado para barrar a organizadora).
- `HackathonPhases.GetPhase(hackathon, now)` dá a fase; só `Published` permite mudanças nesta etapa.
- Convites de funcionário (`CompanyInvitation`, `ReceivedInvitations.jsx`) são o modelo de convite a seguir.

## 4. Modelo de dados (PostgreSQL)

### 4.1 Entidades novas

**`HackathonTeam`** — tabela `hackathon_teams`

| Propriedade | Observação |
|---|---|
| `Id`, `HackathonId`, `CreatedByAccountId` | FKs `fk_hackathon_teams_hackathons` (Cascade), `fk_hackathon_teams_accounts` (Restrict) |
| `Name` (100), `NormalizedName` (100) | `ux_hackathon_teams_hackathon_name` único em (`hackathon_id`, `normalized_name`) |
| `IsOpen` (bool) | aceita pedidos para entrar; padrão `false` |
| `MemberCount` (int) | membros ativos; token de concorrência (`IsConcurrencyToken`); `ck_hackathon_teams_member_count` (`member_count BETWEEN 0 AND 5`) |
| `CreatedAt`, `UpdatedAt` | |

**`Participation`** — tabela `participations` (a inscrição)

| Propriedade | Observação |
|---|---|
| `Id`, `HackathonId`, `AccountId` | FKs `fk_participations_hackathons` (Cascade), `fk_participations_accounts` (Restrict) |
| `TeamId` (`Guid?`) | nulo no individual; FK `fk_participations_hackathon_teams` (Restrict) |
| `JoinedAt`, `LeftAt` (`DateTime?`) | sair grava `LeftAt` (histórico preservado, como `CompanyMembership.EndedAt`) |
| `CreatedAt`, `UpdatedAt` | |

Índices: `ux_participations_active_account` único em (`hackathon_id`, `account_id`) **filtrado por `left_at IS NULL`**; `ix_participations_team_id`; `ix_participations_account_id`.

**`TeamInvitation`** — tabela `team_invitations` (mesmo formato de `CompanyInvitation`)

`Id`, `TeamId`, `InvitedByAccountId`, `Email` (254), `NormalizedEmail` (254), `Status` (`Pending`, `Accepted`, `Declined`, `Revoked`, `Cancelled`), `ExpiresAt` (= criação + 24h), `RespondedAt`, `CreatedAt`, `UpdatedAt`. Expirado = `Pending` com `ExpiresAt <= agora` (calculado, sem job). `ck_team_invitations_status`; `ix_team_invitations_normalized_email`; `ix_team_invitations_team_id`.

**`TeamJoinRequest`** — tabela `team_join_requests`

`Id`, `TeamId`, `AccountId`, `Status` (`Pending`, `Accepted`, `Declined`, `Cancelled`), `RespondedByAccountId?`, `RespondedAt`, `CreatedAt`, `UpdatedAt`. `ck_team_join_requests_status`; `ix_team_join_requests_team_id`; `ix_team_join_requests_account_id`.

Enums como string (`HasConversion<string>().HasMaxLength(32)`), nomes `pk_`/`fk_`/`ux_`/`ix_`/`ck_` como nas outras tabelas. Navegações inversas: `Hackathon.Teams`, `Hackathon.Participations`, `HackathonTeam.Members` (participações ativas e antigas), `HackathonTeam.Invitations`, `HackathonTeam.JoinRequests`. Migration: `ParticipationsAndTeams`.

`Submission` continua fora do `DbContext` (etapa 4).

### 4.2 Regras derivadas

- "Membros da equipe" = participações com `TeamId` da equipe e `LeftAt == null`.
- Equipe sem membros ativos é **apagada** (com seus convites e pedidos, em cascata). Participações antigas que apontavam para ela ficam com `TeamId = null`. *Mais simples que guardar equipes vazias; nenhum fluxo desta etapa lê equipe desfeita.*
- Nome de equipe: obrigatório, até 100 caracteres, único no Hackathon sem diferenciar maiúsculas (`Normalize`).

## 5. API

### 5.1 Serviço

`IParticipationService` / `ParticipationService(DampCodeDbContext, ICompanyAccessService)`, com `ParticipationServiceResult<T>` e `ParticipationServiceError { None, NotFound, Forbidden, Conflict, Expired, Validation }` — mesmo formato das etapas anteriores. Controllers finos, erros em `ProblemDetails { Detail }`.

### 5.2 Rotas

Inscrição fica sob o Hackathon (`HackathonsController`); equipe e "minhas inscrições" num controller novo `TeamsController` (`api/Teams`) e `ParticipationsController` (`api/Participations`). Todas com `[Authorize]`, exceto a lista de equipes abertas.

| Método e rota | O que faz |
|---|---|
| `POST /api/Hackathons/{id}/inscricao` | Inscrição individual. 409 se o Hackathon for de equipe. |
| `DELETE /api/Hackathons/{id}/inscricao` | Cancela a inscrição individual (grava `LeftAt`). No formato equipe → 409 "Use sair da equipe.". |
| `GET /api/Hackathons/{id}/equipes` | Anônimo. Equipes **abertas** do Hackathon com vaga: `teamId`, `nome`, `membros` (quantidade), `vagas`. |
| `POST /api/Hackathons/{id}/equipes` | Cria equipe `{ nome, aberta }` e inscreve o criador nela. |
| `PATCH /api/Teams/{teamId}` | Integrante muda `nome` e/ou `aberta`. |
| `POST /api/Teams/{teamId}/convites` | Integrante convida `{ email }`. |
| `DELETE /api/Teams/{teamId}/convites/{inviteId}` | Integrante revoga convite pendente. |
| `POST /api/Teams/convites/{inviteId}/aceitar` · `/recusar` | O convidado responde. |
| `POST /api/Teams/{teamId}/pedidos` | Participante pede para entrar (equipe aberta). |
| `DELETE /api/Teams/pedidos/{requestId}` | Quem pediu cancela o próprio pedido pendente. |
| `POST /api/Teams/pedidos/{requestId}/aceitar` · `/recusar` | Integrante da equipe responde. |
| `DELETE /api/Teams/{teamId}/membros/me` | Sair da equipe (grava `LeftAt`; desfaz a equipe se ficar vazia). |
| `GET /api/Participations/me` | Painel do participante (5.3). |

### 5.3 `GET /api/Participations/me`

```json
{
  "inscricoes": [
    { "hackathonId", "titulo", "empresa", "fase", "formato", "dataInicio",
      "equipe": null | { "teamId", "nome", "aberta",
        "membros": [{ "accountId", "nome" }],
        "convitesPendentes": [{ "id", "email", "expiraEm" }],
        "pedidosPendentes": [{ "id", "accountId", "nome" }] } }
  ],
  "convitesRecebidos": [{ "id", "teamId", "equipe", "hackathonId", "hackathon", "convidadoPor", "expiraEm" }],
  "pedidosEnviados": [{ "id", "teamId", "equipe", "hackathonId", "hackathon" }]
}
```

Inscrições ativas (`LeftAt == null`) em qualquer fase, com `fase`, ordenadas por `dataInicio`; convites pendentes e não expirados; pedidos pendentes. `nome` vem de `UserProfile.Name`.

### 5.4 Contagem no Hackathon

`HackathonResponseDto.Participantes` passa a ser `"{N} pessoas já entraram"` (`"1 pessoa já entrou"` quando N = 1), com N = participações ativas. A listagem pública conta tudo numa consulta agrupada, sem uma consulta por Hackathon.

### 5.5 Validações e mensagens

| Situação | Resposta |
|---|---|
| Conta não é participante | 403 "Apenas participantes podem se inscrever em Hackathons." |
| Conta gerencia a empresa organizadora | 403 "Quem organiza o Hackathon não pode participar dele." |
| Hackathon inexistente, rascunho ou cancelado | 404 "Hackathon não encontrado." |
| Fase diferente de Publicado | 409 "As inscrições deste Hackathon estão encerradas." |
| Já inscrito (individual ou em equipe) | 409 "Você já está inscrito neste Hackathon." |
| Inscrição individual em Hackathon de equipe | 409 "Este Hackathon é em equipe. Crie uma equipe ou entre em uma." |
| Criar equipe em Hackathon individual | 409 "Este Hackathon é individual." |
| Nome de equipe vazio ou > 100 | 400 "Informe um nome de equipe com até 100 caracteres." |
| Nome repetido no Hackathon | 409 "Já existe uma equipe com este nome neste Hackathon." |
| Ação de integrante feita por quem não é da equipe | 404 "Equipe não encontrada." |
| Equipe com 5 pessoas (convidar, pedir, aceitar) | 409 "Esta equipe já está completa." |
| Convite para e-mail sem conta de participante | 404 "Nenhum participante encontrado com este e-mail." |
| Convidar alguém já na equipe / convite pendente repetido | 409 "Esta pessoa já está na equipe." / "Já existe um convite pendente para este e-mail." |
| Pedido para equipe fechada | 409 "Esta equipe não está aceitando pedidos." |
| Pedido pendente repetido | 409 "Você já pediu para entrar nesta equipe." |
| Aceitar convite estando inscrito em outra equipe do mesmo Hackathon | 409 "Você já está inscrito neste Hackathon. Saia da equipe atual antes." |
| Convite já respondido / pedido já respondido | 409 "Este convite já foi respondido." / "Este pedido já foi respondido." |
| Convite expirado | 410 "Este convite expirou." |
| Convite ou pedido que não é da conta | 404 "Convite não encontrado." / "Pedido não encontrado." |

Aceitar convite ou pedido roda numa transação e trata `DbUpdateException` do índice `ux_participations_active_account` como 409 "Você já está inscrito neste Hackathon." (corrida entre dois aceites da mesma pessoa) e `DbUpdateConcurrencyException` do `MemberCount` como 409 "Esta equipe já está completa." (corrida entre duas pessoas na última vaga).

Aceitar convite de funcionário (etapa 1) com inscrição ativa em Hackathon não encerrado/cancelado da mesma empresa → 409 "Você está inscrito em um Hackathon desta empresa. Saia da inscrição antes de aceitar."

## 6. Front (`Damp-Code-Front`)

### 6.1 Página do Hackathon (`HackathonPage.jsx`)

- Carrega `GET /api/Participations/me` (se logado como participante) para saber se a pessoa já está inscrita.
- **Individual:** botão "Participar" chama a inscrição; inscrito vê "Inscrito ✓" e "Cancelar inscrição".
- **Equipe:** inscrito vê "Sua equipe: <nome>" com link para o painel; não inscrito vê "Criar equipe" (nome + caixa "Aceitar pedidos") e a lista de equipes abertas (`GET /api/Hackathons/{id}/equipes`) com "Pedir para entrar".
- Fora da fase Publicado, os botões somem e aparece "Inscrições encerradas".
- Sem login, os botões levam para `/loginUser`.
- Erros da API (`detail`) aparecem perto dos botões.

### 6.2 Painel do participante (`ParticipantDashboard.jsx`)

Novo componente `MyParticipations.jsx` (em `app-components/participant-dashboard/`), ao lado de `ReceivedInvitations`:

- "Convites de equipe" com Aceitar/Recusar.
- "Minhas inscrições": cada Hackathon com fase e data de início; se for equipe: nome, membros, chave "Aceitando pedidos", convidar por e-mail, convites pendentes (revogar), pedidos recebidos (aceitar/recusar), "Sair da equipe"; se individual: "Cancelar inscrição".
- "Pedidos enviados" com "Cancelar pedido".
- Ações que mudam algo só aparecem na fase Publicado.

### 6.3 Empresa

`ManageChallenges` já mostra `participantes`; passa a ser o número real sem mudança de código. Nada mais muda para a empresa nesta etapa.

### 6.4 API simulada

`installMockApi.js` e `seed.js` ganham as mesmas rotas e regras. Seed: Ana inscrita no Hackathon individual publicado; uma equipe aberta "Os Rápidos" com Bruno no Hackathon de equipe publicado ("Limite em tempo real", Nubank), para testar pedido e convite.

## 7. Testes

- `ParticipationServiceTests` (EF InMemory): inscrição individual (sucesso, já inscrito, formato errado, fase errada, organizadora barrada, empresa barrada), cancelar e se inscrever de novo, criar equipe (nome repetido sem diferenciar maiúsculas), convite (sucesso, sem conta, repetido, equipe cheia, expirado → 410, aceitar já inscrito em outra equipe → 409, revogar), pedido (equipe fechada → 409, aceitar por integrante, aceitar com equipe cheia → 409, outros pedidos da pessoa cancelados), sair (equipe continua; último sai → equipe desfeita), fase Ativo bloqueia tudo, `GET me` com convites expirados fora.
- Cancelamentos automáticos: inscrever-se cancela convites/pedidos da pessoa no Hackathon; equipe completa cancela os pendentes dela; `MemberCount` desatualizado (concorrência simulada mudando o valor no banco) → 409.
- `CompanyServiceTests`: aceitar convite de funcionário com inscrição ativa em Hackathon da empresa → 409; com inscrição em Hackathon encerrado → aceita.
- `HackathonServiceTests`: `participantes` com contagem real ("1 pessoa já entrou", "3 pessoas já entraram"), saídos não contam.
- `ParticipationsEndpointTests`: inscrição sem token → 401, empresa → 403, lista de equipes abertas anônima, fluxo convite → aceitar → `GET me` mostra a equipe.
- Front: lint sem problemas novos, build, roteiro no navegador com o mock.

## 8. Deixado para o builder

- Layout dos blocos novos, seguindo os vizinhos (`ReceivedInvitations`, `ManageEmployees`).
- Divisão em commits e se a inscrição individual reaproveita o mesmo método de "entrar" das equipes.
