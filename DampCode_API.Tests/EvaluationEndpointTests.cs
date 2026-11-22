using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DampCode_API.Tests;

public sealed class EvaluationEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string NewEmail() => $"{Guid.NewGuid():N}@exemplo.com";

    private static async Task<AuthResponseDto> RegisterParticipantAsync(HttpClient client, string name = "Maria Lima")
    {
        var response = await client.PostAsJsonAsync("/api/Auth/register/participante", new
        {
            name,
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

    // Hackathon individual na fase Em avaliação gravado direto no banco, com dois prêmios e uma entrega do participante.
    private async Task<(AuthResponseDto Company, AuthResponseDto Participant, Guid HackathonId, Guid SubmissionId)> ReviewingAsync(HttpClient client)
    {
        var company = await RegisterCompanyAsync(client);
        var participant = await RegisterParticipantAsync(client);
        var store = (InMemorySubmissionVersionStore)factory.Services.GetRequiredService<ISubmissionVersionStore>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DampCodeDbContext>();

        var companyId = db.Companies.Single(item => item.PrimaryAccountId == company.Id).Id;
        var start = DateTime.UtcNow.AddDays(-4);
        var hackathon = new Hackathon
        {
            CompanyId = companyId,
            CreatedByAccountId = company.Id,
            Title = "Hackathon em avaliação",
            Slug = $"hackathon-{Guid.NewGuid():N}",
            Description = "Descrição do Hackathon em avaliação",
            Area = "web",
            ParticipationMode = HackathonParticipationMode.Individual,
            Status = HackathonStatus.Scheduled,
            EvaluationMethod = "Avaliação Manual",
            PrizeAmount = 1000,
            RewardType = HackathonRewardType.Money,
            WinnersCount = 2,
            StartsAt = start,
            EndsAt = start.AddDays(3),
            ReviewEndsAt = start.AddDays(6),
            ScheduledAt = DateTime.UtcNow
        };
        db.Hackathons.Add(hackathon);
        for (var position = 1; position <= 2; position++)
            db.HackathonPrizes.Add(new HackathonPrize
            {
                HackathonId = hackathon.Id,
                Position = position,
                RewardType = HackathonRewardType.Money,
                Amount = 1000m / position
            });

        var participation = new Participation { HackathonId = hackathon.Id, AccountId = participant.Id };
        db.Participations.Add(participation);
        var documentId = await store.InsertAsync(Guid.Empty, "print(1)", CancellationToken.None);
        var sentAt = DateTime.UtcNow.AddDays(-2);
        var submission = new Submission
        {
            HackathonId = hackathon.Id,
            ParticipationId = participation.Id,
            Status = "Submitted",
            CurrentVersion = 1,
            CurrentMongoVersionId = documentId,
            FirstSubmittedAt = sentAt,
            LastSubmittedAt = sentAt
        };
        db.Submissions.Add(submission);
        db.SubmissionVersions.Add(new SubmissionVersion
        {
            SubmissionId = submission.Id,
            Version = 1,
            Language = "Python",
            MongoDocumentId = documentId,
            SubmittedByAccountId = participant.Id,
            SubmittedAt = sentAt
        });
        await db.SaveChangesAsync();
        return (company, participant, hackathon.Id, submission.Id);
    }

    [Fact]
    public async Task Ranking_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();
        var (_, _, id, _) = await ReviewingAsync(client);

        var response = await factory.CreateClient().GetAsync($"/api/Hackathons/{id}/avaliacao");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ranking_AsParticipant_Returns403()
    {
        var client = factory.CreateClient();
        var (_, participant, id, _) = await ReviewingAsync(client);

        var response = await client.SendAsync(Request(HttpMethod.Get, $"/api/Hackathons/{id}/avaliacao", participant.Token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FullFlow_GradeChooseWinnerPublish()
    {
        var client = factory.CreateClient();
        var (company, participant, id, submissionId) = await ReviewingAsync(client);

        var graded = await client.SendAsync(Request(HttpMethod.Put, $"/api/Hackathons/{id}/entregas/{submissionId}/avaliacao",
            company.Token, new { nota = 90, comentario = "Ótima solução" }));
        var chosen = await client.SendAsync(Request(HttpMethod.Put, $"/api/Hackathons/{id}/vencedores", company.Token,
            new { vencedores = new[] { new { posicao = 1, submissionId } } }));
        var published = await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/resultado", company.Token));
        var overview = await (await client.SendAsync(Request(HttpMethod.Get, "/api/Participations/me", participant.Token)))
            .Content.ReadFromJsonAsync<ParticipationsOverviewDto>();

        Assert.Equal(HttpStatusCode.OK, graded.StatusCode);
        Assert.Equal(HttpStatusCode.OK, chosen.StatusCode);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var hackathon = (await published.Content.ReadFromJsonAsync<HackathonResponseDto>())!;
        Assert.True(hackathon.ResultadoPublicado);
        Assert.Equal("Encerrado", hackathon.Fase);
        var result = Assert.Single(overview!.Inscricoes).Resultado!;
        Assert.Equal(1, result.Posicao);
        Assert.Equal(1, result.Premio!.Posicao);
    }

    [Fact]
    public async Task HackathonJson_HasPremios()
    {
        var client = factory.CreateClient();
        var (_, _, id, _) = await ReviewingAsync(client);

        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync($"/api/Hackathons/{id}"));

        Assert.Equal(2, json.RootElement.GetProperty("premios").GetArrayLength());
        Assert.False(json.RootElement.GetProperty("resultadoPublicado").GetBoolean());
    }

    // Segunda entrega no mesmo Hackathon, de outro participante.
    private async Task<(AuthResponseDto Participant, Guid SubmissionId)> AddSubmissionAsync(HttpClient client, Guid hackathonId, string name)
    {
        var participant = await RegisterParticipantAsync(client, name);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DampCodeDbContext>();
        var participation = new Participation { HackathonId = hackathonId, AccountId = participant.Id };
        db.Participations.Add(participation);
        var sentAt = DateTime.UtcNow.AddDays(-1);
        var submission = new Submission
        {
            HackathonId = hackathonId,
            ParticipationId = participation.Id,
            Status = "Submitted",
            CurrentVersion = 1,
            FirstSubmittedAt = sentAt,
            LastSubmittedAt = sentAt
        };
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();
        return (participant, submission.Id);
    }

    [Fact]
    public async Task Result_AfterPublish_PublicRanking()
    {
        var client = factory.CreateClient();
        var (company, _, id, submissionId) = await ReviewingAsync(client);
        var (_, otherSubmissionId) = await AddSubmissionAsync(client, id, "João Alves");
        await client.SendAsync(Request(HttpMethod.Put, $"/api/Hackathons/{id}/entregas/{submissionId}/avaliacao",
            company.Token, new { nota = 90, comentario = "Ótima solução" }));
        await client.SendAsync(Request(HttpMethod.Put, $"/api/Hackathons/{id}/entregas/{otherSubmissionId}/avaliacao",
            company.Token, new { nota = 70, comentario = "Faltaram testes" }));
        await client.SendAsync(Request(HttpMethod.Put, $"/api/Hackathons/{id}/vencedores", company.Token,
            new { vencedores = new[] { new { posicao = 1, submissionId } } }));
        var published = await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/resultado", company.Token));
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        var anonymous = await factory.CreateClient().GetAsync($"/api/Hackathons/{id}/resultado");
        var raw = await anonymous.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        using var json = JsonDocument.Parse(raw);
        var root = json.RootElement;
        Assert.Equal(id, root.GetProperty("hackathonId").GetGuid());
        Assert.Equal("Hackathon em avaliação", root.GetProperty("titulo").GetString());
        Assert.True(root.GetProperty("resultadoPublicado").GetBoolean());
        Assert.Equal(3, root.GetProperty("totalClassificados").GetInt32());
        var first = root.GetProperty("classificacao")[0];
        Assert.Equal(1, first.GetProperty("posicao").GetInt32());
        Assert.Equal("Maria Lima", first.GetProperty("nome").GetString());
        Assert.False(first.GetProperty("equipe").GetBoolean());
        Assert.Equal(["Maria Lima"], first.GetProperty("membros").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(90, first.GetProperty("nota").GetInt32());
        Assert.Equal(1, first.GetProperty("premio").GetProperty("posicao").GetInt32());
        Assert.Equal("Dinheiro", first.GetProperty("premio").GetProperty("tipo").GetString());
        Assert.False(first.GetProperty("voce").GetBoolean());
        var second = root.GetProperty("classificacao")[1];
        Assert.Equal(3, second.GetProperty("posicao").GetInt32());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("premio").ValueKind);
        // nada de comentário da empresa, ids de entrega ou conta, código ou e-mail na resposta pública
        foreach (var forbidden in new[] { "comentario", "feedback", "Ótima solução", "Faltaram testes", "submissionId", "accountId", "@exemplo.com", "codigo" })
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Result_WithToken_MarksOnlyCallerEntry()
    {
        var client = factory.CreateClient();
        var (company, participant, id, submissionId) = await ReviewingAsync(client);
        var (other, otherSubmissionId) = await AddSubmissionAsync(client, id, "João Alves");
        await client.SendAsync(Request(HttpMethod.Put, $"/api/Hackathons/{id}/entregas/{submissionId}/avaliacao", company.Token, new { nota = 90 }));
        await client.SendAsync(Request(HttpMethod.Put, $"/api/Hackathons/{id}/entregas/{otherSubmissionId}/avaliacao", company.Token, new { nota = 70 }));
        await client.SendAsync(Request(HttpMethod.Post, $"/api/Hackathons/{id}/resultado", company.Token));

        var asParticipant = await (await client.SendAsync(Request(HttpMethod.Get, $"/api/Hackathons/{id}/resultado", participant.Token)))
            .Content.ReadFromJsonAsync<HackathonResultDto>();
        var asOther = await (await client.SendAsync(Request(HttpMethod.Get, $"/api/Hackathons/{id}/resultado", other.Token)))
            .Content.ReadFromJsonAsync<HackathonResultDto>();
        var invalidToken = await client.SendAsync(Request(HttpMethod.Get, $"/api/Hackathons/{id}/resultado", "token-invalido"));

        Assert.Equal(["Maria Lima", "João Alves"], asParticipant!.Classificacao.Select(item => item.Nome));
        Assert.Equal([true, false], asParticipant.Classificacao.Select(item => item.Voce));
        Assert.Equal([false, true], asOther!.Classificacao.Select(item => item.Voce));
        // token inválido não barra a rota pública: segue como visitante
        Assert.Equal(HttpStatusCode.OK, invalidToken.StatusCode);
        var anonymous = (await invalidToken.Content.ReadFromJsonAsync<HackathonResultDto>())!;
        Assert.All(anonymous.Classificacao, item => Assert.False(item.Voce));
    }

    [Fact]
    public async Task Result_NotPublished_ReturnsEmpty()
    {
        var client = factory.CreateClient();
        var (_, _, id, _) = await ReviewingAsync(client);

        using var json = JsonDocument.Parse(await factory.CreateClient().GetStringAsync($"/api/Hackathons/{id}/resultado"));

        Assert.False(json.RootElement.GetProperty("resultadoPublicado").GetBoolean());
        Assert.Equal(0, json.RootElement.GetProperty("totalClassificados").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("classificacao").GetArrayLength());
    }

    [Fact]
    public async Task Result_Draft_Returns404ForStranger()
    {
        var client = factory.CreateClient();
        var (company, participant, id, _) = await ReviewingAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DampCodeDbContext>();
            db.Hackathons.Single(item => item.Id == id).Status = HackathonStatus.Draft;
            await db.SaveChangesAsync();
        }

        var anonymous = await factory.CreateClient().GetAsync($"/api/Hackathons/{id}/resultado");
        var stranger = await client.SendAsync(Request(HttpMethod.Get, $"/api/Hackathons/{id}/resultado", participant.Token));
        var owner = await client.SendAsync(Request(HttpMethod.Get, $"/api/Hackathons/{id}/resultado", company.Token));

        Assert.Equal(HttpStatusCode.NotFound, anonymous.StatusCode);
        Assert.Equal("Hackathon não encontrado.", (await anonymous.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
    }
}
