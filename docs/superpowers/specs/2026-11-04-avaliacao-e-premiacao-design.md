# Design — Avaliação e premiação (sexta entrega)

## 1. Objetivo

Quinta das seis etapas combinadas (empresas e funcionários → ciclo de vida do Hackathon → inscrição e equipes → submissões → **avaliação e premiação** → perfil e dashboards).

Esta etapa entrega:

- **prêmios por posição**, definidos pela empresa ao criar o Hackathon;
- **nota** de cada entrega, dada pela empresa na tela de comparação (a "mini IDE" da etapa 4), com uma **sugestão automática** pela semelhança com a solução de referência;
- **ranking** das entregas por nota e **escolha livre dos vencedores** (a empresa pode premiar quem não está em primeiro);
- **publicação do resultado**, que o participante vê no próprio painel (posição, nota e prêmio).

### Restrições

- Mesmas das entregas anteriores: não renomear nada existente (classes, rotas, DTOs, campos de JSON), seguir o estilo atual da API e do front, mudanças aditivas.
- **Identidade visual mantida**: paleta roxa (`surface`, `surface-light`, `brand`, `highlight`), fontes Dangrek/Exo, mesmos cartões, bordas e botões.
- Base: `master` (API) e `main` (front), que já têm as etapas 1–4 mescladas. Branch nova `feat/avaliacao-e-premiacao` nos dois repositórios.

### Fora do escopo

- **Notificação** (sininho, e-mail etc.): o usuário vai decidir o canal depois. Nesta etapa o resultado aparece no painel do participante.
- Executar código ou casos de teste automáticos.
- Avaliação por IA.
- Resultado público na página do Hackathon e ranking geral da plataforma (etapa 6).
- A página `Pages/company/Ranking.jsx` atual (gráficos da empresa): fica para a etapa 6 e não é mexida aqui.

## 2. Regras de negócio

### Decididas com o usuário

1. A empresa avalia na tela de comparação: código da pessoa ao lado da solução de referência.
2. A nota parte de uma **sugestão automática pela semelhança** com a referência; a **nota final é da empresa**. (Proposta de Claude, aceita pelo usuário. Casos de teste e IA ficaram para depois.)
3. Existe um ranking com pontos, mas **a empresa escolhe quem ganha**, mesmo que não seja o primeiro (por exemplo, pela organização do código).
4. **A empresa define na criação do Hackathon quantas posições são premiadas e o prêmio de cada uma.**
5. O participante fica sabendo **a posição em que ficou e a premiação**. O canal (notificação) fica para depois.

### Decisões de Claude

