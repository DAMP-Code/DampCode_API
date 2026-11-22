# Design — Módulos e conteúdos do Hackathon

## 1. Objetivo

A empresa organiza o material de apoio do Hackathon em **módulos** (ex.: "Semana 1 — Fundamentos") e, dentro de cada módulo, em **conteúdos**: textos, links (cursos, documentação, repositórios) e vídeos. Quem abre a página do Hackathon vê os módulos em ordem e consome os conteúdos ali mesmo.

Pedido do usuário em 2026-11-21: "a edição de hackathon para adicionar módulos e nos módulos, conteúdo, cursos e afins". O usuário pediu para seguir sem perguntas, então as regras da seção 2 são decisões de Claude, registradas para revisão.

### Restrições

- Mesmas das entregas anteriores: não renomear nada existente (classes, rotas, DTOs, campos de JSON), seguir o estilo atual da API e do front, mudanças aditivas.
- Identidade visual mantida no front.
- Base: `master` (API). Branch nova `feat/modulos-e-conteudos`. No front, a branch sai da pilha local `feat/verificacao-de-empresas`.

### Fora do escopo

- Progresso do participante (marcar conteúdo como visto, porcentagem do curso).
- Upload de arquivos (PDF, imagem): não existe Object Storage ainda. Arquivo entra como link.
- Conteúdo só para inscritos: todo conteúdo é visível para quem pode ver o Hackathon.
- Módulos na prévia do formulário de criação.

## 2. Regras de negócio (decisões de Claude)

1. **Módulo:** título (obrigatório, até 120 caracteres) e descrição (opcional, até 1.000). Até **20 módulos** por Hackathon.
2. **Conteúdo:** tipo `Texto`, `Link` ou `Video`; título (obrigatório, até 160).
   - `Texto`: campo `texto` obrigatório, até 20.000 caracteres; `url` é ignorada.
   - `Link` e `Video`: campo `url` obrigatório, até 2.048 caracteres, só `http://` ou `https://`; `texto` opcional (até 1.000) serve de descrição curta.
   - Até **30 conteúdos** por módulo.
3. **Quem edita:** quem gerencia a empresa dona do Hackathon (conta principal ou funcionário), a mesma checagem de `ICompanyAccessService` usada na edição do Hackathon.
4. **Quando edita:** em qualquer fase, **menos Encerrado e Cancelado** (mesmo depois de publicado, ao contrário de título e descrição, porque é material que cresce durante o desafio).
5. **Quem vê:** a mesma visibilidade de `GET /api/Hackathons/{id}`: publicado é público (inclusive visitante anônimo); rascunho e cancelado só quem gerencia a empresa.
6. **Ordem:** cada módulo tem `posicao` dentro do Hackathon e cada conteúdo tem `posicao` dentro do módulo, começando em 1. Item novo entra no fim. Apagar um item reordena os seguintes (sem buracos). Reordenar recebe a lista completa de ids na nova ordem; lista com ids faltando, sobrando ou repetidos é recusada (400).
7. Apagar um módulo apaga os conteúdos dele.

## 3. Modelo (PostgreSQL)

```text
hackathon_modules
  id uuid PK
  hackathon_id uuid FK -> hackathons (cascade)
  position int not null           -- único por hackathon
  title varchar(120) not null
  description varchar(1000) null
  created_at, updated_at timestamptz

hackathon_module_contents
  id uuid PK
  module_id uuid FK -> hackathon_modules (cascade)
  position int not null           -- único por módulo
  content_type varchar(10) not null   -- Text | Link | Video (enum gravado como texto)
  title varchar(160) not null
  body text null
  url varchar(2048) null
  created_at, updated_at timestamptz
```

Entidades `HackathonModule` e `HackathonModuleContent` (com `enum HackathonModuleContentType { Text, Link, Video }`), configurações em `Data/Configurations`, `Hackathon.Modules` como coleção nova. Os índices únicos de posição podem ser adiados se atrapalharem a reordenação em uma transação; nesse caso a regra fica só no serviço.

## 4. API

