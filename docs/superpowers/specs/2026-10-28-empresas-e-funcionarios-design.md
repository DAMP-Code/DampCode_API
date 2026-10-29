# Design — Empresas, funcionários e verificação (segunda entrega)

## 1. Objetivo

Primeira de seis etapas combinadas (empresas e funcionários → ciclo de vida do Hackathon → inscrição e equipes → submissões → avaliação e premiação → perfil e dashboards). Esta etapa entrega:

- convite, aceite/recusa, revogação e remoção de funcionários de uma empresa;
- verificação da empresa por um administrador;
- uma regra única de "quem pode gerenciar Hackathons de qual empresa", que a etapa 2 vai consumir;
- telas mínimas no front para usar tudo isso.

### Restrições

- **Não renomear nada que já existe**: classes, métodos, rotas, DTOs, colunas e componentes ficam como estão. Mudanças são aditivas.
- **Seguir o estilo atual do código**, para que o novo pareça escrito pelo time:
  - API: controllers finos com construtor primário, regras em services com interface (`IXService`/`XService`), resultado em `record` (`XServiceResult` + enum de erro, como `AuthServiceResult`), `IEntityTypeConfiguration` com tabelas/colunas em `snake_case`, nomes de constraint `pk_`/`fk_`/`ux_`/`ck_`, mensagens de erro em português, `ProblemDetails` nos erros.
  - Testes: xUnit com `ApiFactory`/`TestDb`, nomes `Metodo_Cenario_Resultado`.
  - Front: React com `fetch` direto para `https://localhost:7092`, comentários em português no mesmo tom dos arquivos existentes, Tailwind, componentes em `src/app-components/<area>/`.
- PostgreSQL continua sendo o banco de contas e empresas. Nada desta etapa vai para o MongoDB.

### Fora do escopo

- Envio de e-mail (não existe serviço de e-mail; o convite aparece dentro da plataforma).
- Tela de administrador no front (a verificação é feita pela API/Swagger).
- Mudanças em Hackathons (etapa 2).
- Cadastro de conta Admin pela API.

## 2. Estado atual relevante

- `Company` tem `PrimaryAccountId` (único) e `VerificationStatus` (`Pending`, `Verified`, `Rejected`, padrão `Pending`).
- `CompanyMembership` existe com `JobTitle`, `StartedAt`, `EndedAt` e índice único parcial `ux_company_memberships_active_account` (`ended_at IS NULL`): uma pessoa só tem um vínculo ativo por vez.
- Nenhum código cria `CompanyMembership` hoje. A conta principal da empresa **não** tem membership; ela é ligada só por `PrimaryAccountId`.
- `AccountType` tem `Admin`, mas nenhuma rota usa.
- O front guarda a resposta do login em `localStorage["user"]` (inclui `token`), mas nenhuma chamada envia `Authorization` ainda.
- O front roda por padrão com a API simulada (`VITE_USE_MOCK=true`, `src/mock/installMockApi.js`).

## 3. Regras de negócio (decididas com o time)

1. **Funcionário é uma pessoa com conta de participante** (`AccountType.Participant`). Ele não cria outra conta. Contas `Company` e `Admin` não podem ser convidadas.
2. **Só a conta principal da empresa** convida, revoga convites e remove funcionários.
3. **O convite é feito pelo e-mail da pessoa** e aparece dentro da plataforma para a conta com esse e-mail. **Só é possível convidar um e-mail que já pertence a uma conta de participante.** Motivo: o cadastro não confirma a posse do e-mail, então um convite "esperando cadastro" poderia ser aceito por quem registrasse aquele e-mail primeiro e ganhar acesso aos Hackathons da empresa.
4. **O convite expira 24 horas depois de criado.** Convite expirado não pode ser aceito.
5. **Aceitar o convite cria um `CompanyMembership` ativo.** Remover o funcionário preenche `EndedAt`; o registro fica como histórico (a etapa 6 usa isso em "experiência profissional").
6. **Uma pessoa só pode estar em uma empresa por vez** (já garantido pelo banco).
7. **Conta principal e funcionários ativos podem gerenciar os Hackathons da empresa.**
8. **Empresa `Pending` ou `Rejected` pode salvar rascunho, mas só empresa `Verified` publica.** Nesta etapa a regra é exposta pelo serviço de acesso (seção 6); quem a aplica nos Hackathons é a etapa 2.
9. **Verificação é manual, feita por uma conta `Admin`**, que marca a empresa como `Verified` ou `Rejected`.

## 4. Modelo de dados

