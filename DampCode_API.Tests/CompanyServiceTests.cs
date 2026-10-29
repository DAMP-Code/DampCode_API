using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using DampCode_API.Services;

namespace DampCode_API.Tests;

public sealed class CompanyServiceTests
{
    private readonly DampCodeDbContext _dbContext = TestDb.Create();
    private readonly CompanyService _service;

    public CompanyServiceTests() => _service = new CompanyService(_dbContext, new CompanyAccessService(_dbContext));

    private static InviteEmployeeDto Invite(string email = "maria@exemplo.com", string? jobTitle = "Desenvolvedora") =>
        new() { Email = email, JobTitle = jobTitle };

    private async Task<(Company Company, Account Participant)> CompanyAndParticipantAsync()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var participant = await CompanySeed.ParticipantAsync(_dbContext, "maria@exemplo.com");
        return (company, participant);
    }

    private async Task<CompanyInvitation> InviteAsync(Company company, string email = "maria@exemplo.com")
    {
        var result = await _service.InviteEmployeeAsync(company.PrimaryAccountId, Invite(email), CancellationToken.None);
        Assert.True(result.Succeeded, result.Message);
        return _dbContext.CompanyInvitations.Single(item => item.Id == result.Value!.Id);
    }

    [Fact]
    public async Task CompanyInvitation_PersistsWithCompany()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        _dbContext.CompanyInvitations.Add(new CompanyInvitation
        {
            CompanyId = company.Id,
            InvitedByAccountId = company.PrimaryAccountId,
            Email = "maria@exemplo.com",
            NormalizedEmail = "MARIA@EXEMPLO.COM",
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await _dbContext.SaveChangesAsync();

        Assert.Equal(CompanyInvitationStatus.Pending, _dbContext.CompanyInvitations.Single().Status);
    }

    [Fact]
    public async Task InviteEmployee_ValidParticipant_CreatesPendingInvitationFor24Hours()
    {
        var (company, _) = await CompanyAndParticipantAsync();

        var result = await _service.InviteEmployeeAsync(company.PrimaryAccountId, Invite(), CancellationToken.None);

        Assert.True(result.Succeeded);
        var invitation = _dbContext.CompanyInvitations.Single();
        Assert.Equal(CompanyInvitationStatus.Pending, invitation.Status);
        Assert.Equal(TimeSpan.FromHours(24), invitation.ExpiresAt - invitation.CreatedAt);
        Assert.Equal("MARIA@EXEMPLO.COM", invitation.NormalizedEmail);
        Assert.Equal("Desenvolvedora", invitation.JobTitle);
        Assert.Equal(company.Id, invitation.CompanyId);
        Assert.Equal(invitation.Id, result.Value!.Id);
    }

    [Fact]
    public async Task InviteEmployee_EmailWithDifferentCaseAndSpaces_FindsAccount()
    {
        var (company, _) = await CompanyAndParticipantAsync();

        var result = await _service.InviteEmployeeAsync(company.PrimaryAccountId, Invite(" Maria@Exemplo.com "), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Maria@Exemplo.com", _dbContext.CompanyInvitations.Single().Email);
    }

    [Fact]
    public async Task InviteEmployee_NotPrimaryAccount_ReturnsForbidden()
    {
        var (_, participant) = await CompanyAndParticipantAsync();
        await CompanySeed.ParticipantAsync(_dbContext, "joao@exemplo.com");

        var result = await _service.InviteEmployeeAsync(participant.Id, Invite("joao@exemplo.com"), CancellationToken.None);

        Assert.Equal(CompanyServiceError.Forbidden, result.Error);
        Assert.Equal("Apenas a conta principal da empresa pode convidar funcionários.", result.Message);
    }

    [Fact]
    public async Task InviteEmployee_OwnEmail_ReturnsValidation()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext, "empresa@exemplo.com");

        var result = await _service.InviteEmployeeAsync(company.PrimaryAccountId, Invite("EMPRESA@exemplo.com"), CancellationToken.None);

        Assert.Equal(CompanyServiceError.Validation, result.Error);
        Assert.Equal("Você não pode convidar a própria conta.", result.Message);
    }

    [Fact]
    public async Task InviteEmployee_EmailWithoutAccount_ReturnsNotFound()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);

        var result = await _service.InviteEmployeeAsync(company.PrimaryAccountId, Invite("ninguem@exemplo.com"), CancellationToken.None);

        Assert.Equal(CompanyServiceError.NotFound, result.Error);
        Assert.Equal("Nenhum participante encontrado com este e-mail. Peça para a pessoa criar a conta primeiro.", result.Message);
        Assert.Empty(_dbContext.CompanyInvitations);
    }

    [Fact]
    public async Task InviteEmployee_CompanyAccountEmail_ReturnsConflict()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext, "empresa@exemplo.com");
        await CompanySeed.CompanyAsync(_dbContext, "outra@exemplo.com");

        var result = await _service.InviteEmployeeAsync(company.PrimaryAccountId, Invite("outra@exemplo.com"), CancellationToken.None);

        Assert.Equal(CompanyServiceError.Conflict, result.Error);
        Assert.Equal("Este e-mail pertence a uma conta que não pode ser funcionária.", result.Message);
    }

    [Fact]
    public async Task InviteEmployee_AlreadyActiveEmployee_ReturnsConflict()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        _dbContext.CompanyMemberships.Add(new CompanyMembership { CompanyId = company.Id, AccountId = participant.Id });
        await _dbContext.SaveChangesAsync();

        var result = await _service.InviteEmployeeAsync(company.PrimaryAccountId, Invite(), CancellationToken.None);

        Assert.Equal(CompanyServiceError.Conflict, result.Error);
        Assert.Equal("Esta pessoa já é funcionária da empresa.", result.Message);
    }

    [Fact]
    public async Task InviteEmployee_PendingInvitationExists_ReturnsConflict()
    {
        var (company, _) = await CompanyAndParticipantAsync();
        await InviteAsync(company);

        var result = await _service.InviteEmployeeAsync(company.PrimaryAccountId, Invite(), CancellationToken.None);

        Assert.Equal(CompanyServiceError.Conflict, result.Error);
        Assert.Equal("Já existe um convite pendente para este e-mail.", result.Message);
    }

    [Fact]
    public async Task InviteEmployee_PreviousInvitationExpired_CreatesNewInvitation()
    {
        var (company, _) = await CompanyAndParticipantAsync();
        var previous = await InviteAsync(company);
        previous.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _dbContext.SaveChangesAsync();

        var result = await _service.InviteEmployeeAsync(company.PrimaryAccountId, Invite(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, _dbContext.CompanyInvitations.Count());
    }

    [Fact]
    public async Task GetSentInvitations_ReturnsOnlyPendingNotExpired()
    {
        var (company, _) = await CompanyAndParticipantAsync();
        await CompanySeed.ParticipantAsync(_dbContext, "joao@exemplo.com");
        await CompanySeed.ParticipantAsync(_dbContext, "lia@exemplo.com");
        var pending = await InviteAsync(company);
        var expired = await InviteAsync(company, "joao@exemplo.com");
        expired.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        var revoked = await InviteAsync(company, "lia@exemplo.com");
        revoked.Status = CompanyInvitationStatus.Revoked;
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetSentInvitationsAsync(company.PrimaryAccountId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(pending.Id, Assert.Single(result.Value!).Id);
    }

    [Fact]
    public async Task RevokeInvitation_Pending_SetsRevokedAndRespondedAt()
    {
        var (company, _) = await CompanyAndParticipantAsync();
        var invitation = await InviteAsync(company);

        var result = await _service.RevokeInvitationAsync(company.PrimaryAccountId, invitation.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(CompanyInvitationStatus.Revoked, invitation.Status);
        Assert.NotNull(invitation.RespondedAt);
    }

    [Fact]
    public async Task RevokeInvitation_OtherCompany_ReturnsNotFound()
    {
        var (company, _) = await CompanyAndParticipantAsync();
        var other = await CompanySeed.CompanyAsync(_dbContext, "outra@exemplo.com");
        var invitation = await InviteAsync(company);

        var result = await _service.RevokeInvitationAsync(other.PrimaryAccountId, invitation.Id, CancellationToken.None);

        Assert.Equal(CompanyServiceError.NotFound, result.Error);
        Assert.Equal(CompanyInvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public async Task RevokeInvitation_AlreadyAnswered_ReturnsConflict()
    {
        var (company, _) = await CompanyAndParticipantAsync();
        var invitation = await InviteAsync(company);
        invitation.Status = CompanyInvitationStatus.Declined;
        await _dbContext.SaveChangesAsync();

        var result = await _service.RevokeInvitationAsync(company.PrimaryAccountId, invitation.Id, CancellationToken.None);

        Assert.Equal(CompanyServiceError.Conflict, result.Error);
        Assert.Equal("Este convite já foi respondido.", result.Message);
    }

    [Fact]
    public async Task GetReceivedInvitations_ReturnsPendingNotExpiredForAccountEmail()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        var other = await CompanySeed.CompanyAsync(_dbContext, "outra@exemplo.com");
        var third = await CompanySeed.CompanyAsync(_dbContext, "terceira@exemplo.com");
        await CompanySeed.ParticipantAsync(_dbContext, "joao@exemplo.com");
        var pending = await InviteAsync(company);
        var expired = await InviteAsync(other);
        expired.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        var declined = await InviteAsync(third);
        declined.Status = CompanyInvitationStatus.Declined;
        await InviteAsync(company, "joao@exemplo.com");
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetReceivedInvitationsAsync(participant.Id, CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(pending.Id, item.Id);
        Assert.Equal("Empresa Exemplo", item.CompanyName);
        Assert.Equal("Desenvolvedora", item.JobTitle);
    }

    [Fact]
    public async Task AcceptInvitation_RegisteredInCompanyHackathon_ReturnsConflict()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Individual);
        _dbContext.Participations.Add(new Participation { HackathonId = hackathon.Id, AccountId = participant.Id });
        await _dbContext.SaveChangesAsync();
        var invitation = await InviteAsync(company);

        var result = await _service.AcceptInvitationAsync(participant.Id, invitation.Id, CancellationToken.None);

        Assert.Equal(CompanyServiceError.Conflict, result.Error);
        Assert.Equal("Você está inscrito em um Hackathon desta empresa. Saia da inscrição antes de aceitar.", result.Message);
        Assert.False(_dbContext.CompanyMemberships.Any());
    }

    [Fact]
    public async Task AcceptInvitation_CancelsPendingTeamItemsInCompanyHackathons()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Team);
        var owner = await CompanySeed.ParticipantAsync(_dbContext, "dona@exemplo.com");
        var team = new HackathonTeam { HackathonId = hackathon.Id, CreatedByAccountId = owner.Id, Name = "Equipe", NormalizedName = "EQUIPE", IsOpen = true, MemberCount = 1 };
        _dbContext.HackathonTeams.Add(team);
        _dbContext.TeamInvitations.Add(new TeamInvitation
        {
            TeamId = team.Id, InvitedByAccountId = owner.Id, Email = "maria@exemplo.com", NormalizedEmail = "MARIA@EXEMPLO.COM",
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        _dbContext.TeamJoinRequests.Add(new TeamJoinRequest { TeamId = team.Id, AccountId = participant.Id });
        await _dbContext.SaveChangesAsync();
        var invitation = await InviteAsync(company);

        var result = await _service.AcceptInvitationAsync(participant.Id, invitation.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(TeamInvitationStatus.Cancelled, _dbContext.TeamInvitations.Single().Status);
        Assert.Equal(TeamJoinRequestStatus.Cancelled, _dbContext.TeamJoinRequests.Single().Status);
    }

    [Fact]
    public async Task AcceptInvitation_RegisteredInClosedCompanyHackathon_Succeeds()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Individual,
            DateTime.UtcNow.AddDays(-10));
        _dbContext.Participations.Add(new Participation { HackathonId = hackathon.Id, AccountId = participant.Id });
        await _dbContext.SaveChangesAsync();
        var invitation = await InviteAsync(company);

        var result = await _service.AcceptInvitationAsync(participant.Id, invitation.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
    }

    [Fact]
    public async Task AcceptInvitation_Valid_CreatesMembershipAndMarksAccepted()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        var invitation = await InviteAsync(company);

        var result = await _service.AcceptInvitationAsync(participant.Id, invitation.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.IsPrimaryAccount);
        Assert.Equal(company.Id, result.Value.CompanyId);
        Assert.Equal(CompanyInvitationStatus.Accepted, invitation.Status);
        Assert.NotNull(invitation.RespondedAt);
        var membership = _dbContext.CompanyMemberships.Single();
        Assert.Equal(participant.Id, membership.AccountId);
        Assert.Equal("Desenvolvedora", membership.JobTitle);
        Assert.Null(membership.EndedAt);
    }

    [Fact]
    public async Task AcceptInvitation_OtherAccountEmail_ReturnsNotFound()
    {
        var (company, _) = await CompanyAndParticipantAsync();
        var intruder = await CompanySeed.ParticipantAsync(_dbContext, "joao@exemplo.com");
        var invitation = await InviteAsync(company);

        var result = await _service.AcceptInvitationAsync(intruder.Id, invitation.Id, CancellationToken.None);

        Assert.Equal(CompanyServiceError.NotFound, result.Error);
        Assert.Equal("Convite não encontrado.", result.Message);
        Assert.Empty(_dbContext.CompanyMemberships);
    }

    [Fact]
    public async Task AcceptInvitation_Expired_ReturnsExpired()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        var invitation = await InviteAsync(company);
        invitation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _dbContext.SaveChangesAsync();

        var result = await _service.AcceptInvitationAsync(participant.Id, invitation.Id, CancellationToken.None);

        Assert.Equal(CompanyServiceError.Expired, result.Error);
        Assert.Equal("Este convite expirou.", result.Message);
        Assert.Empty(_dbContext.CompanyMemberships);
    }

    [Fact]
    public async Task AcceptInvitation_DeletedCompany_ReturnsNotFound()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        var invitation = await InviteAsync(company);
        company.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var result = await _service.AcceptInvitationAsync(participant.Id, invitation.Id, CancellationToken.None);

        Assert.Equal(CompanyServiceError.NotFound, result.Error);
        Assert.Equal("Convite não encontrado.", result.Message);
        Assert.Empty(_dbContext.CompanyMemberships);
    }

    [Fact]
    public async Task AcceptInvitation_AlreadyAnswered_ReturnsConflict()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        var invitation = await InviteAsync(company);
        invitation.Status = CompanyInvitationStatus.Revoked;
        await _dbContext.SaveChangesAsync();

        var result = await _service.AcceptInvitationAsync(participant.Id, invitation.Id, CancellationToken.None);

        Assert.Equal(CompanyServiceError.Conflict, result.Error);
        Assert.Equal("Este convite já foi respondido.", result.Message);
    }

    [Fact]
    public async Task AcceptInvitation_AlreadyEmployeeElsewhere_ReturnsConflict()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        var other = await CompanySeed.CompanyAsync(_dbContext, "outra@exemplo.com");
        _dbContext.CompanyMemberships.Add(new CompanyMembership { CompanyId = other.Id, AccountId = participant.Id });
        await _dbContext.SaveChangesAsync();
        var invitation = await InviteAsync(company);

        var result = await _service.AcceptInvitationAsync(participant.Id, invitation.Id, CancellationToken.None);

        Assert.Equal(CompanyServiceError.Conflict, result.Error);
        Assert.Equal("Você já está vinculado a uma empresa.", result.Message);
        Assert.Equal(CompanyInvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public async Task AcceptInvitation_AfterRemoval_CreatesSecondMembership()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        Assert.True((await _service.AcceptInvitationAsync(participant.Id, (await InviteAsync(company)).Id, CancellationToken.None)).Succeeded);
        Assert.True((await _service.RemoveEmployeeAsync(company.PrimaryAccountId, participant.Id, CancellationToken.None)).Succeeded);

        var result = await _service.AcceptInvitationAsync(participant.Id, (await InviteAsync(company)).Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var memberships = _dbContext.CompanyMemberships.Where(item => item.AccountId == participant.Id).ToList();
        Assert.Equal(2, memberships.Count);
        Assert.Single(memberships, item => item.EndedAt == null);
    }

    [Fact]
    public async Task DeclineInvitation_Valid_MarksDeclined()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        var invitation = await InviteAsync(company);

        var result = await _service.DeclineInvitationAsync(participant.Id, invitation.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(CompanyInvitationStatus.Declined, invitation.Status);
        Assert.NotNull(invitation.RespondedAt);
        Assert.Empty(_dbContext.CompanyMemberships);
    }

    [Fact]
    public async Task GetEmployees_PrimaryOrEmployee_ReturnsActiveOnly()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        var former = await CompanySeed.ParticipantAsync(_dbContext, "joao@exemplo.com", "João Alves");
        _dbContext.CompanyMemberships.Add(new CompanyMembership { CompanyId = company.Id, AccountId = former.Id, EndedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        await _service.AcceptInvitationAsync(participant.Id, (await InviteAsync(company)).Id, CancellationToken.None);

        var byPrimary = await _service.GetEmployeesAsync(company.PrimaryAccountId, CancellationToken.None);
        var byEmployee = await _service.GetEmployeesAsync(participant.Id, CancellationToken.None);

        var employee = Assert.Single(byPrimary.Value!);
        Assert.Equal(participant.Id, employee.AccountId);
        Assert.Equal("Maria Lima", employee.Name);
        Assert.Equal("maria@exemplo.com", employee.Email);
        Assert.Equal("Desenvolvedora", employee.JobTitle);
        Assert.Equal(participant.Id, Assert.Single(byEmployee.Value!).AccountId);
    }

    [Fact]
    public async Task GetEmployees_NoCompany_ReturnsNotFound()
    {
        var participant = await CompanySeed.ParticipantAsync(_dbContext, "maria@exemplo.com");

        var result = await _service.GetEmployeesAsync(participant.Id, CancellationToken.None);

        Assert.Equal(CompanyServiceError.NotFound, result.Error);
    }

    [Fact]
    public async Task RemoveEmployee_Primary_SetsEndedAt()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        await _service.AcceptInvitationAsync(participant.Id, (await InviteAsync(company)).Id, CancellationToken.None);

        var result = await _service.RemoveEmployeeAsync(company.PrimaryAccountId, participant.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(_dbContext.CompanyMemberships.Single().EndedAt);
    }

    [Fact]
    public async Task RemoveEmployee_NotEmployee_ReturnsNotFound()
    {
        var (company, participant) = await CompanyAndParticipantAsync();

        var result = await _service.RemoveEmployeeAsync(company.PrimaryAccountId, participant.Id, CancellationToken.None);

        Assert.Equal(CompanyServiceError.NotFound, result.Error);
    }

    [Fact]
    public async Task RemoveEmployee_ByEmployee_ReturnsForbidden()
    {
        var (company, participant) = await CompanyAndParticipantAsync();
        await _service.AcceptInvitationAsync(participant.Id, (await InviteAsync(company)).Id, CancellationToken.None);

        var result = await _service.RemoveEmployeeAsync(participant.Id, participant.Id, CancellationToken.None);

        Assert.Equal(CompanyServiceError.Forbidden, result.Error);
        Assert.Null(_dbContext.CompanyMemberships.Single().EndedAt);
    }

    [Fact]
    public async Task GetPendingVerifications_ReturnsOnlyPending()
    {
        var pending = await CompanySeed.CompanyAsync(_dbContext, "pendente@exemplo.com");
        await CompanySeed.CompanyAsync(_dbContext, "verificada@exemplo.com", CompanyVerificationStatus.Verified);
        await CompanySeed.CompanyAsync(_dbContext, "rejeitada@exemplo.com", CompanyVerificationStatus.Rejected);

        var result = await _service.GetPendingVerificationsAsync(CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(pending.Id, item.Id);
        Assert.Equal(pending.Cnpj, item.Cnpj);
    }

    [Fact]
    public async Task UpdateVerification_Verified_ChangesStatus()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);

        var result = await _service.UpdateVerificationAsync(company.Id, new UpdateVerificationDto { Status = "Verified" }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(CompanyVerificationStatus.Verified, company.VerificationStatus);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("x")]
    public async Task UpdateVerification_InvalidStatus_ReturnsValidation(string status)
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);

        var result = await _service.UpdateVerificationAsync(company.Id, new UpdateVerificationDto { Status = status }, CancellationToken.None);

        Assert.Equal(CompanyServiceError.Validation, result.Error);
        Assert.Equal(CompanyVerificationStatus.Pending, company.VerificationStatus);
    }

    [Fact]
    public async Task UpdateVerification_UnknownCompany_ReturnsNotFound()
    {
        var result = await _service.UpdateVerificationAsync(Guid.NewGuid(), new UpdateVerificationDto { Status = "Verified" }, CancellationToken.None);

        Assert.Equal(CompanyServiceError.NotFound, result.Error);
    }
}