- **Prêmio de cada posição:** tipo (`Dinheiro` ou `Entrevista`, os dois que já existem), valor (só para dinheiro) e uma descrição curta opcional (até 120 caracteres, ex.: "Entrevista para vaga júnior"). De **1 a 10** posições.
- **Quando avaliar:** nota e vencedores só podem ser alterados depois do fim das submissões (fase **Em avaliação**) ou, se o prazo de avaliação já passou e o Hackathon encerrou pelas datas, **enquanto o resultado não foi publicado**. A empresa já vê as entregas antes (etapa 4), mas sem campo de nota.
- **Nota:** inteiro de 0 a 100, mais um comentário opcional (até 1.000 caracteres) que o participante vê depois da publicação.
- **Sugestão:** porcentagem de semelhança (0–100) entre a versão que vale e a referência (algoritmo na seção 4). Aparece ao lado da nota e preenche o campo enquanto não há nota salva. Sem referência cadastrada, não há sugestão e a nota é toda manual.
- **Ranking:** ordenado pela nota (maior primeiro); empate desempatado por quem enviou a versão que vale primeiro (`LastSubmittedAt` menor). Entregas sem nota ficam no fim, marcadas "Sem nota". Quem não entregou não entra.
- **Vencedores:** a empresa atribui cada posição premiada a uma entrega. Uma entrega recebe no máximo um prêmio, e cada posição tem no máximo um vencedor. Uma posição pode ficar sem vencedor, mas a tela pede confirmação ("A posição 3 ficou sem vencedor. Publicar assim mesmo?").
- **Classificação final (a posição que o participante vê):** os premiados ocupam as posições dos seus prêmios (quem recebeu o prêmio de 1º lugar aparece como 1º, mesmo com nota menor); os demais vêm em seguida, na ordem do ranking por nota, a partir da próxima posição livre (com 3 prêmios dados, o próximo é o 4º). Assim o "ficou em 1º" sempre bate com o prêmio.
- **Publicar resultado:** o botão "Publicar resultado" exige que **todas as entregas tenham nota**. Ele grava a data de publicação e, se o Hackathon ainda está em avaliação, **encerra o Hackathon** (mesmo efeito do "Encerrar" atual). Depois de publicado, nada muda: nota, comentário e vencedores ficam travados.
- **"Encerrar" sem publicar** continua possível (comportamento da etapa 2). Um Hackathon encerrado sem resultado ainda pode ter o resultado avaliado e publicado depois.
- **Equipe:** a nota e o prêmio são da equipe; todos os integrantes veem o resultado.
- **Hackathons existentes:** a migration cria um prêmio de 1º lugar a partir dos campos antigos (`RewardType`, `PrizeAmount`) para cada Hackathon que tenha `RewardType` = Entrevista, ou Dinheiro com `PrizeAmount > 0`. Rascunhos incompletos (dinheiro sem valor) ficam sem prêmio e a empresa completa ao editar. A migration **não mexe** em `WinnersCount`: o "Top N" antigo continua aparecendo nesses Hackathons até a próxima edição com a lista nova.

## 3. Estado atual relevante

- `Hackathon` tem `PrizeAmount`, `RewardType` (`Money`/`Interview`) e `WinnersCount` (1, 10, 15, 20, 50, 100), preenchidos pelos campos `Premiacao`, `TipoPremiacao` e `Ranking` ("Top N") do `CreateHackathonDto`. O `HackathonResponseDto` devolve `ranking`, `premiacao` e `tipoPremiacao`. A publicação (etapa 2) exige tipo, valor (se dinheiro) e "quantos ganham".
- Fases (`HackathonPhases.GetPhase`): `Active` até `EndsAt`, `InReview` até `ReviewEndsAt`, depois `Closed`; `CloseAsync` (rota `POST /{id}/encerrar`) encerra manualmente durante `InReview`.
- Etapa 4: `Submission` (uma por pessoa ou equipe), versão que vale em `CurrentMongoVersionId`, código no MongoDB via `ISubmissionVersionStore.GetCodeAsync`; referência em `Hackathon.ReferenceLanguage/ReferenceCode`. Tela `SubmissionsReview.jsx` (`/dashboard/Empresa/entregas/:id`) com lista à esquerda e referência e código lado a lado.
- Participante: `GET /api/Participations/me` (usado por `MyParticipations.jsx`) devolve `ParticipationsOverviewDto` com `inscricoes` (`MyParticipationDto`).
- Front usa `fetch` direto para `https://localhost:7092/api/...` e a API simulada em `src/mock/installMockApi.js` + `seed.js`.

## 4. Semelhança com a referência

`SimilarityScore.Compute(string reference, string code, string language) → int` (0–100), classe estática pura em `Services/`:

1. **Remover comentários** conforme a linguagem: `//` e `/* */` para JavaScript, C#, Java; `#` para Python; `--` e `/* */` para SQL; nenhum para "Outra". Strings ficam como estão.
2. **Separar em tokens:** identificadores/palavras, números e cada símbolo isolado; espaços e quebras de linha são descartados.
3. **Normalizar nomes:** todo identificador que não é palavra reservada da linguagem vira `ID`, e todo número vira `NUM`. As listas de palavras reservadas ficam numa tabela pequena por linguagem (para "Outra" e SQL, comparação sem diferenciar maiúsculas). Com isso, renomear variáveis não muda a nota.
4. **Comparar:** coeficiente de Dice sobre os multiconjuntos de trigramas de tokens: `2·|A∩B| / (|A|+|B|)`, arredondado para inteiro em porcentagem. Com menos de 3 tokens em algum lado, comparar unigramas. Ambos vazios → 0.

