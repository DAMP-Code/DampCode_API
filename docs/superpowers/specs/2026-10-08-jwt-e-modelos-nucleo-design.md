# Design — JWT e revisão das models do núcleo (primeira entrega)

Data: 2026-10-08
Status: aprovado em conversa; aguardando revisão da spec escrita

## 1. Objetivo

Entregar a primeira etapa da reestruturação da DampCode API:

1. Models centrais alinhadas ao DER do núcleo (`dampcode-core-der.mmd`), sem problemas para as próximas entregas.
2. Autenticação JWT funcional: service dedicado, formato tradicional, expiração configurada, secret em User Secrets.
3. Controllers protegidos com `[Authorize]`.
4. Swagger com botão **Authorize** (Bearer).
5. Login (e cadastros) retornando o token para testes e uso pelo front.

### Restrições

- Não alterar nomes nem a estrutura existente da API: rotas, DTOs e contratos atuais permanecem; mudanças são apenas aditivas.
- Somente back-end.
- Não aumentar significativamente a complexidade do código; seguir os padrões atuais (controllers finos, services com regras, `IEntityTypeConfiguration` em `snake_case`, records de resultado).

### Fonte da verdade

- `dampcode-core-der.mmd` (núcleo: `accounts`, `user_profiles`, `companies`, `company_memberships`, `technologies`, `user_technologies`, `company_technologies`) é a referência desta entrega.
- O DER completo (imagem com ~25 tabelas) é referência apenas para entregas futuras.

### Fora do escopo

Convites empresariais, verificação de empresa, `system_roles`, troca de e-mail, refresh token/revogação, CRUD e lifecycle de Hackathon, equipes, participações, submissões, MongoDB e Object Storage.

## 2. Estado atual relevante

- As 7 configurações EF do núcleo já correspondem ao `.mmd` coluna a coluna; não serão remodeladas.
- `TokenService` existe, não é registrado nem usado.
- `Program.cs` não chama `UseAuthentication()` e não há esquema JWT registrado.
- `UsersController` devolve `"participante"` para contas `Admin` (bug) e duplica a projeção de `UserResponseDto`.
- `StudentData`, `WeatherForecast` e `WeatherForecastController` são legados/template e não são referenciados.
- Nenhuma migration existe.

## 3. Núcleo JWT

### 3.1 `ITokenService` / `TokenService`

- Mantém o nome `TokenService`; ganha a interface `ITokenService`.
- Assinatura única: `TokenResult GenerateToken(Account account)`, onde `TokenResult(string Token, DateTime ExpiresAt)` (UTC).
- O método antigo `generateToken(Account, DateTime)` é removido; a expiração vem da configuração.
- Algoritmo: HS256.
- Claims:
  - `sub` — ID da conta;
  - `email` — e-mail da conta;
  - `role` — nome do `AccountType` (`Participant`, `Company`, `Admin`);
  - `jti` — GUID novo por token;
  - `iat` — instante de emissão (Unix seconds).
- Não incluir nome, empresa ou permissões mutáveis.

### 3.2 Configuração (`JwtOptions`, seção `Jwt`)

| Chave | Local | Valor |
|---|---|---|
| `Jwt:Issuer` | `appsettings.json` | `Api` |
| `Jwt:Audience` | `appsettings.json` | `Front` |
| `Jwt:ExpirationMinutes` | `appsettings.json` | `1440` (24h) |
| `Jwt:Secret` | User Secrets (dev) / `Jwt__Secret` (produção) | ≥ 32 bytes UTF-8; nunca versionado |

- Validação na inicialização (`ValidateOnStart`): Issuer, Audience e Secret obrigatórios; Secret com pelo menos 32 bytes; `ExpirationMinutes` > 0. Falha impede a API de subir com mensagem clara.

### 3.3 Validação do token

