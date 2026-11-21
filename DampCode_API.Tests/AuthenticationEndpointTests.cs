using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using DampCode_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Swagger;

namespace DampCode_API.Tests;

public sealed class AuthenticationEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<AuthResponseDto> RegisterAsync(HttpClient client)
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var response = await client.PostAsJsonAsync("/api/Auth/register/participante", new
        {
            name = "Ana Souza",
            email,
            password = "SenhaForte123",
            tecnologias = new[] { "React" }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }

    private static HttpRequestMessage Get(string url, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task Users_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/Users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Users_WithToken_Returns200()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);

        var response = await client.SendAsync(Get("/api/Users", auth.Token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsPreviousFieldsAndToken()
    {
        var client = factory.CreateClient();
        var registered = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync("/api/Auth/login", new { email = registered.Email, password = "SenhaForte123" });
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(registered.Id, body!.Id);
        Assert.Equal("Ana Souza", body.Name);
        Assert.Equal("participante", body.Role);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.True(body.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Me_ReturnsAuthenticatedAccount()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);

        var response = await client.SendAsync(Get("/api/Auth/me", auth.Token));
        var me = await response.Content.ReadFromJsonAsync<UserResponseDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(auth.Id, me!.Id);
        Assert.Equal("participante", me.Role);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithTokenSignedByAnotherSecret_Returns401()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);
        var account = new Account { Id = auth.Id, Email = auth.Email, NormalizedEmail = auth.Email.ToUpperInvariant(), PasswordHash = "x" };
        var forged = new TokenService(TestJwt.Options(secret: "outro-segredo-qualquer-com-mais-de-32-bytes")).GenerateToken(account);

        var response = await client.SendAsync(Get("/api/Auth/me", forged.Token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithExpiredToken_Returns401()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);
        var issuedAt = DateTime.UtcNow.AddMinutes(-30);
        var expired = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "Api",
            Audience = "Front",
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = DateTime.UtcNow.AddMinutes(-10),
            SigningCredentials = new SigningCredentials(TestJwt.Options().Value.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object> { ["sub"] = auth.Id.ToString(), ["role"] = "Participant" }
        });

        var response = await client.SendAsync(Get("/api/Auth/me", expired));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_AfterAccountIsSuspended_Returns401()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DampCodeDbContext>();
            dbContext.Accounts.Single(account => account.Id == auth.Id).Status = AccountStatus.Suspended;
            await dbContext.SaveChangesAsync();
        }

        var response = await client.SendAsync(Get("/api/Auth/me", auth.Token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage Put(string url, string? token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task UpdateMe_ReturnsUpdatedAccount()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);

        var response = await client.SendAsync(Put("/api/Auth/me", auth.Token, new { name = "Ana Lima", avatarColor = "#C68FE6" }));
        var json = await response.Content.ReadAsStringAsync();
        var me = await client.SendAsync(Get("/api/Auth/me", auth.Token));
        var current = await me.Content.ReadFromJsonAsync<UserResponseDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"avatarColor\":\"#C68FE6\"", json);
        Assert.Contains("\"name\":\"Ana Lima\"", json);
        Assert.Equal("Ana Lima", current!.Name);
        Assert.Equal("#C68FE6", current.AvatarColor);
    }

    [Fact]
    public async Task UpdateMe_InvalidColor_Returns400WithDetail()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);

        var response = await client.SendAsync(Put("/api/Auth/me", auth.Token, new { name = (string?)null, avatarColor = "azul" }));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Cor inválida. Use o formato #RRGGBB.", problem!.Detail);
    }

    private const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    [Fact]
    public async Task UpdateMe_Image_IsReturnedByMeAndCanBeRemoved()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);

        var saved = await client.SendAsync(Put("/api/Auth/me", auth.Token, new { avatarImage = Png }));
        var savedJson = await saved.Content.ReadAsStringAsync();
        var afterSave = await (await client.SendAsync(Get("/api/Auth/me", auth.Token))).Content.ReadFromJsonAsync<UserResponseDto>();
        var removed = await client.SendAsync(Put("/api/Auth/me", auth.Token, new { avatarImage = "" }));
        var afterRemove = await (await client.SendAsync(Get("/api/Auth/me", auth.Token))).Content.ReadFromJsonAsync<UserResponseDto>();

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Contains("\"avatarImage\":\"data:image/png;base64,", savedJson);
        Assert.Equal(Png, afterSave!.AvatarImage);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Null(afterRemove!.AvatarImage);
    }

    [Theory]
    [InlineData("data:image/gif;base64,R0lGODlhAQABAAAAACw=")]
    [InlineData("too-long")]
    public async Task UpdateMe_InvalidImage_Returns400WithDetail(string image)
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);
        if (image == "too-long") image = "data:image/png;base64," + new string('A', 400_000);

        var response = await client.SendAsync(Put("/api/Auth/me", auth.Token, new { avatarImage = image }));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Foto inválida. Envie PNG, JPG ou WebP de até 300 KB.", problem!.Detail);
    }

    [Fact]
    public async Task UpdateMe_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().SendAsync(Put("/api/Auth/me", null, new { avatarColor = "#C68FE6" }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RevokesOldTokenAndSwapsLoginPassword()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);

        var response = await client.SendAsync(Put("/api/Auth/me/senha", auth.Token, new { senhaAtual = "SenhaForte123", novaSenha = "NovaSenhaForte456" }));
        var changed = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(auth.Id, changed!.Id);
        Assert.Equal("Ana Souza", changed.Name);
        Assert.NotEqual(auth.Token, changed.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Get("/api/Auth/me", auth.Token))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get("/api/Auth/me", changed.Token))).StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/Auth/login", new { email = auth.Email, password = "SenhaForte123" });
        var newLogin = await client.PostAsJsonAsync("/api/Auth/login", new { email = auth.Email, password = "NovaSenhaForte456" });
        var loggedIn = await newLogin.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get("/api/Auth/me", loggedIn!.Token))).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns400AndKeepsToken()
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client);

        var response = await client.SendAsync(Put("/api/Auth/me/senha", auth.Token, new { senhaAtual = "SenhaErrada123", novaSenha = "NovaSenhaForte456" }));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Senha atual incorreta.", problem!.Detail);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get("/api/Auth/me", auth.Token))).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().SendAsync(Put("/api/Auth/me/senha", null, new { senhaAtual = "SenhaForte123", novaSenha = "NovaSenhaForte456" }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Swagger_DeclaresBearerScheme()
    {
        var document = factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        Assert.True(document.Components.SecuritySchemes.TryGetValue("Bearer", out var scheme));
        Assert.Equal("bearer", scheme.Scheme);
        Assert.Equal("JWT", scheme.BearerFormat);
        Assert.Contains("ParticipanteDto", document.Components.Schemas.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("curta")]
    public void Startup_WithInvalidSecret_FailsWithClearMessage(string secret)
    {
        using var invalidFactory = new ApiFactory(secret);

        var exception = Assert.ThrowsAny<Exception>(() => invalidFactory.CreateClient());

        Assert.Contains("Jwt:Secret", exception.ToString());
    }
}