Custo linear, sem dependência nova. A sugestão é calculada na hora em que a empresa abre o ranking ou uma entrega (a referência pode mudar), não é gravada. Se a linguagem da entrega for diferente da referência, a sugestão é calculada mesmo assim, com a linguagem da entrega para os comentários e palavras reservadas, e a tela mostra o aviso "Linguagem diferente da referência".

## 5. Modelo de dados (PostgreSQL)

**`HackathonPrize`** (nova) — tabela `hackathon_prizes`: `Id`, `HackathonId` (FK `fk_hackathon_prizes_hackathons`, Cascade), `Position` (int, 1–10), `RewardType` (`HackathonRewardType`, gravado como os enums atuais), `Amount` (`decimal?`, mesma precisão de `PrizeAmount`), `Description` (`string?`, 120), `CreatedAt`. `ux_hackathon_prizes_position` único em (`hackathon_id`, `position`). `Hackathon` ganha `ICollection<HackathonPrize> Prizes`.

**`Submission`** ganha (aditivo):

| Propriedade | Observação |
|---|---|
| `Score` (`int?`) | nota 0–100; nula enquanto não avaliada |
| `Feedback` (`string?`, 1000) | comentário para o participante |
| `EvaluatedAt` (`DateTime?`) | última vez que a nota mudou |
| `EvaluatedByAccountId` (`Guid?`) | FK `fk_submissions_evaluated_by_accounts` (Restrict) |
| `PrizePosition` (`int?`) | posição do prêmio recebido; `ux_submissions_prize_position` único em (`hackathon_id`, `prize_position`) com filtro `prize_position IS NOT NULL` |
| `FinalPosition` (`int?`) | classificação final, gravada na publicação |

`Status` da `Submission` continua `"Submitted"`; ao publicar, passa a `"Evaluated"`.

**`Hackathon`** ganha `ResultsPublishedAt` (`DateTime?`).

**Campos antigos mantidos em sincronia:** ao salvar a lista de prêmios, `WinnersCount` = quantidade de prêmios, e `RewardType`/`PrizeAmount` = os do prêmio de 1º lugar. Assim `ranking` ("Top N"), `premiacao`, `tipoPremiacao`, os cards e os gráficos atuais continuam funcionando. A validação de "quantos ganham" deixa de exigir os valores fixos 1/10/15/20/50/100 quando a lista vem preenchida.

Migration: `Evaluation` (cria a tabela, as colunas e o insert dos prêmios de 1º lugar dos Hackathons existentes, conforme a seção 2).

## 6. API

### 6.1 Entrada do Hackathon

`CreateHackathonDto` ganha `Premios` (`List<PrizeDto>?`), com `PrizeDto(int Posicao, string Tipo, decimal? Valor, string? Descricao)`. Regras:

- Se `Premios` vier com itens, ele manda: posições 1..N consecutivas, N ≤ 10, `Tipo` "Dinheiro" ou "Entrevista", valor > 0 e ≤ 9.999.999.999,99 para dinheiro (ignorado para entrevista), descrição ≤ 120. Os campos antigos `Ranking`, `Premiacao` e `TipoPremiacao` são derivados da lista (seção 5).
- Se `Premios` vier nulo, vale o comportamento atual (campos antigos), para não quebrar clientes antigos, e a lista é **regravada com um único prêmio de 1º lugar** a partir de `TipoPremiacao`/`Premiacao` (se o tipo estiver preenchido; sem tipo, a lista fica vazia). Assim um cliente antigo continua conseguindo publicar e escolher vencedor.
- A lista segue a mesma regra dos demais campos de regra no `UpdateAsync` atual: **só é aplicada na fase Rascunho**; depois de publicado, o `PUT` ignora `Premios` (e o front desativa a seção, como já faz com `isPublished`).
- A publicação exige pelo menos um prêmio (a regra atual de tipo/valor/quantos ganham passa a ser satisfeita pela lista).

