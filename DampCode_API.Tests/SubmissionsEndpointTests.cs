using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DampCode_API.Tests;

public sealed class SubmissionsEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    // Empresa verificada publica um Hackathon individual com solução de referência, uma participante se inscreve
    // e o início é movido para o passado (fase Ativo).
    private async Task<(AuthResponseDto Company, AuthResponseDto Participant, string HackathonId)> ActiveHackathonAsync(HttpClient client)
    {
        var company = await RegisterCompanyAsync(client);
        await UpdateDatabaseAsync(db => db.Companies.Single(item => item.PrimaryAccountId == company.Id)
            .VerificationStatus = CompanyVerificationStatus.Verified);
        var created = await client.SendAsync(Request(HttpMethod.Post, "/api/Hackathons/register/Hackathon", company.Token, new
        {
            Titulo = $"Desafio {Guid.NewGuid():N}",
            Descricao = "Monte uma solução que reduza o tempo entre o pedido e a entrega.",
            Area = "web",
            Tecnologias = new[] { "Python" },
            Metodo = "Avaliação Manual",
            Ranking = "Top 10",
            Premiacao = 5000,
            Formato = "Individual",
            TipoPremiacao = "Dinheiro",
            LinguagemReferencia = "Python",
            SolucaoReferencia = "def resposta(): return 42",
            DataInicio = DateTime.UtcNow.AddDays(2),
            DataFinal = DateTime.UtcNow.AddDays(5),
            DataFimAvaliacao = DateTime.UtcNow.AddDays(8)
        }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<HackathonResponseDto>())!.HackathonId;
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/publicar", company.Token))).StatusCode);
        var participant = await RegisterParticipantAsync(client);
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/inscricao", participant.Token))).StatusCode);
        await UpdateDatabaseAsync(db => db.Hackathons.Single(item => item.Id == Guid.Parse(id)).StartsAt = DateTime.UtcNow.AddHours(-1));
        return (company, participant, id);
    }

    [Fact]
    public async Task Submit_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();
        var (_, _, id) = await ActiveHackathonAsync(client);

        var response = await factory.CreateClient().PostAsJsonAsync($"/api/Hackathons/{id}/entrega", new { linguagem = "Python", codigo = "print(1)" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Submit_ThenGetMine_ShowsVersion()
    {
        var client = factory.CreateClient();
        var (_, participant, id) = await ActiveHackathonAsync(client);

        var sent = await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/entrega", participant.Token,
            new { linguagem = "Python", codigo = "print(1)", nomeArquivo = "rota.py" }));
        var mine = await (await client.SendAsync(Request(HttpMethod.Get, $"/api/Hackathons/{id}/entrega", participant.Token)))
            .Content.ReadFromJsonAsync<MySubmissionDto>();

        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        Assert.True(mine!.Aberta);
        Assert.Equal("print(1)", mine.Atual!.Codigo);
        Assert.Equal("rota.py", mine.Atual.NomeArquivo);
    }

    [Fact]
    public async Task Company_SeesEntriesAndReference()
    {
        var client = factory.CreateClient();
        var (company, participant, id) = await ActiveHackathonAsync(client);
        await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/entrega", participant.Token, new { linguagem = "Python", codigo = "print(1)" }));

        var entries = await client.SendAsync(Request(HttpMethod.Get, $"/api/Hackathons/{id}/entregas", company.Token));
        var reference = await client.SendAsync(Request(HttpMethod.Get, $"/api/Hackathons/{id}/referencia", company.Token));

        Assert.Equal(HttpStatusCode.OK, entries.StatusCode);
        var entry = Assert.Single((await entries.Content.ReadFromJsonAsync<List<SubmissionEntryDto>>())!);
        Assert.Equal(1, entry.VersaoAtual);
        Assert.Equal("def resposta(): return 42", (await reference.Content.ReadFromJsonAsync<ReferenceSolutionDto>())!.Codigo);
    }

    [Fact]
    public async Task Participant_GetEntries_Returns403()
    {
        var client = factory.CreateClient();
        var (_, participant, id) = await ActiveHackathonAsync(client);

        var response = await client.SendAsync(Request(HttpMethod.Get, $"/api/Hackathons/{id}/entregas", participant.Token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HackathonJson_DoesNotExposeReference()
    {
        var client = factory.CreateClient();
        var (_, _, id) = await ActiveHackathonAsync(client);

        var single = await factory.CreateClient().GetStringAsync($"/api/Hackathons/{id}");
        var list = await factory.CreateClient().GetStringAsync("/api/Hackathons");

        foreach (var json in new[] { single, list })
        {
            Assert.DoesNotContain("def resposta", json);
            Assert.DoesNotContain("referencia", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("referenceCode", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
