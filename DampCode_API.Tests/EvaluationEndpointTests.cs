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
}
