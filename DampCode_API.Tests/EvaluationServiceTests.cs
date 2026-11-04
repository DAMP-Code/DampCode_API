using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using DampCode_API.Services;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Tests;

public sealed class EvaluationServiceTests
{
    private const string ReferenceCode = "def soma(a, b):\n    return a + b";

    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly DampCodeDbContext _dbContext;
    private readonly InMemorySubmissionVersionStore _store = new();
    private readonly EvaluationService _service;

    public EvaluationServiceTests()
    {
        _dbContext = TestDb.CreateShared(_databaseName);
        _service = new EvaluationService(_dbContext, new CompanyAccessService(_dbContext), _store);
    }

    // Hackathon individual na fase Em avaliação (começou há 4 dias), com prêmios, referência e uma entrega por participante.
    // As entregas saem em ordem de envio (a primeira enviou antes).
    private async Task<(Company Company, Hackathon Hackathon, List<Submission> Submissions)> ReviewingAsync(
        int participants = 3, int prizes = 3, string? reference = ReferenceCode)
    {
        var company = await CompanySeed.CompanyAsync(_dbContext, status: CompanyVerificationStatus.Verified);
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Individual,
            DateTime.UtcNow.AddDays(-4));
        hackathon.ReferenceLanguage = reference is null ? null : "Python";
        hackathon.ReferenceCode = reference;
        for (var position = 1; position <= prizes; position++)
            _dbContext.HackathonPrizes.Add(new HackathonPrize
            {
                HackathonId = hackathon.Id,
                Position = position,
                RewardType = HackathonRewardType.Money,
                Amount = 1000m / position
            });

