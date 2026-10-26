using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using DampCode_API.Services;
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