### 4.1 Nova entidade `CompanyInvitation` (`Models/CompanyInvitation.cs`)

| Propriedade | Tipo | Coluna | Observação |
|---|---|---|---|
| `Id` | `Guid` | `id` | `Guid.NewGuid()`, `ValueGeneratedNever` |
| `CompanyId` | `Guid` | `company_id` | FK `fk_company_invitations_companies`, `Restrict` |
| `InvitedByAccountId` | `Guid` | `invited_by_account_id` | FK `fk_company_invitations_accounts`, `Restrict` |
| `Email` | `string` (254) | `email` | como digitado, com `Trim` |
| `NormalizedEmail` | `string` (254) | `normalized_email` | mesmo `Normalize` do `AuthService` (`Trim().ToUpperInvariant()`) |
| `JobTitle` | `string?` (150) | `job_title` | copiado para o membership no aceite |
| `Status` | `CompanyInvitationStatus` | `status` | string, max 32 |
| `ExpiresAt` | `DateTime` | `expires_at` | `CreatedAt + 24h` |
| `RespondedAt` | `DateTime?` | `responded_at` | aceite, recusa ou revogação |
| `CreatedAt` / `UpdatedAt` | `DateTime` | `created_at` / `updated_at` | padrão das outras entidades |

`public enum CompanyInvitationStatus { Pending, Accepted, Declined, Revoked }`

- Expiração **não** é um status gravado: convite `Pending` com `ExpiresAt <= agora` é tratado como expirado em todas as consultas e ações. Assim não é preciso job agendado.
- Configuração `CompanyInvitationConfiguration`, tabela `company_invitations`:
  - `ck_company_invitations_status`: `status IN ('Pending', 'Accepted', 'Declined', 'Revoked')`;
  - índice `ix_company_invitations_normalized_email` em `normalized_email`;
  - índice `ix_company_invitations_company_id` em `company_id`.
  - Sem índice único parcial para "um convite pendente por e-mail e empresa", porque um convite expirado continua `Pending` no banco. A regra fica no service (seção 5.1).
- Navegações: `Company.Invitations` (nova coleção em `Company`), `CompanyInvitation.Company`, `CompanyInvitation.InvitedByAccount`. Não adicionar coleção em `Account`.
- `DbSet<CompanyInvitation> CompanyInvitations` no `DampCodeDbContext`.
- Migration nova: `CompanyInvitations`.

### 4.2 Sem mudanças em tabelas existentes

`companies`, `company_memberships` e `accounts` não mudam de colunas.

## 5. API

Novo `CompaniesController` (`[Authorize]`, `api/[controller]`) e novo `ICompanyService`/`CompanyService`, com o mesmo formato de resultado do `AuthService`:

```csharp
public sealed record CompanyServiceResult<T>(bool Succeeded, T? Value = default, CompanyServiceError Error = CompanyServiceError.None, string? Message = null);
public enum CompanyServiceError { None, NotFound, Forbidden, Conflict, Expired, Validation }
```

Mapeamento no controller: `NotFound` → 404, `Forbidden` → 403, `Conflict` → 409, `Expired` → 410, `Validation` → 400. Todos com `ProblemDetails { Detail = Message }`.

O ID da conta vem do `sub`, como em `AuthController.Me`.

### 5.1 Rotas

| Método e rota | Quem pode | O que faz |
|---|---|---|
| `GET /api/Companies/me` | qualquer autenticado | Empresa ligada à conta: `CompanyAccessDto` (seção 5.2). 404 se não houver vínculo. |
| `GET /api/Companies/me/employees` | conta principal ou funcionário ativo | Funcionários ativos: `CompanyEmployeeDto(Guid AccountId, string Name, string Email, string? JobTitle, DateTime StartedAt)`. |
| `GET /api/Companies/me/invitations` | conta principal | Convites `Pending` não expirados que a empresa enviou: `CompanyInvitationDto`. |
| `POST /api/Companies/me/invitations` | conta principal | Corpo `InviteEmployeeDto { Email, JobTitle? }`. Cria o convite. 201 com `CompanyInvitationDto`. |
| `DELETE /api/Companies/me/invitations/{id:guid}` | conta principal | Revoga um convite pendente (`Revoked`). 204. |
| `DELETE /api/Companies/me/employees/{accountId:guid}` | conta principal | Remove o funcionário (`EndedAt = agora`). 204. |
| `GET /api/Companies/invitations/received` | participante | Convites `Pending` não expirados para o e-mail da conta: `ReceivedInvitationDto(Guid Id, string CompanyName, string? JobTitle, DateTime ExpiresAt)`. |
| `POST /api/Companies/invitations/{id:guid}/accept` | participante dono do e-mail | Aceita: cria `CompanyMembership`, marca `Accepted`. 200 com `CompanyAccessDto`. |
| `POST /api/Companies/invitations/{id:guid}/decline` | participante dono do e-mail | Recusa (`Declined`). 204. |
| `GET /api/Companies/pending-verification` | `[Authorize(Roles = "Admin")]` | Empresas `Pending`: `CompanyVerificationDto(Guid Id, string Name, string Cnpj, string? Area, DateTime CreatedAt)`. |
| `PATCH /api/Companies/{id:guid}/verification` | `[Authorize(Roles = "Admin")]` | Corpo `UpdateVerificationDto { Status }` com `Verified` ou `Rejected`. 204. |