- Pacote: `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.x (alinhado ao .NET 8 do projeto).
- `TokenValidationParameters`: valida assinatura, issuer, audience e lifetime; `ClockSkew = 1 minuto`; `NameClaimType = "sub"`, `RoleClaimType = "role"`.
- `MapInboundClaims = false`.
- `OnTokenValidated` delega para `AccountTokenValidator`, que carrega a conta pelo `sub` e rejeita (`context.Fail`) quando:
  - a conta não existe ou `DeletedAt` não é nulo;
  - `Status != Active`;
  - `CredentialsChangedAt` (truncado em segundos) é posterior ao `iat` do token.
- Respostas: token ausente, inválido, expirado ou rejeitado → 401; autenticado sem permissão → 403.

## 4. Controllers, DTOs e Swagger

### 4.1 `AuthResponseDto` (aditivo)

```json
{
  "id": "guid",
  "name": "Nome",
  "email": "email@exemplo.com",
  "role": "participante",
  "token": "eyJhbGciOi...",
  "expiresAt": "2026-10-09T20:00:00Z"
}
```

- Retornado por `POST /api/Auth/login`, `POST /api/Auth/register/participante` e `POST /api/Auth/register/empresa`.
- `AuthService` recebe `ITokenService` e gera o token no resultado de sucesso.

### 4.2 `AccountRoles`

- Helper estático `AccountRoles.ToFrontRole(AccountType)`: `Participant → "participante"`, `Company → "empresa"`, `Admin → "admin"`.
- Usado por `AuthService` e `UsersController` (corrige o bug do `Admin`).

### 4.3 `AuthController`

- `[AllowAnonymous]` nas rotas existentes (inalteradas).
- Nova rota `GET /api/Auth/me` com `[Authorize]`: lê o `sub` e retorna `UserResponseDto` da conta autenticada; 404 se a conta não for encontrada.

### 4.4 `UsersController`

- `[Authorize]` na classe: `GET /api/Users` e `GET /api/Users/{id}` exigem token.
- Projeção para `UserResponseDto` unificada e compartilhada com `GET /api/Auth/me`.
- Observação futura (não implementada agora): `GET /api/Users` expõe e-mails a qualquer usuário autenticado; avaliar restringir a `Admin` ou omitir e-mail.

### 4.5 `HackathonsController`

Sem alteração (continua retornando 501).

### 4.6 `Program.cs`

- Registrar `JwtOptions`, `ITokenService`, `AccountTokenValidator` e `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)`.
- Pipeline: `UseCors` → `UseAuthentication` → `UseAuthorization` → `MapControllers`.

### 4.7 Swagger

- `AddSecurityDefinition("Bearer", ...)` do tipo HTTP, scheme `bearer`, `BearerFormat = "JWT"`, com requisito de segurança global.
- No botão Authorize cola-se apenas o token (sem o prefixo `Bearer `).
- `CustomSchemaIds` existentes (`ParticipanteDto`, `EmpresaDto`, `CreateHackathonDTO`) preservados.

## 5. Models, limpeza e migration

### 5.1 Models fora do DbContext

- `Hackathon`:
  - `ParticipationMode` → enum `HackathonParticipationMode { Individual, Team }`;
  - `Status` → enum `HackathonStatus { Draft, Scheduled, Active, InReview, Closed, Cancelled }`;
  - `EvaluationMethod` permanece `string?` (valores ainda não definidos);
  - demais campos inalterados; continua fora do DbContext.
- `Submission`: inalterada; continua fora do DbContext.

### 5.2 Remoções

- `Models/StudentData.cs`
- `WeatherForecast.cs`
- `Controllers/WeatherForecastController.cs`

### 5.3 Migration inicial

- `DampCodeDbContextFactory : IDesignTimeDbContextFactory<DampCodeDbContext>`, usada apenas pelo `dotnet ef`, com connection string local de exemplo sem senha real (ou `ConnectionStrings__PostgreSql` se definida).
- Migration `InitialCore` com as 7 tabelas do núcleo.
- Revisão manual do SQL (`dotnet ef migrations script`): PKs, FKs, check constraints, índices únicos e índice parcial `ux_company_memberships_active_account`.
- A migration não é aplicada a nenhum banco nesta entrega.

## 6. Testes

Projeto `DampCode_API.Tests` (xUnit, EF Core InMemory, `Microsoft.AspNetCore.Mvc.Testing`), adicionado à solution.

- `TokenService`: claims `sub`, `email`, `role`, `jti`, `iat`; expiração respeita `ExpirationMinutes`; secret curto é rejeitado.
- `AccountTokenValidator`: aceita conta ativa; rejeita deletada, suspensa e token anterior a `CredentialsChangedAt`.
- `AuthService`: cadastro retorna token; e-mail duplicado (case-insensitive) → conflito; CNPJ duplicado → conflito; senha errada → credenciais inválidas; conta inativa → bloqueada; resposta nunca contém hash.
- `AccountRoles`: `Admin → "admin"`.
- Integração: `GET /api/Users` sem token → 401, com token → 200; `GET /api/Auth/me` retorna a conta do token.

Limitação: o provider InMemory ignora transações, check constraints e índices únicos; essas garantias dependem do PostgreSQL após aplicar a migration. Os avisos de transação ignorada são suprimidos apenas nos testes.

## 7. Documentação

README com:

- configuração de `Jwt:Secret` e `ConnectionStrings:PostgreSql` via `dotnet user-secrets`;
- geração e aplicação da migration;
- fluxo de teste no Swagger (login → copiar `token` → Authorize → `/api/Auth/me`).

## 8. Critérios de aceite

- `dotnet build` sem erros; `dotnet test` verde.
- Login e cadastros retornam `token` e `expiresAt`; campos anteriores inalterados.
- `/api/Users` e `/api/Auth/me` retornam 401 sem token e funcionam com token válido.
- Conta suspensa, deletada ou com credenciais alteradas após a emissão tem o token rejeitado.
- Swagger exibe o botão Authorize e envia o header `Authorization: Bearer`.
- Nenhum secret ou senha versionado.
- Migration `InitialCore` gerada e revisada.
