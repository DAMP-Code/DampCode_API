using DampCode_API.Data;
using DampCode_API.Models;
using DampCode_API.Services;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Tests;

public sealed class ParticipationServiceTests
{
    private readonly DampCodeDbContext _dbContext = TestDb.Create();
    private readonly ParticipationService _service;

    public ParticipationServiceTests() => _service = new ParticipationService(_dbContext, new CompanyAccessService(_dbContext));

    private Task<Company> OrganizerAsync(string email = "empresa@exemplo.com") =>
        CompanySeed.CompanyAsync(_dbContext, email, CompanyVerificationStatus.Verified);

    private async Task<(Hackathon Hackathon, Account Ana)> IndividualAsync(DateTime? startsAt = null)
    {
        var company = await OrganizerAsync();
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Individual, startsAt);
        var ana = await CompanySeed.ParticipantAsync(_dbContext, "ana@exemplo.com", "Ana Souza");
        return (hackathon, ana);
    }

    private async Task AddMembershipAsync(Company company, Account account)
    {
        _dbContext.CompanyMemberships.Add(new CompanyMembership { CompanyId = company.Id, AccountId = account.Id });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Participation_PersistsWithTeam()
    {
        var company = await OrganizerAsync();
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Team);
        var participant = await CompanySeed.ParticipantAsync(_dbContext, "ana@exemplo.com");
        var team = new HackathonTeam
        {
            HackathonId = hackathon.Id,
            CreatedByAccountId = participant.Id,
            Name = "Os Rápidos",
            NormalizedName = "OS RÁPIDOS",
            MemberCount = 1
        };
        _dbContext.HackathonTeams.Add(team);
        _dbContext.Participations.Add(new Participation { HackathonId = hackathon.Id, AccountId = participant.Id, TeamId = team.Id });
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.Participations.Include(item => item.Team).SingleAsync();
        Assert.Equal("Os Rápidos", saved.Team!.Name);
        Assert.Null(saved.LeftAt);
    }

    [Fact]
    public async Task Register_Individual_CreatesParticipation()
    {
        var (hackathon, ana) = await IndividualAsync();

        var result = await _service.RegisterAsync(ana.Id, hackathon.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Null(result.Value!.Equipe);
        Assert.Equal("Publicado", result.Value.Fase);
        Assert.Equal("Individual", result.Value.Formato);
        Assert.Single(_dbContext.Participations.Where(item => item.AccountId == ana.Id && item.LeftAt == null));
    }

    [Fact]
    public async Task Register_Twice_ReturnsConflict()
    {
        var (hackathon, ana) = await IndividualAsync();
        await _service.RegisterAsync(ana.Id, hackathon.Id, CancellationToken.None);

        var result = await _service.RegisterAsync(ana.Id, hackathon.Id, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Você já está inscrito neste Hackathon.", result.Message);
    }

    [Fact]
    public async Task Register_TeamHackathon_ReturnsConflict()
    {
        var company = await OrganizerAsync();
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Team);
        var ana = await CompanySeed.ParticipantAsync(_dbContext, "ana@exemplo.com");

        var result = await _service.RegisterAsync(ana.Id, hackathon.Id, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Este Hackathon é em equipe. Crie uma equipe ou entre em uma.", result.Message);
    }

    [Fact]
    public async Task Register_ActivePhase_ReturnsConflict()
    {
        var (hackathon, ana) = await IndividualAsync(DateTime.UtcNow.AddDays(-1));

        var result = await _service.RegisterAsync(ana.Id, hackathon.Id, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("As inscrições deste Hackathon estão encerradas.", result.Message);
    }

    [Fact]
    public async Task Register_CompanyAccount_ReturnsForbidden()
    {
        var (hackathon, _) = await IndividualAsync();
        var other = await OrganizerAsync("outra@exemplo.com");

        var result = await _service.RegisterAsync(other.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Forbidden, result.Error);
        Assert.Equal("Apenas participantes podem se inscrever em Hackathons.", result.Message);
    }

    [Fact]
    public async Task Register_EmployeeOfOrganizer_ReturnsForbidden()
    {
        var (hackathon, ana) = await IndividualAsync();
        await AddMembershipAsync(_dbContext.Companies.Single(item => item.Id == hackathon.CompanyId), ana);

        var result = await _service.RegisterAsync(ana.Id, hackathon.Id, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Forbidden, result.Error);
        Assert.Equal("Quem organiza o Hackathon não pode participar dele.", result.Message);
    }

    [Fact]
    public async Task Register_EmployeeOfOtherCompany_Succeeds()
    {
        var (hackathon, ana) = await IndividualAsync();
        await AddMembershipAsync(await OrganizerAsync("outra@exemplo.com"), ana);

        var result = await _service.RegisterAsync(ana.Id, hackathon.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
    }

    [Fact]
    public async Task Register_DraftHackathon_ReturnsNotFound()
    {
        var (hackathon, ana) = await IndividualAsync();
        hackathon.Status = HackathonStatus.Draft;
        await _dbContext.SaveChangesAsync();

        var result = await _service.RegisterAsync(ana.Id, hackathon.Id, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.NotFound, result.Error);
        Assert.Equal("Hackathon não encontrado.", result.Message);
    }

    [Fact]
    public async Task Cancel_Individual_SetsLeftAt()
    {
        var (hackathon, ana) = await IndividualAsync();
        await _service.RegisterAsync(ana.Id, hackathon.Id, CancellationToken.None);

        var result = await _service.CancelRegistrationAsync(ana.Id, hackathon.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(_dbContext.Participations.Single().LeftAt);
    }

    [Fact]
    public async Task Register_AfterCancel_RegistersAgain()
    {
        var (hackathon, ana) = await IndividualAsync();
        await _service.RegisterAsync(ana.Id, hackathon.Id, CancellationToken.None);
        await _service.CancelRegistrationAsync(ana.Id, hackathon.Id, CancellationToken.None);

        var result = await _service.RegisterAsync(ana.Id, hackathon.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(2, _dbContext.Participations.Count());
        Assert.Single(_dbContext.Participations.Where(item => item.LeftAt == null));
    }

    [Fact]
    public async Task Cancel_TeamHackathon_ReturnsConflict()
    {
        var company = await OrganizerAsync();
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Team);
        var ana = await CompanySeed.ParticipantAsync(_dbContext, "ana@exemplo.com");

        var result = await _service.CancelRegistrationAsync(ana.Id, hackathon.Id, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Use sair da equipe.", result.Message);
    }
}