Todas as rotas ficam em `HackathonsController`, com nomes em português como as atuais. Saída em JSON com os nomes que o front lê:

```json
{
  "moduloId": "guid",
  "titulo": "Semana 1 — Fundamentos",
  "descricao": "…",
  "posicao": 1,
  "conteudos": [
    { "conteudoId": "guid", "tipo": "Video", "titulo": "Aula de abertura", "texto": null, "url": "https://youtu.be/…", "posicao": 1 }
  ]
}
```

O tipo sai como `Texto`, `Link` ou `Video` (o enum interno é `Text/Link/Video`; o mapeamento fica num helper, como `HackathonPhases`).

| Método | Rota | Corpo | Sucesso |
|---|---|---|---|
| GET | `/api/Hackathons/{id}/modulos` (anônimo permitido) | — | 200, lista de módulos com conteúdos, em ordem |
| POST | `/api/Hackathons/{id}/modulos` | `{ titulo, descricao }` | 201, módulo |
| PUT | `/api/Hackathons/{id}/modulos/{moduloId}` | `{ titulo, descricao }` | 200, módulo |
| DELETE | `/api/Hackathons/{id}/modulos/{moduloId}` | — | 204 |
| PUT | `/api/Hackathons/{id}/modulos/ordem` | `{ ids: [guid] }` | 200, lista de módulos |
| POST | `/api/Hackathons/{id}/modulos/{moduloId}/conteudos` | `{ tipo, titulo, texto, url }` | 201, conteúdo |
| PUT | `/api/Hackathons/{id}/modulos/{moduloId}/conteudos/{conteudoId}` | `{ tipo, titulo, texto, url }` | 200, conteúdo |
| DELETE | `/api/Hackathons/{id}/modulos/{moduloId}/conteudos/{conteudoId}` | — | 204 |
| PUT | `/api/Hackathons/{id}/modulos/{moduloId}/conteudos/ordem` | `{ ids: [guid] }` | 200, módulo |

Erros com `ProblemDetails.Detail` em português, como o resto da API:

- 401 sem token nas rotas de escrita;
- 404 Hackathon (ou módulo/conteúdo dele) não encontrado ou não visível para quem chama;
- 403 conta que não gerencia a empresa dona;
- 409 Hackathon encerrado ou cancelado ("Hackathon encerrado ou cancelado não aceita mudanças nos módulos."), ou limite de módulos/conteúdos atingido;
- 400 validação (campos, tipo desconhecido, URL inválida, lista de ordem inconsistente).

Serviço novo `IHackathonModuleService` / `HackathonModuleService` (registrado no `Program.cs`), com o mesmo padrão de resultado (`Succeeded`, `Error`, `Message`) dos outros serviços.

## 5. Testes (projeto `DampCode_API.Tests`)

- Serviço: criar, editar, apagar e reordenar módulos e conteúdos; posições sem buracos depois de apagar; limites de 20 e 30; validação de tipo, URL e campos; bloqueio em Encerrado e Cancelado; funcionário da empresa pode editar; conta de outra empresa recebe Forbidden; rascunho invisível para quem não gerencia.
- Endpoint: GET anônimo de Hackathon publicado; POST sem token dá 401; fluxo feliz de criar módulo e conteúdo com os nomes de JSON da seção 4.

## 6. Front

- **Mock** (`src/mock/installMockApi.js`): as mesmas rotas e regras, com módulos de exemplo no Hackathon 4 do seed.
- **Página nova** `/cadastroHackaton/:id/modulos` (só empresa, `RequireRole role="empresa"`): lista de módulos com editar, apagar e mover para cima/baixo; dentro de cada módulo, a lista de conteúdos com as mesmas ações e um formulário para adicionar (tipo, título, texto ou URL). Atalho "Módulos" no painel da empresa (`ManageChallenges`) e na tela de edição do Hackathon.
- **Página do Hackathon** (`HackathonView`): seção "Conteúdos" na coluna principal, com um bloco recolhível por módulo. Texto aparece direto; Link vira botão que abre em nova aba; Vídeo do YouTube aparece incorporado (`youtube-nocookie.com/embed`), outros vídeos viram link.
