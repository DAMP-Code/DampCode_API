# DampCode API

API ASP.NET Core 8 da DampCode (PostgreSQL via EF Core, autenticação JWT).

## Configuração local

Os segredos ficam em User Secrets, nunca no repositório. Na pasta `DampCode_API/`:

```bash
dotnet user-secrets set "ConnectionStrings:PostgreSql" "Host=localhost;Port=5432;Database=dampcode_dev;Username=postgres;Password=<sua-senha>"
dotnet user-secrets set "Jwt:Secret" "<texto aleatório com pelo menos 32 caracteres>"
```

Em produção use as variáveis `ConnectionStrings__PostgreSql` e `Jwt__Secret`.
Sem `Jwt:Secret` válido a API não inicia.

`Jwt:Issuer`, `Jwt:Audience` e `Jwt:ExpirationMinutes` (padrão 1440 = 24h) ficam em `appsettings.json`.

## Banco de dados

Com a connection string configurada (acima), na raiz do repositório:

```bash
dotnet tool restore
dotnet ef database update --project DampCode_API --startup-project DampCode_API
```

Para criar uma nova migration:

```bash
dotnet ef migrations add <Nome> --project DampCode_API --startup-project DampCode_API --output-dir Data/Migrations
```

> O `dotnet ef` inicializa a API para descobrir o `DbContext`, por isso precisa da connection string.
> Se a máquina só tiver runtimes mais novos que o .NET 8, prefixe os comandos `dotnet ef` com `DOTNET_ROLL_FORWARD=Major`
> (PowerShell: `$env:DOTNET_ROLL_FORWARD="Major"`).

## Testando a autenticação no Swagger

1. `dotnet run --project DampCode_API` e abra `/swagger`.
2. Chame `POST /api/Auth/login` (ou um dos cadastros) e copie o campo `token` da resposta.
3. Clique em **Authorize** e cole apenas o token (sem `Bearer `).
4. Chame `GET /api/Auth/me` ou `GET /api/Users`.

Tokens expiram em 24h e deixam de valer se a conta for suspensa, desativada, excluída ou tiver as credenciais alteradas.

## Conta Admin

Não existe cadastro de Admin pela API. Para verificar empresas (`GET /api/Companies/pending-verification` e
`PATCH /api/Companies/{id}/verification`), promova uma conta já cadastrada direto no banco, com o e-mail em maiúsculas:

```sql
UPDATE accounts SET account_type = 'Admin' WHERE normalized_email = 'ADMIN@EXEMPLO.COM';
```

A permissão vem do token: depois de promover, faça login de novo para receber um token com a role `Admin`.
Da mesma forma, uma conta rebaixada continua com acesso de Admin até o token atual expirar.

## Testes

O projeto de testes usa .NET 10 (SDK 10 necessário); a API continua em .NET 8.

```bash
dotnet test DampCode_API.Tests
```
