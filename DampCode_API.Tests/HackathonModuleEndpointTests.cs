using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DampCode_API.Tests;

public sealed class HackathonModuleEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    // Hackathon publicado (início em 2 dias) da empresa, gravado direto no banco.
    private async Task<(AuthResponseDto Company, Guid HackathonId)> PublishedAsync(HttpClient client, HackathonStatus status = HackathonStatus.Scheduled)
    {
        var company = await RegisterCompanyAsync(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DampCodeDbContext>();
        var companyId = db.Companies.Single(item => item.PrimaryAccountId == company.Id).Id;
        var start = DateTime.UtcNow.AddDays(2);
        var hackathon = new Hackathon
        {
            CompanyId = companyId,
            CreatedByAccountId = company.Id,
            Title = "Hackathon publicado",
            Slug = $"hackathon-{Guid.NewGuid():N}",
            Description = "Descrição do Hackathon publicado",
            Area = "web",
            ParticipationMode = HackathonParticipationMode.Individual,
            Status = status,
            EvaluationMethod = "Avaliação Manual",
            StartsAt = start,
            EndsAt = start.AddDays(3),
            ReviewEndsAt = start.AddDays(6),
            ScheduledAt = DateTime.UtcNow
        };
        db.Hackathons.Add(hackathon);
        await db.SaveChangesAsync();
        return (company, hackathon.Id);
    }

    private static async Task<Guid> CreateModuleAsync(HttpClient client, string token, Guid hackathonId, string title)
    {
        var response = await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{hackathonId}/modulos", token, new { titulo = title }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HackathonModuleDto>())!.ModuloId;
    }

    [Fact]
    public async Task GetModules_PublishedAsAnonymous_Returns200()
    {
        var client = factory.CreateClient();
        var (company, id) = await PublishedAsync(client);
        await CreateModuleAsync(client, company.Token, id, "Semana 1");

        var response = await factory.CreateClient().GetAsync($"/api/Hackathons/{id}/modulos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var modules = await response.Content.ReadFromJsonAsync<List<HackathonModuleDto>>();
        Assert.Equal("Semana 1", Assert.Single(modules!).Titulo);
    }

    [Fact]
    public async Task GetModules_DraftAsAnonymous_Returns404()
    {
        var client = factory.CreateClient();
        var (_, id) = await PublishedAsync(client, HackathonStatus.Draft);

        var response = await factory.CreateClient().GetAsync($"/api/Hackathons/{id}/modulos");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateModule_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();
        var (_, id) = await PublishedAsync(client);

        var response = await factory.CreateClient().PostAsJsonAsync($"/api/Hackathons/{id}/modulos", new { titulo = "Semana 1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateModule_AsParticipant_Returns403()
    {
        var client = factory.CreateClient();
        var (_, id) = await PublishedAsync(client);
        var participant = await RegisterParticipantAsync(client);

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/modulos", participant.Token, new { titulo = "Semana 1" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateModule_Cancelled_Returns409WithMessage()
    {
        var client = factory.CreateClient();
        var (company, id) = await PublishedAsync(client, HackathonStatus.Cancelled);

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/modulos", company.Token, new { titulo = "Semana 1" }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Hackathon encerrado ou cancelado não aceita mudanças nos módulos.", problem!.Detail);
    }

    [Fact]
    public async Task CreateContent_InvalidUrl_Returns400()
    {
        var client = factory.CreateClient();
        var (company, id) = await PublishedAsync(client);
        var moduleId = await CreateModuleAsync(client, company.Token, id, "Semana 1");

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/modulos/{moduleId}/conteudos", company.Token,
            new { tipo = "Link", titulo = "Curso", url = "javascript:alert(1)" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FullFlow_CreateModuleAndContent_UsesJsonNamesFromDesign()
    {
        var client = factory.CreateClient();
        var (company, id) = await PublishedAsync(client);

        var createdModule = await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/modulos", company.Token,
            new { titulo = "Semana 1 — Fundamentos", descricao = "Base do desafio" }));
        Assert.Equal(HttpStatusCode.Created, createdModule.StatusCode);
        using var moduleJson = JsonDocument.Parse(await createdModule.Content.ReadAsStringAsync());
        var moduleId = moduleJson.RootElement.GetProperty("moduloId").GetGuid();
        Assert.Equal("Semana 1 — Fundamentos", moduleJson.RootElement.GetProperty("titulo").GetString());
        Assert.Equal("Base do desafio", moduleJson.RootElement.GetProperty("descricao").GetString());
        Assert.Equal(1, moduleJson.RootElement.GetProperty("posicao").GetInt32());
        Assert.Equal(0, moduleJson.RootElement.GetProperty("conteudos").GetArrayLength());

        var createdContent = await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/modulos/{moduleId}/conteudos", company.Token,
            new { tipo = "Video", titulo = "Aula de abertura", url = "https://youtu.be/abc" }));
        Assert.Equal(HttpStatusCode.Created, createdContent.StatusCode);
        using var contentJson = JsonDocument.Parse(await createdContent.Content.ReadAsStringAsync());
        Assert.True(contentJson.RootElement.TryGetProperty("conteudoId", out _));
        Assert.Equal("Video", contentJson.RootElement.GetProperty("tipo").GetString());
        Assert.Equal("Aula de abertura", contentJson.RootElement.GetProperty("titulo").GetString());
        Assert.Equal(JsonValueKind.Null, contentJson.RootElement.GetProperty("texto").ValueKind);
        Assert.Equal("https://youtu.be/abc", contentJson.RootElement.GetProperty("url").GetString());
        Assert.Equal(1, contentJson.RootElement.GetProperty("posicao").GetInt32());

        var listed = await factory.CreateClient().GetAsync($"/api/Hackathons/{id}/modulos");
        using var listJson = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        var module = Assert.Single(listJson.RootElement.EnumerateArray());
        var content = Assert.Single(module.GetProperty("conteudos").EnumerateArray());
        Assert.Equal("Aula de abertura", content.GetProperty("titulo").GetString());
    }

    [Fact]
    public async Task EditReorderAndDelete_ReturnExpectedStatusCodes()
    {
        var client = factory.CreateClient();
        var (company, id) = await PublishedAsync(client);
        var first = await CreateModuleAsync(client, company.Token, id, "Semana 1");
        var second = await CreateModuleAsync(client, company.Token, id, "Semana 2");
        var contentResponse = await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/modulos/{first}/conteudos", company.Token,
            new { tipo = "Texto", titulo = "Regras", texto = "Leia tudo." }));
        var contentId = (await contentResponse.Content.ReadFromJsonAsync<HackathonModuleContentDto>())!.ConteudoId;

        var updatedModule = await client.SendAsync(Request(HttpMethod.Put, $"/api/Hackathons/{id}/modulos/{first}", company.Token,
            new { titulo = "Semana 1 — Fundamentos" }));
        var updatedContent = await client.SendAsync(Request(HttpMethod.Put, $"/api/Hackathons/{id}/modulos/{first}/conteudos/{contentId}",
            company.Token, new { tipo = "Link", titulo = "Curso", url = "https://cursos.exemplo.com" }));
        var reorderedContents = await client.SendAsync(Request(HttpMethod.Put, $"/api/Hackathons/{id}/modulos/{first}/conteudos/ordem",
            company.Token, new { ids = new[] { contentId } }));
        var reordered = await client.SendAsync(Request(HttpMethod.Put, $"/api/Hackathons/{id}/modulos/ordem", company.Token,
            new { ids = new[] { second, first } }));
        var badOrder = await client.SendAsync(Request(HttpMethod.Put, $"/api/Hackathons/{id}/modulos/ordem", company.Token,
            new { ids = new[] { second } }));
        var deletedContent = await client.SendAsync(Request(HttpMethod.Delete, $"/api/Hackathons/{id}/modulos/{first}/conteudos/{contentId}",
            company.Token));
        var deletedModule = await client.SendAsync(Request(HttpMethod.Delete, $"/api/Hackathons/{id}/modulos/{second}", company.Token));

        Assert.Equal(HttpStatusCode.OK, updatedModule.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updatedContent.StatusCode);
        Assert.Equal("Link", (await updatedContent.Content.ReadFromJsonAsync<HackathonModuleContentDto>())!.Tipo);
        Assert.Equal(HttpStatusCode.OK, reorderedContents.StatusCode);
        Assert.Equal(first, (await reorderedContents.Content.ReadFromJsonAsync<HackathonModuleDto>())!.ModuloId);
        Assert.Equal(HttpStatusCode.OK, reordered.StatusCode);
        Assert.Equal([second, first], (await reordered.Content.ReadFromJsonAsync<List<HackathonModuleDto>>())!.Select(item => item.ModuloId));
        Assert.Equal(HttpStatusCode.BadRequest, badOrder.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deletedContent.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deletedModule.StatusCode);

        var remaining = await (await factory.CreateClient().GetAsync($"/api/Hackathons/{id}/modulos")).Content
            .ReadFromJsonAsync<List<HackathonModuleDto>>();
        var module = Assert.Single(remaining!);
        Assert.Equal(first, module.ModuloId);
        Assert.Equal(1, module.Posicao);
        Assert.Empty(module.Conteudos);
    }
}
