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
}