`CompanyInvitationDto(Guid Id, string Email, string? JobTitle, DateTime ExpiresAt, DateTime CreatedAt)`.

`Roles = "Admin"` funciona porque a claim `role` já carrega o nome do `AccountType` e o `RoleClaimType` é `"role"`.

### 5.2 `CompanyAccessDto`

`CompanyAccessDto(Guid CompanyId, string Name, string VerificationStatus, bool IsPrimaryAccount, bool CanPublish)`.

O front usa isso para decidir o que mostrar. Por isso `AuthResponseDto` e `UserResponseDto` **não** mudam.

### 5.3 Validações e erros

Convidar (`POST /me/invitations`):
- Conta não é a principal de uma empresa → 403 "Apenas a conta principal da empresa pode convidar funcionários."
- E-mail inválido → 400 (DataAnnotations: `[Required, EmailAddress, StringLength(254)]`; `JobTitle` `[StringLength(150)]`).
- E-mail é o da própria conta principal → 400 "Você não pode convidar a própria conta."
- Não existe conta ativa (`DeletedAt == null`) com esse e-mail → 404 "Nenhum participante encontrado com este e-mail. Peça para a pessoa criar a conta primeiro."
- Já existe conta com esse e-mail e ela não é `Participant` → 409 "Este e-mail pertence a uma conta que não pode ser funcionária."
- A pessoa já é funcionária ativa **desta** empresa → 409 "Esta pessoa já é funcionária da empresa."
- Já existe convite `Pending` e não expirado desta empresa para o e-mail → 409 "Já existe um convite pendente para este e-mail."
- Se a pessoa está ativa em **outra** empresa, o convite é criado mesmo assim; o bloqueio acontece no aceite.

Aceitar:
- Convite não existe, ou o e-mail não é o da conta → 404 "Convite não encontrado." (não revela convites de outras pessoas).
- Conta não é `Participant` → 403.
- Status diferente de `Pending` → 409 "Este convite já foi respondido."
- Expirado → 410 "Este convite expirou."
- Já tem vínculo ativo com qualquer empresa → 409 "Você já está vinculado a uma empresa."
- Sucesso: em transação, cria `CompanyMembership { CompanyId, AccountId, JobTitle, StartedAt = agora }`, marca o convite `Accepted` e `RespondedAt`.

Recusar: mesmas checagens de existência, status e expiração; marca `Declined`.

Revogar: convite precisa ser da empresa da conta principal e estar `Pending` → senão 404 / 409.

Remover funcionário: precisa ter membership ativo nesta empresa → senão 404.

Verificação: empresa inexistente → 404; status diferente de `Verified`/`Rejected` → 400. Não há transição proibida: o admin pode mudar de `Rejected` para `Verified` e vice-versa.

## 6. Regra de acesso para Hackathons (`ICompanyAccessService`)

Serviço pequeno, separado do `CompanyService`, para a etapa 2 usar sem depender do resto:

```csharp
public interface ICompanyAccessService
{
    // Empresa que a conta pode gerenciar: a própria (conta principal) ou a do vínculo ativo.
    Task<CompanyAccessDto?> GetManagedCompanyAsync(Guid accountId, CancellationToken cancellationToken);
}
```

- Conta `Company` → empresa onde `PrimaryAccountId == accountId` (`IsPrimaryAccount = true`).
- Conta `Participant` → empresa do `CompanyMembership` com `EndedAt == null` (`IsPrimaryAccount = false`).
- Empresa com `DeletedAt != null` → `null`.
- `CanPublish = VerificationStatus == Verified`.
- `GET /api/Companies/me` usa este mesmo serviço.

## 7. Conta Admin

