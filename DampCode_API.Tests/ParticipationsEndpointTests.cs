using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DampCode_API.Tests;

public sealed class ParticipationsEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string NewEmail() => $"{Guid.NewGuid():N}@exemplo.com";

    private static async Task<AuthResponseDto> RegisterParticipantAsync(HttpClient client, string? email = null, string name = "Maria Lima")
    {
        var response = await client.PostAsJsonAsync("/api/Auth/register/participante", new
        {
            name,
            email = email ?? NewEmail(),
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

    // Empresa verificada cria e publica um Hackathon no formato pedido.
    private async Task<(AuthResponseDto Company, string HackathonId)> PublishedHackathonAsync(HttpClient client, string formato)
    {
        var company = await RegisterCompanyAsync(client);
        await UpdateDatabaseAsync(db => db.Companies.Single(item => item.PrimaryAccountId == company.Id)
            .VerificationStatus = CompanyVerificationStatus.Verified);
        var created = await client.SendAsync(Request(HttpMethod.Post, "/api/Hackathons/register/Hackathon", company.Token, new
        {
            Titulo = $"Desafio {Guid.NewGuid():N}",
            Descricao = "Monte uma solução que reduza o tempo entre o pedido e a entrega.",
            Area = "web",
            Tecnologias = new[] { "React" },
            Metodo = "Avaliação Manual",
            Ranking = "Top 10",
            Premiacao = 5000,
            Formato = formato,
            TipoPremiacao = "Dinheiro",
            DataInicio = DateTime.UtcNow.AddDays(2),
            DataFinal = DateTime.UtcNow.AddDays(5),
            DataFimAvaliacao = DateTime.UtcNow.AddDays(8)
        }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<HackathonResponseDto>())!.HackathonId;
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/publicar", company.Token))).StatusCode);
        return (company, id);
    }

    [Fact]
    public async Task Register_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();
        var (_, id) = await PublishedHackathonAsync(client, "Individual");

        var response = await factory.CreateClient().PostAsync($"/api/Hackathons/{id}/inscricao", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_Company_Returns403()
    {
        var client = factory.CreateClient();
        var (_, id) = await PublishedHackathonAsync(client, "Individual");
        var other = await RegisterCompanyAsync(client);

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/inscricao", other.Token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OpenTeams_Anonymous_Returns200()
    {
        var client = factory.CreateClient();
        var (_, id) = await PublishedHackathonAsync(client, "Equipe");
        var ana = await RegisterParticipantAsync(client);
        var created = await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/equipes", ana.Token, new { nome = "Os Rápidos", aberta = true }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var teams = await factory.CreateClient().GetFromJsonAsync<List<OpenTeamDto>>($"/api/Hackathons/{id}/equipes");

        var team = Assert.Single(teams!);
        Assert.Equal("Os Rápidos", team.Nome);
        Assert.Equal(4, team.Vagas);
    }

    [Fact]
    public async Task InviteFlow_ParticipationsMeShowsTeam()
    {
        var client = factory.CreateClient();
        var (_, id) = await PublishedHackathonAsync(client, "Equipe");
        var ana = await RegisterParticipantAsync(client, name: "Ana Souza");
        var brunoEmail = NewEmail();
        var bruno = await RegisterParticipantAsync(client, brunoEmail, "Bruno Lima");
        var team = await (await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/equipes", ana.Token, new { nome = "Os Rápidos" })))
            .Content.ReadFromJsonAsync<MyTeamDto>();
        var invite = await client.SendAsync(Request(HttpMethod.Post, $"/api/Teams/{team!.TeamId}/convites", ana.Token, new { email = brunoEmail }));
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);

        var brunoView = await (await client.SendAsync(Request(HttpMethod.Get, "/api/Participations/me", bruno.Token)))
            .Content.ReadFromJsonAsync<ParticipationsOverviewDto>();
        var received = Assert.Single(brunoView!.ConvitesRecebidos);
        var accepted = await client.SendAsync(Request(HttpMethod.Post, $"/api/Teams/convites/{received.Id}/aceitar", bruno.Token));
        var anaView = await (await client.SendAsync(Request(HttpMethod.Get, "/api/Participations/me", ana.Token)))
            .Content.ReadFromJsonAsync<ParticipationsOverviewDto>();

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(["Ana Souza", "Bruno Lima"], Assert.Single(anaView!.Inscricoes).Equipe!.Membros.Select(item => item.Nome));
    }

    [Fact]
    public async Task Participants_CountAppearsInHackathonJson()
    {
        var client = factory.CreateClient();
        var (_, id) = await PublishedHackathonAsync(client, "Individual");
        var ana = await RegisterParticipantAsync(client);

        var registered = await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/inscricao", ana.Token));
        var hackathon = await factory.CreateClient().GetFromJsonAsync<HackathonResponseDto>($"/api/Hackathons/{id}");

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        Assert.Equal("1 pessoa já entrou", hackathon!.Participantes);
    }

    [Fact]
    public async Task LeaveTeam_Returns204()
    {
        var client = factory.CreateClient();
        var (_, id) = await PublishedHackathonAsync(client, "Equipe");
        var ana = await RegisterParticipantAsync(client);
        var team = await (await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/equipes", ana.Token, new { nome = "Sozinha" })))
            .Content.ReadFromJsonAsync<MyTeamDto>();

        var response = await client.SendAsync(Request(HttpMethod.Delete, $"/api/Teams/{team!.TeamId}/membros/me", ana.Token));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
