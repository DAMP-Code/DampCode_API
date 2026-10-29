using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using DampCode_API.Services;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Tests;

public sealed class SubmissionServiceTests
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly DampCodeDbContext _dbContext;
    private readonly InMemorySubmissionVersionStore _store = new();
    private readonly SubmissionService _service;

    public SubmissionServiceTests()
    {
        _dbContext = TestDb.CreateShared(_databaseName);
        _service = new SubmissionService(_dbContext, new CompanyAccessService(_dbContext), _store);
    }

    private static SubmitCodeDto Code(string code = "print(1)", string language = "Python", string? fileName = null) =>
        new() { Linguagem = language, Codigo = code, NomeArquivo = fileName };

    private async Task<Participation> RegisterAsync(Hackathon hackathon, Account account, HackathonTeam? team = null)
    {
        var participation = new Participation { HackathonId = hackathon.Id, AccountId = account.Id, TeamId = team?.Id };
        _dbContext.Participations.Add(participation);
        await _dbContext.SaveChangesAsync();
        return participation;
    }

    // Hackathon individual na fase Ativo, com Ana inscrita.
    private async Task<(Company Company, Hackathon Hackathon, Account Ana)> ActiveIndividualAsync()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext, status: CompanyVerificationStatus.Verified);
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Individual,
            DateTime.UtcNow.AddHours(-1));
        var ana = await CompanySeed.ParticipantAsync(_dbContext, "ana@exemplo.com", "Ana Souza");
        await RegisterAsync(hackathon, ana);
        return (company, hackathon, ana);
    }

    // Hackathon de equipe na fase Ativo, com Ana e Bruno na equipe "Os Rápidos".
    private async Task<(Company Company, Hackathon Hackathon, HackathonTeam Team, Account Ana, Account Bruno)> ActiveTeamAsync(
        string companyEmail = "empresa@exemplo.com")
    {
        var company = await CompanySeed.CompanyAsync(_dbContext, companyEmail, CompanyVerificationStatus.Verified);
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Team,
            DateTime.UtcNow.AddHours(-1));
        var ana = await CompanySeed.ParticipantAsync(_dbContext, $"ana-{Guid.NewGuid():N}@exemplo.com", "Ana Souza");
        var bruno = await CompanySeed.ParticipantAsync(_dbContext, $"bruno-{Guid.NewGuid():N}@exemplo.com", "Bruno Lima");
        var team = new HackathonTeam
        {
            HackathonId = hackathon.Id,
            CreatedByAccountId = ana.Id,
            Name = "Os Rápidos",
            NormalizedName = "OS RÁPIDOS",
            MemberCount = 2
        };
        _dbContext.HackathonTeams.Add(team);
        await RegisterAsync(hackathon, ana, team);
        await RegisterAsync(hackathon, bruno, team);
        return (company, hackathon, team, ana, bruno);
    }

    private async Task<SubmissionCodeDto> SubmitAsync(Account account, Hackathon hackathon, SubmitCodeDto? dto = null)
    {
        var result = await _service.SubmitAsync(account.Id, hackathon.Id, dto ?? Code(), CancellationToken.None);
        Assert.True(result.Succeeded, result.Message);
        return result.Value!;
    }

    [Fact]
    public async Task Submission_PersistsWithVersions()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext, status: CompanyVerificationStatus.Verified);
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Team);
        var ana = await CompanySeed.ParticipantAsync(_dbContext, "ana@exemplo.com");
        var team = new HackathonTeam { HackathonId = hackathon.Id, CreatedByAccountId = ana.Id, Name = "Equipe", NormalizedName = "EQUIPE", MemberCount = 1 };
        _dbContext.HackathonTeams.Add(team);
        var submission = new Submission { HackathonId = hackathon.Id, TeamId = team.Id, Status = "Submitted", CurrentVersion = 1 };
        submission.Versions.Add(new SubmissionVersion
        {
            Submission = submission,
            Version = 1,
            Language = "Python",
            MongoDocumentId = "abc",
            SubmittedByAccountId = ana.Id
        });
        _dbContext.Submissions.Add(submission);
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.Submissions.Include(item => item.Versions).SingleAsync();
        Assert.Single(saved.Versions);
        Assert.Null(saved.ParticipationId);
        Assert.Null(saved.StageId);
    }

    [Fact]
    public async Task InMemoryStore_RoundTrip()
    {
        var store = new InMemorySubmissionVersionStore();

        var id = await store.InsertAsync(Guid.NewGuid(), "print(1)", CancellationToken.None);
        var code = await store.GetCodeAsync(id, CancellationToken.None);
        await store.DeleteAsync(id, CancellationToken.None);

        Assert.Equal(24, id.Length);
        Assert.Equal("print(1)", code);
        Assert.Null(await store.GetCodeAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task Submit_FirstVersion_CreatesSubmission()
    {
        var (_, hackathon, ana) = await ActiveIndividualAsync();

        var version = await SubmitAsync(ana, hackathon, Code("print(1)", fileName: "rota.py"));

        Assert.Equal(1, version.Versao);
        Assert.Equal("rota.py", version.NomeArquivo);
        var submission = _dbContext.Submissions.Single();
        Assert.NotNull(submission.ParticipationId);
        Assert.Null(submission.TeamId);
        Assert.Equal(1, submission.CurrentVersion);
        Assert.Equal("print(1)", _store.Documents[submission.CurrentMongoVersionId!].Code);
    }

    [Fact]
    public async Task Submit_SecondVersion_BecomesCurrent()
    {
        var (_, hackathon, ana) = await ActiveIndividualAsync();
        await SubmitAsync(ana, hackathon, Code("print(1)"));
        await SubmitAsync(ana, hackathon, Code("print(2)"));

        var mine = (await _service.GetMineAsync(ana.Id, hackathon.Id, CancellationToken.None)).Value!;

        Assert.True(mine.Aberta);
        Assert.Equal([2, 1], mine.Versoes.Select(item => item.Versao));
        Assert.Equal([true, false], mine.Versoes.Select(item => item.Vale));
        Assert.Equal("print(2)", mine.Atual!.Codigo);
    }

    [Fact]
    public async Task Submit_TeamMembers_ShareSubmission()
    {
        var (_, hackathon, team, ana, bruno) = await ActiveTeamAsync();
        await SubmitAsync(ana, hackathon, Code("v1"));
        await SubmitAsync(bruno, hackathon, Code("v2"));

        var mine = (await _service.GetMineAsync(ana.Id, hackathon.Id, CancellationToken.None)).Value!;

        var submission = _dbContext.Submissions.Single();
        Assert.Equal(team.Id, submission.TeamId);
        Assert.Null(submission.ParticipationId);
        Assert.Equal("Os Rápidos", mine.Equipe);
        Assert.Equal(["Bruno Lima", "Ana Souza"], mine.Versoes.Select(item => item.EnviadoPor));
    }

    [Fact]
    public async Task Submit_NotRegistered_ReturnsForbidden()
    {
        var (_, hackathon, _) = await ActiveIndividualAsync();
        var carla = await CompanySeed.ParticipantAsync(_dbContext, "carla@exemplo.com");

        var result = await _service.SubmitAsync(carla.Id, hackathon.Id, Code(), CancellationToken.None);

        Assert.Equal(SubmissionServiceError.Forbidden, result.Error);
        Assert.Equal("Você não está inscrito neste Hackathon.", result.Message);
    }

    [Fact]
    public async Task GetMine_LeftParticipation_ReturnsForbidden()
    {
        var (_, hackathon, ana) = await ActiveIndividualAsync();
        _dbContext.Participations.Single().LeftAt = DateTime.UtcNow.AddDays(-1);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetMineAsync(ana.Id, hackathon.Id, CancellationToken.None);

        Assert.Equal(SubmissionServiceError.Forbidden, result.Error);
    }

    [Fact]
    public async Task Submit_BeforeStart_ReturnsClosed()
    {
        var (_, hackathon, ana) = await ActiveIndividualAsync();
        hackathon.StartsAt = DateTime.UtcNow.AddDays(1);
        hackathon.EndsAt = DateTime.UtcNow.AddDays(2);
        await _dbContext.SaveChangesAsync();

        var result = await _service.SubmitAsync(ana.Id, hackathon.Id, Code(), CancellationToken.None);

        Assert.Equal(SubmissionServiceError.Conflict, result.Error);
        Assert.Equal("As submissões deste Hackathon estão fechadas.", result.Message);
    }

    [Fact]
    public async Task Submit_AtEndsAt_ReturnsClosed()
    {
        var (_, hackathon, ana) = await ActiveIndividualAsync();
        hackathon.StartsAt = DateTime.UtcNow.AddDays(-3);
        hackathon.EndsAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var result = await _service.SubmitAsync(ana.Id, hackathon.Id, Code(), CancellationToken.None);

        Assert.Equal("As submissões deste Hackathon estão fechadas.", result.Message);
    }

    [Fact]
    public async Task Submit_InvalidLanguage_ReturnsValidation()
    {
        var (_, hackathon, ana) = await ActiveIndividualAsync();

        var result = await _service.SubmitAsync(ana.Id, hackathon.Id, Code(language: "Cobol"), CancellationToken.None);

        Assert.Equal(SubmissionServiceError.Validation, result.Error);
        Assert.Equal("Escolha uma linguagem.", result.Message);
    }

    [Fact]
    public async Task Submit_EmptyCode_ReturnsValidation()
    {
        var (_, hackathon, ana) = await ActiveIndividualAsync();

        var result = await _service.SubmitAsync(ana.Id, hackathon.Id, Code("   "), CancellationToken.None);

        Assert.Equal("Envie algum código.", result.Message);
        Assert.False(_dbContext.Submissions.Any());
    }

    [Theory]
    [InlineData(102400, true)]
    [InlineData(102401, false)]
    public async Task Submit_SizeLimit_Boundary(int length, bool accepted)
    {
        var (_, hackathon, ana) = await ActiveIndividualAsync();

        var result = await _service.SubmitAsync(ana.Id, hackathon.Id, Code(new string('a', length)), CancellationToken.None);

        Assert.Equal(accepted, result.Succeeded);
        if (!accepted) Assert.Equal("O código deve ter no máximo 100 KB.", result.Message);
    }

    [Fact]
    public async Task Submit_ConcurrentVersion_ReturnsConflictAndDeletesDocument()
    {
        var (_, hackathon, ana) = await ActiveIndividualAsync();
        await SubmitAsync(ana, hackathon, Code("v1"));
        var submissionId = _dbContext.Submissions.Single().Id;
        // outra requisição enviou uma versão depois que este contexto leu a entrega
        await using (var other = TestDb.CreateShared(_databaseName))
        {
            other.Submissions.Single(item => item.Id == submissionId).CurrentVersion = 2;
            await other.SaveChangesAsync();
        }

        var result = await _service.SubmitAsync(ana.Id, hackathon.Id, Code("v2"), CancellationToken.None);

        Assert.Equal(SubmissionServiceError.Conflict, result.Error);
        Assert.Equal("Outra versão foi enviada ao mesmo tempo. Tente novamente.", result.Message);
        Assert.Equal(["v1"], _store.Documents.Values.Select(item => item.Code));
    }

    [Fact]
    public async Task Submit_AfterOrphanDocument_Succeeds()
    {
        var (_, hackathon, ana) = await ActiveIndividualAsync();
        await SubmitAsync(ana, hackathon, Code("v1"));
        // envio que caiu entre o MongoDB e o PostgreSQL
        await _store.InsertAsync(_dbContext.Submissions.Single().Id, "orfao", CancellationToken.None);

        var version = await SubmitAsync(ana, hackathon, Code("v2"));
        var mine = (await _service.GetMineAsync(ana.Id, hackathon.Id, CancellationToken.None)).Value!;

        Assert.Equal(2, version.Versao);
        Assert.Equal(2, mine.Versoes.Count);
        Assert.Equal("v2", mine.Atual!.Codigo);
    }

    [Fact]
    public async Task GetMyVersion_Existing_ReturnsCode()
    {
        var (_, hackathon, ana) = await ActiveIndividualAsync();
        await SubmitAsync(ana, hackathon, Code("v1"));
        await SubmitAsync(ana, hackathon, Code("v2"));

        var result = await _service.GetMyVersionAsync(ana.Id, hackathon.Id, 1, CancellationToken.None);

        Assert.Equal("v1", result.Value!.Codigo);
    }

    [Fact]
    public async Task GetMyVersion_Unknown_ReturnsNotFound()
    {
        var (_, hackathon, ana) = await ActiveIndividualAsync();
        await SubmitAsync(ana, hackathon);

        var result = await _service.GetMyVersionAsync(ana.Id, hackathon.Id, 7, CancellationToken.None);

        Assert.Equal(SubmissionServiceError.NotFound, result.Error);
        Assert.Equal("Versão não encontrada.", result.Message);
    }

    [Fact]
    public async Task GetMine_NoSubmission_ReturnsEmpty()
    {
        var (_, hackathon, _, ana, _) = await ActiveTeamAsync();

        var mine = (await _service.GetMineAsync(ana.Id, hackathon.Id, CancellationToken.None)).Value!;

        Assert.Empty(mine.Versoes);
        Assert.Null(mine.Atual);
        Assert.Equal("Os Rápidos", mine.Equipe);
    }

    [Fact]
    public async Task GetEntries_ListsTeamsWithAndWithoutSubmission()
    {
        var (company, hackathon, _, ana, _) = await ActiveTeamAsync();
        var carla = await CompanySeed.ParticipantAsync(_dbContext, "carla@exemplo.com", "Carla Dias");
        var other = new HackathonTeam { HackathonId = hackathon.Id, CreatedByAccountId = carla.Id, Name = "Turma do Bit", NormalizedName = "TURMA DO BIT", MemberCount = 1 };
        _dbContext.HackathonTeams.Add(other);
        await RegisterAsync(hackathon, carla, other);
        await SubmitAsync(ana, hackathon);

        var result = await _service.GetEntriesAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(["Os Rápidos", "Turma do Bit"], result.Value!.Select(item => item.Nome));
        Assert.Equal(1, result.Value[0].VersaoAtual);
        Assert.Null(result.Value[1].SubmissionId);
    }

    [Fact]
    public async Task GetEntries_Individual_ListsParticipants()
    {
        var (company, hackathon, ana) = await ActiveIndividualAsync();
        await SubmitAsync(ana, hackathon);

        var result = await _service.GetEntriesAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        var entry = Assert.Single(result.Value!);
        Assert.Equal("Ana Souza", entry.Nome);
        Assert.Equal(1, entry.VersaoAtual);
    }

    [Fact]
    public async Task GetEntries_OtherCompany_ReturnsNotFound()
    {
        var (_, hackathon, _) = await ActiveIndividualAsync();
        var other = await CompanySeed.CompanyAsync(_dbContext, "outra@exemplo.com", CompanyVerificationStatus.Verified);

        var result = await _service.GetEntriesAsync(other.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.Equal(SubmissionServiceError.NotFound, result.Error);
        Assert.Equal("Hackathon não encontrado.", result.Message);
    }

    [Fact]
    public async Task GetEntries_Participant_ReturnsForbidden()
    {
        var (_, hackathon, ana) = await ActiveIndividualAsync();

        var result = await _service.GetEntriesAsync(ana.Id, hackathon.Id, CancellationToken.None);

        Assert.Equal(SubmissionServiceError.Forbidden, result.Error);
        Assert.Equal("Apenas empresas e seus funcionários podem gerenciar Hackathons.", result.Message);
    }

    [Fact]
    public async Task GetEntry_ReturnsVersionsAndCurrentCode()
    {
        var (company, hackathon, ana) = await ActiveIndividualAsync();
        await SubmitAsync(ana, hackathon, Code("v1"));
        await SubmitAsync(ana, hackathon, Code("v2"));
        var submissionId = _dbContext.Submissions.Single().Id;

        var result = await _service.GetEntryAsync(company.PrimaryAccountId, hackathon.Id, submissionId, CancellationToken.None);

        Assert.Equal("Ana Souza", result.Value!.Nome);
        Assert.Equal(2, result.Value.Versoes.Count);
        Assert.Equal("v2", result.Value.Atual!.Codigo);
    }

    [Fact]
    public async Task GetEntryVersion_ReturnsOldCode()
    {
        var (company, hackathon, ana) = await ActiveIndividualAsync();
        await SubmitAsync(ana, hackathon, Code("v1"));
        await SubmitAsync(ana, hackathon, Code("v2"));
        var submissionId = _dbContext.Submissions.Single().Id;

        var result = await _service.GetEntryVersionAsync(company.PrimaryAccountId, hackathon.Id, submissionId, 1, CancellationToken.None);

        Assert.Equal("v1", result.Value!.Codigo);
    }

    [Fact]
    public async Task GetReference_ReturnsCompanyCode()
    {
        var (company, hackathon, _) = await ActiveIndividualAsync();
        hackathon.ReferenceLanguage = "Python";
        hackathon.ReferenceCode = "def resposta(): pass";
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetReferenceAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.Equal("Python", result.Value!.Linguagem);
        Assert.Equal("def resposta(): pass", result.Value.Codigo);
    }
}