`HackathonResponseDto` ganha `Premios` (lista de `{ posicao, tipo, valor, descricao }`), público como os demais dados do Hackathon, e `ResultadoPublicado` (bool).

### 6.2 Serviço

`IEvaluationService` / `EvaluationService(DampCodeDbContext, ICompanyAccessService, ISubmissionVersionStore)`, com `EvaluationServiceResult<T>` e `EvaluationServiceError { None, NotFound, Forbidden, Conflict, Validation }`, no mesmo formato dos serviços atuais. Controllers finos, erros em `ProblemDetails { Detail }`.

### 6.3 Rotas (em `HackathonsController`, todas `[Authorize]`)

| Método e rota | Quem | O que faz |
|---|---|---|
| `GET /api/Hackathons/{id}/avaliacao` | quem gerencia | Ranking: `podeAvaliar` (bool), `resultadoPublicado`, `premios`, e `entregas` em ordem de ranking: `submissionId`, `nome`, `linguagem`, `semelhanca` (int ou nulo), `nota`, `comentario`, `posicaoPremio`, `enviadoEm`. |
| `PUT /api/Hackathons/{id}/entregas/{submissionId}/avaliacao` | quem gerencia | `{ nota, comentario? }`. Grava nota e comentário. |
| `PUT /api/Hackathons/{id}/vencedores` | quem gerencia | `{ vencedores: [{ posicao, submissionId }] }`. Substitui a escolha toda de uma vez (posições existentes na lista de prêmios, sem repetir posição nem entrega). |
| `POST /api/Hackathons/{id}/resultado` | quem gerencia | Publica: calcula e grava `FinalPosition`, `Status = "Evaluated"`, `ResultsPublishedAt`; encerra o Hackathon se estiver em avaliação. Devolve o `HackathonResponseDto`. |

`GET /{id}/entregas/{submissionId}` (etapa 4) ganha `semelhanca`, `nota` e `comentario` no `SubmissionDetailDto` (campos novos no fim do record).

**Participante:** `MyParticipationDto` ganha `Resultado` (nulo até publicar): `{ posicao, totalClassificados, nota, comentario, premio: { posicao, tipo, valor, descricao } | null }`. Quem se inscreveu e não entregou recebe `Resultado` com `posicao` nula e a mensagem "Sem entrega" na tela.

### 6.4 Validações e mensagens

| Situação | Resposta |
|---|---|
| Avaliar/escolher vencedores antes do fim das submissões | 409 "A avaliação abre quando as submissões terminam." |
| Avaliar/escolher vencedores/publicar depois de publicado | 409 "O resultado já foi publicado." |
| Avaliar em Hackathon cancelado | 409 (mensagem atual de Hackathon finalizado) |
| Nota fora de 0–100 | 400 "A nota deve ficar entre 0 e 100." |
| Comentário > 1000 | 400 "O comentário deve ter no máximo 1000 caracteres." |
| Posição sem prêmio, posição ou entrega repetida | 400 "Escolha um vencedor diferente para cada prêmio." |
| Publicar com entrega sem nota | 409 "Dê nota a todas as entregas antes de publicar." |
| Prêmios inválidos na criação | 400 com a regra que falhou (ex.: "Informe o valor do prêmio do 2º lugar.") |
| Duas pessoas da empresa salvando vencedores ao mesmo tempo | último a salvar vale; conflito de índice único → 409 "Os vencedores mudaram. Recarregue a página." |
| Acesso | mesmas regras e mensagens da etapa 4 |

## 7. Front (`Damp-Code-Front`)

