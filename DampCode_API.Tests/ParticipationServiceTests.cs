using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using DampCode_API.Services;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Tests;

public sealed class ParticipationServiceTests
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly DampCodeDbContext _dbContext;
    private readonly ParticipationService _service;

    public ParticipationServiceTests()
    {
        _dbContext = TestDb.CreateShared(_databaseName);
        _service = new ParticipationService(_dbContext, new CompanyAccessService(_dbContext));
    }

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

    private async Task<(Hackathon Hackathon, Account Ana, Account Bruno)> TeamHackathonAsync()
    {
        var company = await OrganizerAsync();
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Team);
        var ana = await CompanySeed.ParticipantAsync(_dbContext, "ana@exemplo.com", "Ana Souza");
        var bruno = await CompanySeed.ParticipantAsync(_dbContext, "bruno@exemplo.com", "Bruno Lima");
        return (hackathon, ana, bruno);
    }

    private async Task<MyTeamDto> CreateTeamAsync(Account owner, Hackathon hackathon, string name = "Os Rápidos", bool open = false)
    {
        var result = await _service.CreateTeamAsync(owner.Id, hackathon.Id, new CreateTeamDto { Nome = name, Aberta = open }, CancellationToken.None);
        Assert.True(result.Succeeded, result.Message);
        return result.Value!;
    }

    private async Task<TeamPendingInvitationDto> InviteAsync(Account member, MyTeamDto team, string email)
    {
        var result = await _service.InviteAsync(member.Id, team.TeamId, new InviteTeamMemberDto { Email = email }, CancellationToken.None);
        Assert.True(result.Succeeded, result.Message);
        return result.Value!;
    }

    // Completa a equipe com participantes extras gravados direto no banco.
    private async Task FillTeamAsync(MyTeamDto team, Hackathon hackathon, int members)
    {
        var entity = _dbContext.HackathonTeams.Single(item => item.Id == team.TeamId);
        for (var index = entity.MemberCount; index < members; index++)
        {
            var extra = await CompanySeed.ParticipantAsync(_dbContext, $"extra{index}@exemplo.com", $"Extra {index}");
            _dbContext.Participations.Add(new Participation { HackathonId = hackathon.Id, AccountId = extra.Id, TeamId = team.TeamId });
        }
        entity.MemberCount = members;
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateTeam_RegistersCreator()
    {
        var (hackathon, ana, _) = await TeamHackathonAsync();

        var team = await CreateTeamAsync(ana, hackathon);

        Assert.Equal("Os Rápidos", team.Nome);
        Assert.Equal(["Ana Souza"], team.Membros.Select(item => item.Nome));
        Assert.Equal(1, _dbContext.HackathonTeams.Single().MemberCount);
        Assert.Equal(team.TeamId, _dbContext.Participations.Single(item => item.AccountId == ana.Id).TeamId);
    }

    [Fact]
    public async Task CreateTeam_DuplicateNameIgnoringCase_ReturnsConflict()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        await CreateTeamAsync(ana, hackathon, "Os Rápidos");

        var result = await _service.CreateTeamAsync(bruno.Id, hackathon.Id, new CreateTeamDto { Nome = " os rápidos " }, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Já existe uma equipe com este nome neste Hackathon.", result.Message);
    }

    [Fact]
    public async Task CreateTeam_EmptyName_ReturnsValidation()
    {
        var (hackathon, ana, _) = await TeamHackathonAsync();

        var result = await _service.CreateTeamAsync(ana.Id, hackathon.Id, new CreateTeamDto { Nome = "  " }, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Validation, result.Error);
        Assert.Equal("Informe um nome de equipe com até 100 caracteres.", result.Message);
    }

    [Fact]
    public async Task CreateTeam_IndividualHackathon_ReturnsConflict()
    {
        var (hackathon, ana) = await IndividualAsync();

        var result = await _service.CreateTeamAsync(ana.Id, hackathon.Id, new CreateTeamDto { Nome = "Os Rápidos" }, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Este Hackathon é individual.", result.Message);
    }

    [Fact]
    public async Task UpdateTeam_ByMember_TogglesOpen()
    {
        var (hackathon, ana, _) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);

        var result = await _service.UpdateTeamAsync(ana.Id, team.TeamId, new UpdateTeamDto { Aberta = true }, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.Value!.Aberta);
        Assert.Equal("Os Rápidos", result.Value.Nome);
    }

    [Fact]
    public async Task UpdateTeam_ByNonMember_ReturnsNotFound()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);

        var result = await _service.UpdateTeamAsync(bruno.Id, team.TeamId, new UpdateTeamDto { Aberta = true }, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.NotFound, result.Error);
        Assert.Equal("Equipe não encontrada.", result.Message);
    }

    [Fact]
    public async Task Invite_EmailWithSpacesAndCase_FindsAccount()
    {
        var (hackathon, ana, _) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);

        var invitation = await InviteAsync(ana, team, " Bruno@Exemplo.com ");

        Assert.Equal("Bruno@Exemplo.com", invitation.Email);
        Assert.Equal("BRUNO@EXEMPLO.COM", _dbContext.TeamInvitations.Single().NormalizedEmail);
    }

    [Fact]
    public async Task Invite_UnknownEmail_ReturnsNotFound()
    {
        var (hackathon, ana, _) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);

        var result = await _service.InviteAsync(ana.Id, team.TeamId, new InviteTeamMemberDto { Email = "ninguem@exemplo.com" }, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.NotFound, result.Error);
        Assert.Equal("Nenhum participante encontrado com este e-mail.", result.Message);
    }

    [Fact]
    public async Task Invite_PendingTwice_ReturnsConflict()
    {
        var (hackathon, ana, _) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);
        await InviteAsync(ana, team, "bruno@exemplo.com");

        var result = await _service.InviteAsync(ana.Id, team.TeamId, new InviteTeamMemberDto { Email = "bruno@exemplo.com" }, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Já existe um convite pendente para este e-mail.", result.Message);
    }

    [Fact]
    public async Task Invite_TeamMember_ReturnsConflict()
    {
        var (hackathon, ana, _) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);

        var result = await _service.InviteAsync(ana.Id, team.TeamId, new InviteTeamMemberDto { Email = "ana@exemplo.com" }, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Esta pessoa já está na equipe.", result.Message);
    }

    [Fact]
    public async Task Invite_FullTeam_ReturnsConflict()
    {
        var (hackathon, ana, _) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);
        await FillTeamAsync(team, hackathon, 5);

        var result = await _service.InviteAsync(ana.Id, team.TeamId, new InviteTeamMemberDto { Email = "bruno@exemplo.com" }, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Esta equipe já está completa.", result.Message);
    }

    [Fact]
    public async Task AcceptInvitation_JoinsTeam()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);
        var invitation = await InviteAsync(ana, team, "bruno@exemplo.com");

        var result = await _service.AcceptInvitationAsync(bruno.Id, invitation.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(2, result.Value!.Equipe!.Membros.Count);
        Assert.Equal(2, _dbContext.HackathonTeams.Single().MemberCount);
        Assert.Equal(TeamInvitationStatus.Accepted, _dbContext.TeamInvitations.Single().Status);
    }

    [Fact]
    public async Task AcceptInvitation_Expired_ReturnsExpired()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);
        var invitation = await InviteAsync(ana, team, "bruno@exemplo.com");
        _dbContext.TeamInvitations.Single().ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _dbContext.SaveChangesAsync();

        var result = await _service.AcceptInvitationAsync(bruno.Id, invitation.Id, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Expired, result.Error);
        Assert.Equal("Este convite expirou.", result.Message);
    }

    [Fact]
    public async Task AcceptInvitation_OtherAccount_ReturnsNotFound()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);
        var invitation = await InviteAsync(ana, team, "bruno@exemplo.com");
        var carla = await CompanySeed.ParticipantAsync(_dbContext, "carla@exemplo.com");

        var result = await _service.AcceptInvitationAsync(carla.Id, invitation.Id, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.NotFound, result.Error);
        Assert.Equal("Convite não encontrado.", result.Message);
    }

    [Fact]
    public async Task AcceptInvitation_AlreadyInOtherTeam_ReturnsConflict()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);
        var invitation = await InviteAsync(ana, team, "bruno@exemplo.com");
        // Bruno cria a própria equipe depois de convidado: o convite é cancelado, então reabrimos para simular a corrida.
        await CreateTeamAsync(bruno, hackathon, "Outra equipe");
        _dbContext.TeamInvitations.Single().Status = TeamInvitationStatus.Pending;
        await _dbContext.SaveChangesAsync();

        var result = await _service.AcceptInvitationAsync(bruno.Id, invitation.Id, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Você já está inscrito neste Hackathon. Saia da equipe atual antes.", result.Message);
    }

    [Fact]
    public async Task AcceptInvitation_CancelsOtherPendingOfAccount()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var carla = await CompanySeed.ParticipantAsync(_dbContext, "carla@exemplo.com", "Carla Dias");
        var anaTeam = await CreateTeamAsync(ana, hackathon, "Equipe da Ana");
        var carlaTeam = await CreateTeamAsync(carla, hackathon, "Equipe da Carla");
        var fromAna = await InviteAsync(ana, anaTeam, "bruno@exemplo.com");
        var fromCarla = await InviteAsync(carla, carlaTeam, "bruno@exemplo.com");

        await _service.AcceptInvitationAsync(bruno.Id, fromAna.Id, CancellationToken.None);

        Assert.Equal(TeamInvitationStatus.Cancelled, _dbContext.TeamInvitations.Single(item => item.Id == fromCarla.Id).Status);
    }

    [Fact]
    public async Task AcceptInvitation_FillsTeam_CancelsTeamPending()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var carla = await CompanySeed.ParticipantAsync(_dbContext, "carla@exemplo.com");
        var team = await CreateTeamAsync(ana, hackathon);
        var toBruno = await InviteAsync(ana, team, "bruno@exemplo.com");
        var toCarla = await InviteAsync(ana, team, "carla@exemplo.com");
        await FillTeamAsync(team, hackathon, 4);

        var result = await _service.AcceptInvitationAsync(bruno.Id, toBruno.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(5, _dbContext.HackathonTeams.Single().MemberCount);
        Assert.Equal(TeamInvitationStatus.Cancelled, _dbContext.TeamInvitations.Single(item => item.Id == toCarla.Id).Status);
    }

    [Fact]
    public async Task RevokeInvitation_ByMember_Revokes()
    {
        var (hackathon, ana, _) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);
        var invitation = await InviteAsync(ana, team, "bruno@exemplo.com");

        var result = await _service.RevokeInvitationAsync(ana.Id, team.TeamId, invitation.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(TeamInvitationStatus.Revoked, _dbContext.TeamInvitations.Single().Status);
    }

    [Fact]
    public async Task DeclineInvitation_SetsDeclined()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);
        var invitation = await InviteAsync(ana, team, "bruno@exemplo.com");

        var result = await _service.DeclineInvitationAsync(bruno.Id, invitation.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(TeamInvitationStatus.Declined, _dbContext.TeamInvitations.Single().Status);
    }

    private async Task<TeamJoinRequestSentDto> RequestAsync(Account requester, MyTeamDto team)
    {
        var result = await _service.RequestToJoinAsync(requester.Id, team.TeamId, CancellationToken.None);
        Assert.True(result.Succeeded, result.Message);
        return result.Value!;
    }

    [Fact]
    public async Task RequestToJoin_OpenTeam_CreatesPending()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon, open: true);

        var request = await RequestAsync(bruno, team);

        Assert.Equal("Os Rápidos", request.Equipe);
        Assert.Equal(TeamJoinRequestStatus.Pending, _dbContext.TeamJoinRequests.Single().Status);
    }

    [Fact]
    public async Task RequestToJoin_ClosedTeam_ReturnsConflict()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);

        var result = await _service.RequestToJoinAsync(bruno.Id, team.TeamId, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Esta equipe não está aceitando pedidos.", result.Message);
    }

    [Fact]
    public async Task RequestToJoin_Twice_ReturnsConflict()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon, open: true);
        await RequestAsync(bruno, team);

        var result = await _service.RequestToJoinAsync(bruno.Id, team.TeamId, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Você já pediu para entrar nesta equipe.", result.Message);
    }

    [Fact]
    public async Task RequestToJoin_AlreadyRegistered_ReturnsConflict()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon, open: true);
        await CreateTeamAsync(bruno, hackathon, "Equipe do Bruno");

        var result = await _service.RequestToJoinAsync(bruno.Id, team.TeamId, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Você já está inscrito neste Hackathon.", result.Message);
    }

    [Fact]
    public async Task AcceptRequest_ByAnyMember_JoinsTeam()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var carla = await CompanySeed.ParticipantAsync(_dbContext, "carla@exemplo.com", "Carla Dias");
        var team = await CreateTeamAsync(ana, hackathon, open: true);
        var toBruno = await InviteAsync(ana, team, "bruno@exemplo.com");
        await _service.AcceptInvitationAsync(bruno.Id, toBruno.Id, CancellationToken.None);
        var request = await RequestAsync(carla, team);

        var result = await _service.AcceptRequestAsync(bruno.Id, request.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Carla Dias", result.Value!.Nome);
        var stored = _dbContext.TeamJoinRequests.Single();
        Assert.Equal(TeamJoinRequestStatus.Accepted, stored.Status);
        Assert.Equal(bruno.Id, stored.RespondedByAccountId);
        Assert.Equal(3, _dbContext.HackathonTeams.Single().MemberCount);
    }

    [Fact]
    public async Task AcceptRequest_ByNonMember_ReturnsNotFound()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var carla = await CompanySeed.ParticipantAsync(_dbContext, "carla@exemplo.com");
        var team = await CreateTeamAsync(ana, hackathon, open: true);
        var request = await RequestAsync(bruno, team);

        var result = await _service.AcceptRequestAsync(carla.Id, request.Id, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.NotFound, result.Error);
        Assert.Equal("Pedido não encontrado.", result.Message);
    }

    [Fact]
    public async Task AcceptRequest_FullTeam_ReturnsConflict()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon, open: true);
        var request = await RequestAsync(bruno, team);
        // completa a equipe sem passar pelo serviço, então o pedido continua pendente
        await FillTeamAsync(team, hackathon, 5);

        var result = await _service.AcceptRequestAsync(ana.Id, request.Id, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Esta equipe já está completa.", result.Message);
    }

    [Fact]
    public async Task AcceptRequest_TeamChangedConcurrently_ReturnsConflict()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon, open: true);
        var request = await RequestAsync(bruno, team);
        await FillTeamAsync(team, hackathon, 4);
        // o contexto do serviço já tem a equipe rastreada com 4 membros...
        Assert.Equal(4, _dbContext.HackathonTeams.Find(team.TeamId)!.MemberCount);
        // ...e outra requisição ocupa a última vaga antes do aceite
        await using (var other = TestDb.CreateShared(_databaseName))
        {
            other.HackathonTeams.Single(item => item.Id == team.TeamId).MemberCount = 5;
            await other.SaveChangesAsync();
        }

        var result = await _service.AcceptRequestAsync(ana.Id, request.Id, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("Esta equipe já está completa.", result.Message);
    }

    [Fact]
    public async Task CancelRequest_ByRequester_Cancels()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon, open: true);
        var request = await RequestAsync(bruno, team);

        var result = await _service.CancelRequestAsync(bruno.Id, request.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(TeamJoinRequestStatus.Cancelled, _dbContext.TeamJoinRequests.Single().Status);
    }

    [Fact]
    public async Task DeclineRequest_SetsDeclined()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon, open: true);
        var request = await RequestAsync(bruno, team);

        var result = await _service.DeclineRequestAsync(ana.Id, request.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var stored = _dbContext.TeamJoinRequests.Single();
        Assert.Equal(TeamJoinRequestStatus.Declined, stored.Status);
        Assert.Equal(ana.Id, stored.RespondedByAccountId);
    }

    [Fact]
    public async Task Leave_Member_TeamContinues()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);
        var invitation = await InviteAsync(ana, team, "bruno@exemplo.com");
        await _service.AcceptInvitationAsync(bruno.Id, invitation.Id, CancellationToken.None);

        var result = await _service.LeaveTeamAsync(ana.Id, team.TeamId, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var stored = _dbContext.HackathonTeams.Single();
        Assert.Equal(1, stored.MemberCount);
        Assert.NotNull(_dbContext.Participations.Single(item => item.AccountId == ana.Id).LeftAt);
    }

    [Fact]
    public async Task Leave_LastMember_DissolvesTeam()
    {
        var (hackathon, ana, _) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);
        await InviteAsync(ana, team, "bruno@exemplo.com");

        var result = await _service.LeaveTeamAsync(ana.Id, team.TeamId, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.False(_dbContext.HackathonTeams.Any());
        Assert.False(_dbContext.TeamInvitations.Any());
        var participation = _dbContext.Participations.Single();
        Assert.Null(participation.TeamId);
        Assert.NotNull(participation.LeftAt);
    }

    [Fact]
    public async Task Leave_ActivePhase_ReturnsConflict()
    {
        var (hackathon, ana, _) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);
        hackathon.StartsAt = DateTime.UtcNow.AddMinutes(-1);
        await _dbContext.SaveChangesAsync();

        var result = await _service.LeaveTeamAsync(ana.Id, team.TeamId, CancellationToken.None);

        Assert.Equal(ParticipationServiceError.Conflict, result.Error);
        Assert.Equal("As inscrições deste Hackathon estão encerradas.", result.Message);
    }

    [Fact]
    public async Task GetOpenTeams_ListsOnlyOpenWithVacancies()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var carla = await CompanySeed.ParticipantAsync(_dbContext, "carla@exemplo.com");
        var open = await CreateTeamAsync(ana, hackathon, "Aberta", open: true);
        await CreateTeamAsync(bruno, hackathon, "Fechada");
        var full = await CreateTeamAsync(carla, hackathon, "Cheia", open: true);
        await FillTeamAsync(full, hackathon, 5);

        var result = await _service.GetOpenTeamsAsync(hackathon.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var item = Assert.Single(result.Value!);
        Assert.Equal(open.TeamId, item.TeamId);
        Assert.Equal(1, item.Membros);
        Assert.Equal(4, item.Vagas);
    }

    [Fact]
    public async Task GetMine_ListsTeamWithMembersInvitesAndRequests()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var carla = await CompanySeed.ParticipantAsync(_dbContext, "carla@exemplo.com", "Carla Dias");
        var team = await CreateTeamAsync(ana, hackathon, open: true);
        await InviteAsync(ana, team, "bruno@exemplo.com");
        await RequestAsync(carla, team);

        var mine = await _service.GetMineAsync(ana.Id, CancellationToken.None);
        var invited = await _service.GetMineAsync(bruno.Id, CancellationToken.None);
        var requester = await _service.GetMineAsync(carla.Id, CancellationToken.None);

        var participation = Assert.Single(mine.Inscricoes);
        Assert.Equal("Equipe", participation.Formato);
        Assert.Equal(["Ana Souza"], participation.Equipe!.Membros.Select(item => item.Nome));
        Assert.Equal("bruno@exemplo.com", Assert.Single(participation.Equipe.ConvitesPendentes).Email);
        Assert.Equal("Carla Dias", Assert.Single(participation.Equipe.PedidosPendentes).Nome);
        var received = Assert.Single(invited.ConvitesRecebidos);
        Assert.Equal("Os Rápidos", received.Equipe);
        Assert.Equal("Ana Souza", received.ConvidadoPor);
        Assert.Empty(invited.Inscricoes);
        Assert.Equal("Os Rápidos", Assert.Single(requester.PedidosEnviados).Equipe);
    }

    [Fact]
    public async Task GetMine_HidesExpiredInvitations()
    {
        var (hackathon, ana, bruno) = await TeamHackathonAsync();
        var team = await CreateTeamAsync(ana, hackathon);
        await InviteAsync(ana, team, "bruno@exemplo.com");
        _dbContext.TeamInvitations.Single().ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _dbContext.SaveChangesAsync();

        var invited = await _service.GetMineAsync(bruno.Id, CancellationToken.None);
        var mine = await _service.GetMineAsync(ana.Id, CancellationToken.None);

        Assert.Empty(invited.ConvitesRecebidos);
        Assert.Empty(mine.Inscricoes.Single().Equipe!.ConvitesPendentes);
    }

    [Fact]
    public async Task GetMine_CancelledHackathon_StillListed()
    {
        var (hackathon, ana) = await IndividualAsync();
        await _service.RegisterAsync(ana.Id, hackathon.Id, CancellationToken.None);
        hackathon.Status = HackathonStatus.Cancelled;
        await _dbContext.SaveChangesAsync();

        var mine = await _service.GetMineAsync(ana.Id, CancellationToken.None);

        var participation = Assert.Single(mine.Inscricoes);
        Assert.Equal("Cancelado", participation.Fase);
        Assert.Null(participation.Equipe);
    }

    [Fact]
    public async Task GetMine_IgnoresLeftParticipations()
    {
        var (hackathon, ana) = await IndividualAsync();
        await _service.RegisterAsync(ana.Id, hackathon.Id, CancellationToken.None);
        await _service.CancelRegistrationAsync(ana.Id, hackathon.Id, CancellationToken.None);

        var mine = await _service.GetMineAsync(ana.Id, CancellationToken.None);

        Assert.Empty(mine.Inscricoes);
    }
}