        var submissions = new List<Submission>();
        for (var index = 0; index < participants; index++)
        {
            var account = await CompanySeed.ParticipantAsync(_dbContext, $"pessoa{index}@exemplo.com", $"Pessoa {index}");
            var participation = new Participation { HackathonId = hackathon.Id, AccountId = account.Id };
            _dbContext.Participations.Add(participation);
            var code = $"def resolve_{index}(x):\n    return x * {index}";
            var documentId = await _store.InsertAsync(Guid.Empty, code, CancellationToken.None);
            var sentAt = DateTime.UtcNow.AddDays(-2).AddMinutes(index);
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
            _dbContext.Submissions.Add(submission);
            _dbContext.SubmissionVersions.Add(new SubmissionVersion
            {
                SubmissionId = submission.Id,
                Version = 1,
                Language = "Python",
                MongoDocumentId = documentId,
                SubmittedByAccountId = account.Id,
                SubmittedAt = sentAt
            });
            submissions.Add(submission);
        }
        await _dbContext.SaveChangesAsync();
        return (company, hackathon, submissions);
    }

    private async Task GradeAsync(Company company, Hackathon hackathon, Submission submission, int score)
    {
        var result = await _service.EvaluateAsync(company.PrimaryAccountId, hackathon.Id, submission.Id,
            new EvaluateDto { Nota = score }, CancellationToken.None);
        Assert.True(result.Succeeded, result.Message);
    }

    private static SetWinnersDto Winners(params (int Position, Guid SubmissionId)[] winners) =>
        new() { Vencedores = winners.Select(item => new WinnerDto { Posicao = item.Position, SubmissionId = item.SubmissionId }).ToList() };

    [Fact]
    public async Task Prizes_PersistWithHackathon()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext, status: CompanyVerificationStatus.Verified);
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Individual,
            DateTime.UtcNow.AddHours(-1));
        _dbContext.HackathonPrizes.AddRange(
            new HackathonPrize { HackathonId = hackathon.Id, Position = 2, RewardType = HackathonRewardType.Interview, Description = "Entrevista com o time" },
            new HackathonPrize { HackathonId = hackathon.Id, Position = 1, RewardType = HackathonRewardType.Money, Amount = 1000m });
        await _dbContext.SaveChangesAsync();

        var reloaded = await TestDb.CreateShared(_databaseName).Hackathons.Include(item => item.Prizes)
            .SingleAsync(item => item.Id == hackathon.Id);

        var prizes = reloaded.Prizes.OrderBy(prize => prize.Position).ToList();
        Assert.Equal(2, prizes.Count);
        Assert.Equal(1000m, prizes[0].Amount);
        Assert.Null(prizes[1].Amount);
        Assert.Equal("Entrevista com o time", prizes[1].Description);
    }

    [Fact]
    public async Task Evaluate_BeforeSubmissionsEnd_ReturnsConflict()
    {
        var (company, hackathon, submissions) = await ReviewingAsync();
        hackathon.StartsAt = DateTime.UtcNow.AddHours(-1);
        hackathon.EndsAt = DateTime.UtcNow.AddDays(1);
        hackathon.ReviewEndsAt = DateTime.UtcNow.AddDays(2);
        await _dbContext.SaveChangesAsync();

        var result = await _service.EvaluateAsync(company.PrimaryAccountId, hackathon.Id, submissions[0].Id,
            new EvaluateDto { Nota = 50 }, CancellationToken.None);

        Assert.Equal(EvaluationServiceError.Conflict, result.Error);
        Assert.Equal("A avaliação abre quando as submissões terminam.", result.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(null)]
    public async Task Evaluate_OutOfRange_ReturnsValidation(int? score)
    {
        var (company, hackathon, submissions) = await ReviewingAsync();

        var result = await _service.EvaluateAsync(company.PrimaryAccountId, hackathon.Id, submissions[0].Id,
            new EvaluateDto { Nota = score }, CancellationToken.None);

        Assert.Equal(EvaluationServiceError.Validation, result.Error);
        Assert.Equal("A nota deve ficar entre 0 e 100.", result.Message);
    }

    [Fact]
    public async Task Evaluate_LongComment_ReturnsValidation()
    {
        var (company, hackathon, submissions) = await ReviewingAsync();

        var result = await _service.EvaluateAsync(company.PrimaryAccountId, hackathon.Id, submissions[0].Id,
            new EvaluateDto { Nota = 50, Comentario = new string('a', 1001) }, CancellationToken.None);

        Assert.Equal(EvaluationServiceError.Validation, result.Error);
        Assert.Equal("O comentário deve ter no máximo 1000 caracteres.", result.Message);
    }

    [Fact]
    public async Task Evaluate_SavesScoreAndEvaluator()
    {
        var (company, hackathon, submissions) = await ReviewingAsync();

        var result = await _service.EvaluateAsync(company.PrimaryAccountId, hackathon.Id, submissions[0].Id,
            new EvaluateDto { Nota = 75, Comentario = "  Código organizado.  " }, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(75, result.Value!.Nota);
        Assert.Equal("Pessoa 0", result.Value.Nome);
        var saved = await TestDb.CreateShared(_databaseName).Submissions.SingleAsync(item => item.Id == submissions[0].Id);
        Assert.Equal(75, saved.Score);
        Assert.Equal("Código organizado.", saved.Feedback);
        Assert.NotNull(saved.EvaluatedAt);
        Assert.Equal(company.PrimaryAccountId, saved.EvaluatedByAccountId);
    }

    [Fact]
    public async Task Ranking_OrdersByScoreThenSubmittedAt()
    {
        var (company, hackathon, submissions) = await ReviewingAsync();
        await GradeAsync(company, hackathon, submissions[2], 80);
        await GradeAsync(company, hackathon, submissions[1], 80);

        var result = await _service.GetRankingAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.Value!.PodeAvaliar);
        Assert.False(result.Value.ResultadoPublicado);
        Assert.Equal(3, result.Value.Premios.Count);
        Assert.Equal([submissions[1].Id, submissions[2].Id, submissions[0].Id], result.Value.Entregas.Select(item => item.SubmissionId));
        Assert.Null(result.Value.Entregas[2].Nota);
        Assert.Equal("Python", result.Value.Entregas[0].Linguagem);
    }

    [Fact]
    public async Task Ranking_SuggestionNullWithoutReference()
    {
        var (company, hackathon, _) = await ReviewingAsync(reference: null);

        var result = await _service.GetRankingAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.All(result.Value!.Entregas, item => Assert.Null(item.Semelhanca));
    }

    [Fact]
    public async Task Ranking_SuggestionFromReference()
    {
        var (company, hackathon, submissions) = await ReviewingAsync(participants: 1);
        _store.Documents[submissions[0].CurrentMongoVersionId!] = (submissions[0].Id, ReferenceCode);

        var result = await _service.GetRankingAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.Equal(100, Assert.Single(result.Value!.Entregas).Semelhanca);
    }

    [Fact]
    public async Task Ranking_OtherCompany_ReturnsNotFound()
    {
        var (_, hackathon, _) = await ReviewingAsync();
        var other = await CompanySeed.CompanyAsync(_dbContext, "outra@exemplo.com", CompanyVerificationStatus.Verified);

        var result = await _service.GetRankingAsync(other.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.Equal(EvaluationServiceError.NotFound, result.Error);
        Assert.Equal("Hackathon não encontrado.", result.Message);
    }

    [Fact]
    public async Task SetWinners_OutOfScoreOrder_Saves()
    {
        var (company, hackathon, submissions) = await ReviewingAsync();
        await GradeAsync(company, hackathon, submissions[0], 90);
        await GradeAsync(company, hackathon, submissions[1], 80);
        await GradeAsync(company, hackathon, submissions[2], 70);

        var result = await _service.SetWinnersAsync(company.PrimaryAccountId, hackathon.Id, Winners((1, submissions[2].Id)), CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(1, result.Value!.Entregas.Single(item => item.SubmissionId == submissions[2].Id).PosicaoPremio);
        var saved = await TestDb.CreateShared(_databaseName).Submissions.ToListAsync();
        Assert.Equal(1, saved.Single(item => item.Id == submissions[2].Id).PrizePosition);
        Assert.Equal(2, saved.Count(item => item.PrizePosition is null));
    }

    [Theory]
    [InlineData("posicao")]
    [InlineData("posicaoRepetida")]
    [InlineData("entregaRepetida")]
    public async Task SetWinners_Invalid_ReturnsValidation(string scenario)
    {
        var (company, hackathon, submissions) = await ReviewingAsync();
        var dto = scenario switch
        {
            "posicao" => Winners((5, submissions[0].Id)),
            "posicaoRepetida" => Winners((1, submissions[0].Id), (1, submissions[1].Id)),
            _ => Winners((1, submissions[0].Id), (2, submissions[0].Id))
        };

        var result = await _service.SetWinnersAsync(company.PrimaryAccountId, hackathon.Id, dto, CancellationToken.None);

        Assert.Equal(EvaluationServiceError.Validation, result.Error);
        Assert.Equal("Escolha um vencedor diferente para cada prêmio.", result.Message);
    }

    [Fact]
    public async Task SetWinners_SwapPositions_Succeeds()
    {
        var (company, hackathon, submissions) = await ReviewingAsync();
        await _service.SetWinnersAsync(company.PrimaryAccountId, hackathon.Id,
            Winners((1, submissions[0].Id), (2, submissions[1].Id)), CancellationToken.None);

        var result = await _service.SetWinnersAsync(company.PrimaryAccountId, hackathon.Id,
            Winners((1, submissions[1].Id), (2, submissions[0].Id)), CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var saved = await TestDb.CreateShared(_databaseName).Submissions.ToListAsync();
        Assert.Equal(1, saved.Single(item => item.Id == submissions[1].Id).PrizePosition);
        Assert.Equal(2, saved.Single(item => item.Id == submissions[0].Id).PrizePosition);
    }

    [Fact]
    public async Task Publish_WithUngraded_ReturnsConflict()
    {
        var (company, hackathon, submissions) = await ReviewingAsync();
        await GradeAsync(company, hackathon, submissions[0], 90);

        var result = await _service.PublishResultsAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.Equal(EvaluationServiceError.Conflict, result.Error);
        Assert.Equal("Dê nota a todas as entregas antes de publicar.", result.Message);
    }

    [Fact]
    public async Task Publish_SetsFinalPositions()
    {
        var (company, hackathon, submissions) = await ReviewingAsync(participants: 5);
        int[] scores = [90, 80, 70, 60, 50];
        for (var index = 0; index < scores.Length; index++) await GradeAsync(company, hackathon, submissions[index], scores[index]);
        await _service.SetWinnersAsync(company.PrimaryAccountId, hackathon.Id,
            Winners((1, submissions[2].Id), (3, submissions[0].Id)), CancellationToken.None);

        var result = await _service.PublishResultsAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var db = TestDb.CreateShared(_databaseName);
        var saved = await db.Submissions.ToListAsync();
        int?[] expected = [3, 4, 1, 5, 6];
        Assert.Equal(expected, submissions.Select(item => saved.Single(row => row.Id == item.Id).FinalPosition));
        Assert.All(saved, item => Assert.Equal("Evaluated", item.Status));
        var reloaded = await db.Hackathons.SingleAsync(item => item.Id == hackathon.Id);
        Assert.Equal(HackathonStatus.Closed, reloaded.Status);
        Assert.NotNull(reloaded.ClosedAt);
        Assert.NotNull(reloaded.ResultsPublishedAt);
    }

    [Fact]
    public async Task Publish_AfterReviewEndsAt_KeepsStatusAndPublishes()
    {
        var (company, hackathon, submissions) = await ReviewingAsync(participants: 1);
        hackathon.ReviewEndsAt = DateTime.UtcNow.AddHours(-1);
        await _dbContext.SaveChangesAsync();
        await GradeAsync(company, hackathon, submissions[0], 60);

        var result = await _service.PublishResultsAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var reloaded = await TestDb.CreateShared(_databaseName).Hackathons.SingleAsync(item => item.Id == hackathon.Id);
        Assert.Equal(HackathonStatus.Scheduled, reloaded.Status);
        Assert.Null(reloaded.ClosedAt);
        Assert.NotNull(reloaded.ResultsPublishedAt);
    }

    [Fact]
    public async Task AfterPublish_EverythingLocked()
    {
        var (company, hackathon, submissions) = await ReviewingAsync(participants: 1);
        await GradeAsync(company, hackathon, submissions[0], 60);
        await _service.PublishResultsAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        var evaluate = await _service.EvaluateAsync(company.PrimaryAccountId, hackathon.Id, submissions[0].Id,
            new EvaluateDto { Nota = 70 }, CancellationToken.None);
        var winners = await _service.SetWinnersAsync(company.PrimaryAccountId, hackathon.Id, Winners((1, submissions[0].Id)), CancellationToken.None);
        var publish = await _service.PublishResultsAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.Equal((EvaluationServiceError.Conflict, "O resultado já foi publicado."), (evaluate.Error, evaluate.Message));
        Assert.Equal((EvaluationServiceError.Conflict, "O resultado já foi publicado."), (winners.Error, winners.Message));
        Assert.Equal((EvaluationServiceError.Conflict, "O resultado já foi publicado."), (publish.Error, publish.Message));
        var ranking = await _service.GetRankingAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);
        Assert.False(ranking.Value!.PodeAvaliar);
        Assert.True(ranking.Value.ResultadoPublicado);
    }

    [Fact]
    public async Task Cancelled_ReturnsConflict()
    {
        var (company, hackathon, submissions) = await ReviewingAsync();
        hackathon.Status = HackathonStatus.Cancelled;
        hackathon.CancelledAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var result = await _service.EvaluateAsync(company.PrimaryAccountId, hackathon.Id, submissions[0].Id,
            new EvaluateDto { Nota = 50 }, CancellationToken.None);

        Assert.Equal(EvaluationServiceError.Conflict, result.Error);
        Assert.Equal("Este Hackathon já foi encerrado ou cancelado.", result.Message);
    }
}