Não há rota para criar Admin. O README ganha uma seção curta explicando como promover uma conta já cadastrada:

```sql
UPDATE accounts SET account_type = 'Admin' WHERE normalized_email = 'ADMIN@EXEMPLO.COM';
```

O builder deve conferir a constraint/conversão real de `account_type` na `AccountConfiguration` antes de escrever o SQL final.

## 8. Front (`Damp-Code-Front`)

### 8.1 Token nas chamadas

As novas chamadas enviam `Authorization: Bearer ${user.token}` lendo o `user` do `AuthContext`. Não mexer nas chamadas existentes nesta etapa.

### 8.2 Dashboard da empresa (`/dashboard/Empresa`)

- Ao abrir, chama `GET /api/Companies/me`.
- Se `verificationStatus !== "Verified"`, mostra um aviso no topo: "Sua empresa ainda não foi verificada. Você pode criar rascunhos, mas só poderá publicar depois da verificação."
- Nova seção **Funcionários** (`src/app-components/company-dashboard/ManageEmployees.jsx`), abaixo de `ManageChallenges`:
  - lista de funcionários ativos (nome, e-mail, cargo, desde quando);
  - se `isPrimaryAccount`: formulário com e-mail e cargo para convidar, lista de convites pendentes com o tempo restante e botão para revogar, e botão "Remover" em cada funcionário com confirmação.

### 8.3 Dashboard do participante (`/dashboard/participante`)

- Nova caixa **Convites** (`src/app-components/participant-dashboard/ReceivedInvitations.jsx`) com os convites recebidos, nome da empresa, cargo, tempo restante e botões Aceitar/Recusar. Fica oculta quando não houver convites.
- Se `GET /api/Companies/me` retornar um vínculo, mostra um botão "Painel da empresa" que leva a `/dashboard/Empresa`.

### 8.4 Login

`UserLogin.jsx` continua mandando participante para `/dashboard/participante`. O funcionário chega ao painel da empresa pelo botão da seção 8.3.

### 8.5 API simulada

`src/mock/installMockApi.js` e `seed.js` ganham as rotas da seção 5.1 (exceto as de Admin), com os mesmos formatos de resposta. Assim o front continua funcionando com `VITE_USE_MOCK=true`.

## 9. Testes

No mesmo padrão de `DampCode_API.Tests`:

- `CompanyServiceTests` (unitário com `TestDb.Create()`): convidar, convidar duplicado, convidar e-mail sem conta, convidar conta Company, aceitar, aceitar expirado (manipulando `ExpiresAt`), aceitar já vinculado, recusar, revogar, remover.
- `CompanyAccessServiceTests`: conta principal, funcionário ativo, funcionário removido, `CanPublish` por status.
- `CompaniesEndpointTests` (integração com `ApiFactory`): 401 sem token, 403 de participante tentando convidar, 403 de não-admin na verificação, fluxo completo convite → aceite → listagem de funcionários.

Observação: InMemory não aplica o índice único de membership ativo, então a regra "um vínculo por vez" precisa ser testada pela checagem do service.

## 10. Decisões tomadas por Claude (para o time revisar)

- Expiração calculada por `ExpiresAt`, sem status `Expired` gravado e sem job.
- Convite para quem já está em outra empresa pode ser criado; o bloqueio é no aceite.
- Recusa e revogação são estados finais; para tentar de novo, a empresa envia um convite novo.
- Funcionário não tem rota para sair da empresa sozinho nesta etapa; só a conta principal remove.
- Erro 410 para convite expirado.
- Verificação sem transições proibidas.
- `CompanyAccessDto` em rota própria, em vez de mudar `AuthResponseDto`/`UserResponseDto`.
- Mock do front atualizado junto.
- Admin é decidido pela claim `role` do token (`[Authorize(Roles = "Admin")]`). Consequência aceita: quem for promovido pelo SQL precisa fazer login de novo, e quem for rebaixado mantém o acesso até o token expirar. O README avisa isso.
- No `/dashboard/Empresa`, quando `isPrimaryAccount` for `false`, as seções `CreateChallenge` e `ManageChallenges` ficam ocultas nesta etapa, porque elas identificam a empresa por `user.name`. O funcionário vê o aviso de verificação e a seção Funcionários (só leitura). A etapa 2 reabre essas seções já usando a empresa do `CompanyAccessDto`.

## 11. Deixado para o builder

- Nomes exatos de arquivos de DTO (seguir `Dto/` com um arquivo por DTO, como hoje).
- Layout visual das novas seções, seguindo o estilo das seções vizinhas.
- Divisão em commits.