- **Formulário do Hackathon (`FormSection`):** a parte de premiação vira a lista "Prêmios por posição": linhas "1º lugar", "2º lugar"... com tipo, valor (se dinheiro) e descrição; botões "Adicionar posição" (até 10) e remover a última. Substitui na tela o seletor "Top N" e os campos de tipo/valor únicos (os campos continuam existindo no DTO). Edição carrega `premios` do Hackathon.
- **Cards e página do Hackathon:** onde hoje aparece o prêmio, mostrar o do 1º lugar e "+N prêmios" quando houver mais (a lista completa na página do Hackathon).
- **`SubmissionsReview.jsx` (a "mini IDE"):** abaixo do código, quando `podeAvaliar`: "Semelhança com a referência: 82%" (ou "Sem referência"), aviso de linguagem diferente, campo **Nota (0–100)** pré-preenchido com a semelhança se ainda não há nota, **Comentário**, botão "Salvar nota". A lista à esquerda mostra a nota ou "Sem nota" em cada entrega.
- **Página nova `Pages/company/EvaluationRanking.jsx`** (rota `/dashboard/Empresa/avaliacao/:id`): tabela do ranking (posição, nome, semelhança, nota, link "Ver código" para a tela de comparação daquela entrega) e, em cada linha, um seletor "Prêmio" com as posições premiadas ainda livres; "Salvar vencedores"; "Publicar resultado" (com as confirmações da seção 2). Depois de publicado, tudo só leitura com a classificação final.
- **`ManageChallenges`:** ação "Avaliação" nas fases Em avaliação e Encerrado (ao lado de "Entregas").
- **Participante (`MyParticipations`):** em cada inscrição com resultado publicado, um destaque: "Você ficou em 2º de 14 · Nota 87", o prêmio se houver ("🏆 2º lugar — R$ 500,00" / "Entrevista") e o comentário da empresa.
- **API simulada:** mesmas rotas e regras. Seed: o Hackathon 1 (iFood) ganha três prêmios; um Hackathon passa para a fase Em avaliação com algumas entregas (com e sem nota) para o roteiro da empresa, e outro com resultado já publicado para o roteiro do participante (Ana).

## 8. Testes

- `SimilarityScoreTests`: código idêntico → 100; renomear variáveis e mudar espaços/comentários → 100; código sem relação → baixo; vazio → 0; Python com `#` e SQL sem diferenciar maiúsculas.
- `EvaluationServiceTests` (EF InMemory + store em memória): avaliar antes do fim → 409; nota fora da faixa → 400; ranking ordenado com desempate e sem nota no fim; sugestão nula sem referência; vencedores fora da ordem da nota; posição/entrega repetida → 400; publicar com entrega sem nota → 409; publicar grava classificação final (premiados nas posições dos prêmios, demais a partir da próxima livre), encerra o Hackathon e trava alterações; Hackathon encerrado pelas datas sem resultado ainda pode publicar; empresa de outro Hackathon → 404; resultado aparece no `MyParticipationDto` só depois de publicar, igual para todos da equipe.
- `HackathonServiceTests`: lista de prêmios válida sincroniza `WinnersCount`/`RewardType`/`PrizeAmount`; posições não consecutivas, > 10, dinheiro sem valor → 400; `Premios` nulo mantém o comportamento antigo e regrava o prêmio de 1º lugar; prêmios ignorados no `PUT` depois de publicado.
- `EvaluationEndpointTests`: sem token → 401; participante chamando `/avaliacao` → 403; fluxo nota → vencedores → publicar.
- Front: lint sem problemas novos, build, roteiro no navegador com o mock (empresa avalia e publica; participante vê o resultado).

## 9. Deixado para o builder

- Layout exato das telas, seguindo os componentes atuais.
- Listas de palavras reservadas por linguagem (curtas, só as comuns).
- Como buscar os códigos do MongoDB para o ranking (um a um ou em lote no store).
- Divisão em commits.
