using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DampCode_API.Tests;

public sealed class HackathonsEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string NewEmail() => $"{Guid.NewGuid():N}@exemplo.com";

    private static async Task<AuthResponseDto> RegisterParticipantAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/Auth/register/participante", new
        {
            name = "Maria Lima",
            email = NewEmail(),
            password = "SenhaForte123"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }

    private static async Task<AuthResponseDto> RegisterCompanyAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/Auth/register/empresa", new
        {
            name = "Empresa Exemplo",
            email = NewEmail(),
            password = "SenhaForte123",
            cnpj = string.Concat(Enumerable.Range(0, 14).Select(_ => Random.Shared.Next(10))),
            area = "Tecnologia",
            descricao = "Empresa de software de exemplo"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task UpdateDatabaseAsync(Action<DampCodeDbContext> change)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DampCodeDbContext>();
        change(dbContext);
        await dbContext.SaveChangesAsync();
    }

    private async Task<AuthResponseDto> RegisterVerifiedCompanyAsync(HttpClient client)
    {
        var company = await RegisterCompanyAsync(client);
        await UpdateDatabaseAsync(db => db.Companies.Single(item => item.PrimaryAccountId == company.Id)
            .VerificationStatus = CompanyVerificationStatus.Verified);
        return company;
    }

    private static object CompleteBody(string title = "Desafio de Rotas") => new
    {
        Titulo = title,
        Descricao = "Monte uma solução que reduza o tempo entre o pedido e a entrega.",
        Empresa = "Texto ignorado",
        Area = "web",
        Tecnologias = new[] { "React", "Node" },
        Metodo = "Avaliação Manual",
        Ranking = "Top 10",
        Premiacao = 5000,
        Formato = "Equipe",
        TipoPremiacao = "Dinheiro",
        corPrincipal = "#6C48C5",
        corSecundaria = "#C68FE6",
        corFundo = "#170A36",
        DataInicio = DateTime.UtcNow.AddDays(2),
        DataFinal = DateTime.UtcNow.AddDays(5),
        DataFimAvaliacao = DateTime.UtcNow.AddDays(8)
    };

    private static async Task<string> CreateAsync(HttpClient client, string token, string title = "Desafio de Rotas")
    {
        var response = await client.SendAsync(Request(HttpMethod.Post, "/api/Hackathons/register/Hackathon", token, CompleteBody(title)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HackathonResponseDto>())!.HackathonId;
    }

    private static async Task<HttpResponseMessage> PublishAsync(HttpClient client, string token, string id) =>
        await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/publicar", token));

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Hackathons/register/Hackathon", CompleteBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Participant_Returns403()
    {
        var client = factory.CreateClient();
        var participant = await RegisterParticipantAsync(client);

        var response = await client.SendAsync(Request(HttpMethod.Post, "/api/Hackathons/register/Hackathon", participant.Token, CompleteBody()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_Company_Returns201WithOldJsonNames()
    {
        var client = factory.CreateClient();
        var company = await RegisterCompanyAsync(client);

        var response = await client.SendAsync(Request(HttpMethod.Post, "/api/Hackathons/register/Hackathon", company.Token, CompleteBody()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(JsonValueKind.String, root.GetProperty("hackathonId").ValueKind);
        Assert.Equal("Desafio de Rotas", root.GetProperty("titulo").GetString());
        Assert.Equal("Empresa Exemplo", root.GetProperty("empresa").GetString());
        Assert.False(root.GetProperty("status").GetBoolean());
        Assert.Equal(JsonValueKind.String, root.GetProperty("dataFinal").ValueKind);
        Assert.Equal("Rascunho", root.GetProperty("fase").GetString());
        Assert.Equal("Top 10", root.GetProperty("ranking").GetString());
    }

    [Fact]
    public async Task GetAll_Anonymous_HidesDraftAndCancelled()
    {
        var client = factory.CreateClient();
        var company = await RegisterVerifiedCompanyAsync(client);
        var title = $"Publicado {Guid.NewGuid():N}";
        var published = await CreateAsync(client, company.Token, title);
        Assert.Equal(HttpStatusCode.OK, (await PublishAsync(client, company.Token, published)).StatusCode);
        var draft = await CreateAsync(client, company.Token, $"Rascunho {Guid.NewGuid():N}");
        var cancelled = await CreateAsync(client, company.Token, $"Cancelado {Guid.NewGuid():N}");
        await PublishAsync(client, company.Token, cancelled);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{cancelled}/cancelar", company.Token))).StatusCode);

        var hackathons = await factory.CreateClient().GetFromJsonAsync<List<HackathonResponseDto>>("/api/Hackathons");

        var ids = hackathons!.Select(item => item.HackathonId).ToList();
        Assert.Contains(published, ids);
        Assert.DoesNotContain(draft, ids);
        Assert.DoesNotContain(cancelled, ids);
        Assert.True(hackathons!.Single(item => item.HackathonId == published).Status);
    }

    [Fact]
    public async Task GetById_Draft_AnonymousIs404_CompanyIs200()
    {
        var client = factory.CreateClient();
        var company = await RegisterCompanyAsync(client);
        var draft = await CreateAsync(client, company.Token);

        var anonymous = await factory.CreateClient().GetAsync($"/api/Hackathons/{draft}");
        var owner = await client.SendAsync(Request(HttpMethod.Get, $"/api/Hackathons/{draft}", company.Token));

        Assert.Equal(HttpStatusCode.NotFound, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
    }

    [Fact]
    public async Task GetCompanyHackathons_ReturnsDrafts()
    {
        var client = factory.CreateClient();
        var company = await RegisterCompanyAsync(client);
        var draft = await CreateAsync(client, company.Token);

        var hackathons = await (await client.SendAsync(Request(HttpMethod.Get, "/api/Hackathons/empresa", company.Token)))
            .Content.ReadFromJsonAsync<List<HackathonResponseDto>>();

        Assert.Equal([draft], hackathons!.Select(item => item.HackathonId));
    }

    [Fact]
    public async Task Publish_UnverifiedCompany_Returns403WithDetail()
    {
        var client = factory.CreateClient();
        var company = await RegisterCompanyAsync(client);
        var draft = await CreateAsync(client, company.Token);

        var response = await PublishAsync(client, company.Token, draft);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Sua empresa precisa ser verificada para publicar.", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Delete_Published_Returns409()
    {
        var client = factory.CreateClient();
        var company = await RegisterVerifiedCompanyAsync(client);
        var id = await CreateAsync(client, company.Token, $"Apagar {Guid.NewGuid():N}");
        await PublishAsync(client, company.Token, id);

        var response = await client.SendAsync(Request(HttpMethod.Delete, $"/api/Hackathons/{id}", company.Token));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_Draft_Returns200()
    {
        var client = factory.CreateClient();
        var company = await RegisterCompanyAsync(client);
        var id = await CreateAsync(client, company.Token);

        var response = await client.SendAsync(Request(HttpMethod.Put, $"/api/Hackathons/{id}", company.Token, CompleteBody("Título editado")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Título editado", (await response.Content.ReadFromJsonAsync<HackathonResponseDto>())!.Titulo);
    }
}
