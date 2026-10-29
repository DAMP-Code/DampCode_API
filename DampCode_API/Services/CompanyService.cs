using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Services;

public sealed class CompanyService(DampCodeDbContext dbContext, ICompanyAccessService companyAccessService) : ICompanyService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromHours(24);

    public async Task<CompanyServiceResult<CompanyInvitationDto>> InviteEmployeeAsync(Guid accountId, InviteEmployeeDto dto, CancellationToken cancellationToken)
    {
        var company = await GetPrimaryCompanyAsync(accountId, cancellationToken);
        if (company is null) return Fail<CompanyInvitationDto>(CompanyServiceError.Forbidden, "Apenas a conta principal da empresa pode convidar funcionários.");

        var email = dto.Email.Trim();
        var normalizedEmail = Normalize(email);
        if (company.PrimaryAccount.NormalizedEmail == normalizedEmail)
            return Fail<CompanyInvitationDto>(CompanyServiceError.Validation, "Você não pode convidar a própria conta.");

        var invited = await dbContext.Accounts.AsNoTracking()
            .SingleOrDefaultAsync(account => account.NormalizedEmail == normalizedEmail && account.DeletedAt == null, cancellationToken);
        if (invited is null)
            return Fail<CompanyInvitationDto>(CompanyServiceError.NotFound, "Nenhum participante encontrado com este e-mail. Peça para a pessoa criar a conta primeiro.");
        if (invited.AccountType != AccountType.Participant)
            return Fail<CompanyInvitationDto>(CompanyServiceError.Conflict, "Este e-mail pertence a uma conta que não pode ser funcionária.");
        if (await dbContext.CompanyMemberships.AnyAsync(item => item.CompanyId == company.Id && item.AccountId == invited.Id && item.EndedAt == null, cancellationToken))
            return Fail<CompanyInvitationDto>(CompanyServiceError.Conflict, "Esta pessoa já é funcionária da empresa.");

        var now = DateTime.UtcNow;
        if (await dbContext.CompanyInvitations.AnyAsync(item => item.CompanyId == company.Id && item.NormalizedEmail == normalizedEmail
                && item.Status == CompanyInvitationStatus.Pending && item.ExpiresAt > now, cancellationToken))
            return Fail<CompanyInvitationDto>(CompanyServiceError.Conflict, "Já existe um convite pendente para este e-mail.");

        var invitation = new CompanyInvitation
        {
            CompanyId = company.Id,
            InvitedByAccountId = accountId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            JobTitle = string.IsNullOrWhiteSpace(dto.JobTitle) ? null : dto.JobTitle.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.Add(InvitationLifetime)
        };
        dbContext.CompanyInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, ToDto(invitation));
    }

    public async Task<CompanyServiceResult<IReadOnlyList<CompanyInvitationDto>>> GetSentInvitationsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var company = await GetPrimaryCompanyAsync(accountId, cancellationToken);
        if (company is null) return Fail<IReadOnlyList<CompanyInvitationDto>>(CompanyServiceError.Forbidden, "Apenas a conta principal da empresa pode ver os convites.");

        var now = DateTime.UtcNow;
        var invitations = await dbContext.CompanyInvitations.AsNoTracking()
            .Where(item => item.CompanyId == company.Id && item.Status == CompanyInvitationStatus.Pending && item.ExpiresAt > now)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new CompanyInvitationDto(item.Id, item.Email, item.JobTitle, item.ExpiresAt, item.CreatedAt))
            .ToListAsync(cancellationToken);
        return new(true, invitations);
    }

    public async Task<CompanyServiceResult<bool>> RevokeInvitationAsync(Guid accountId, Guid invitationId, CancellationToken cancellationToken)
    {
        var company = await GetPrimaryCompanyAsync(accountId, cancellationToken);
        if (company is null) return Fail<bool>(CompanyServiceError.Forbidden, "Apenas a conta principal da empresa pode revogar convites.");

        var invitation = await dbContext.CompanyInvitations
            .SingleOrDefaultAsync(item => item.Id == invitationId && item.CompanyId == company.Id, cancellationToken);
        if (invitation is null) return Fail<bool>(CompanyServiceError.NotFound, "Convite não encontrado.");
        if (invitation.Status != CompanyInvitationStatus.Pending) return Fail<bool>(CompanyServiceError.Conflict, "Este convite já foi respondido.");

        invitation.Status = CompanyInvitationStatus.Revoked;
        invitation.RespondedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, true);
    }

    public async Task<IReadOnlyList<ReceivedInvitationDto>> GetReceivedInvitationsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await GetActiveAccountAsync(accountId, cancellationToken);
        if (account is null || account.AccountType != AccountType.Participant) return [];

        var now = DateTime.UtcNow;
        return await dbContext.CompanyInvitations.AsNoTracking()
            .Where(item => item.NormalizedEmail == account.NormalizedEmail && item.Status == CompanyInvitationStatus.Pending
                && item.ExpiresAt > now && item.Company.DeletedAt == null)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new ReceivedInvitationDto(item.Id, item.Company.Name, item.JobTitle, item.ExpiresAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CompanyServiceResult<CompanyAccessDto>> AcceptInvitationAsync(Guid accountId, Guid invitationId, CancellationToken cancellationToken)
    {
        var (account, invitation, error) = await FindReceivedInvitationAsync<CompanyAccessDto>(accountId, invitationId, cancellationToken);
        if (error is not null) return error;
        if (await dbContext.CompanyMemberships.AnyAsync(item => item.AccountId == account!.Id && item.EndedAt == null, cancellationToken))
            return Fail<CompanyAccessDto>(CompanyServiceError.Conflict, "Você já está vinculado a uma empresa.");

        var now = DateTime.UtcNow;
        // Quem organiza não participa: inscrito em Hackathon ainda em andamento desta empresa não vira funcionário dela.
        var companyHackathons = await dbContext.Participations.AsNoTracking()
            .Where(item => item.AccountId == account.Id && item.LeftAt == null && item.Hackathon.CompanyId == invitation!.CompanyId)
            .Select(item => item.Hackathon)
            .ToListAsync(cancellationToken);
        if (companyHackathons.Any(item => HackathonPhases.GetPhase(item, now) is not (HackathonPhase.Closed or HackathonPhase.Cancelled)))
            return Fail<CompanyAccessDto>(CompanyServiceError.Conflict, "Você está inscrito em um Hackathon desta empresa. Saia da inscrição antes de aceitar.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.CompanyMemberships.Add(new CompanyMembership
        {
            CompanyId = invitation!.CompanyId,
            AccountId = account!.Id,
            JobTitle = invitation.JobTitle,
            StartedAt = now
        });
        invitation.Status = CompanyInvitationStatus.Accepted;
        invitation.RespondedAt = now;
        // Funcionário não participa dos Hackathons da empresa: convites e pedidos de equipe neles não teriam como ser aceitos.
        foreach (var teamInvitation in await dbContext.TeamInvitations
                     .Where(item => item.NormalizedEmail == account.NormalizedEmail && item.Status == TeamInvitationStatus.Pending
                         && item.Team.Hackathon.CompanyId == invitation.CompanyId)
                     .ToListAsync(cancellationToken))
        {
            teamInvitation.Status = TeamInvitationStatus.Cancelled;
            teamInvitation.RespondedAt = now;
        }
        foreach (var request in await dbContext.TeamJoinRequests
                     .Where(item => item.AccountId == account.Id && item.Status == TeamJoinRequestStatus.Pending
                         && item.Team.Hackathon.CompanyId == invitation.CompanyId)
                     .ToListAsync(cancellationToken))
        {
            request.Status = TeamJoinRequestStatus.Cancelled;
            request.RespondedAt = now;
        }
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Aceite simultâneo: o índice ux_company_memberships_active_account barra o segundo vínculo ativo.
            return Fail<CompanyAccessDto>(CompanyServiceError.Conflict, "Você já está vinculado a uma empresa.");
        }
        await transaction.CommitAsync(cancellationToken);
        return new(true, await companyAccessService.GetManagedCompanyAsync(account.Id, cancellationToken));
    }

    public async Task<CompanyServiceResult<bool>> DeclineInvitationAsync(Guid accountId, Guid invitationId, CancellationToken cancellationToken)
    {
        var (_, invitation, error) = await FindReceivedInvitationAsync<bool>(accountId, invitationId, cancellationToken);
        if (error is not null) return error;

        invitation!.Status = CompanyInvitationStatus.Declined;
        invitation.RespondedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, true);
    }

    public async Task<CompanyServiceResult<IReadOnlyList<CompanyEmployeeDto>>> GetEmployeesAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var access = await companyAccessService.GetManagedCompanyAsync(accountId, cancellationToken);
        if (access is null) return Fail<IReadOnlyList<CompanyEmployeeDto>>(CompanyServiceError.NotFound, "Empresa não encontrada.");

        var employees = await dbContext.CompanyMemberships.AsNoTracking()
            .Where(item => item.CompanyId == access.CompanyId && item.EndedAt == null)
            .OrderBy(item => item.StartedAt)
            .Select(item => new CompanyEmployeeDto(item.AccountId, item.Account.UserProfile!.Name ?? item.Account.Email,
                item.Account.Email, item.JobTitle, item.StartedAt))
            .ToListAsync(cancellationToken);
        return new(true, employees);
    }

    public async Task<CompanyServiceResult<bool>> RemoveEmployeeAsync(Guid accountId, Guid employeeAccountId, CancellationToken cancellationToken)
    {
        var company = await GetPrimaryCompanyAsync(accountId, cancellationToken);
        if (company is null) return Fail<bool>(CompanyServiceError.Forbidden, "Apenas a conta principal da empresa pode remover funcionários.");

        var membership = await dbContext.CompanyMemberships
            .SingleOrDefaultAsync(item => item.CompanyId == company.Id && item.AccountId == employeeAccountId && item.EndedAt == null, cancellationToken);
        if (membership is null) return Fail<bool>(CompanyServiceError.NotFound, "Funcionário não encontrado.");

        membership.EndedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, true);
    }

    public async Task<IReadOnlyList<CompanyVerificationDto>> GetPendingVerificationsAsync(CancellationToken cancellationToken) =>
        await dbContext.Companies.AsNoTracking()
            .Where(company => company.VerificationStatus == CompanyVerificationStatus.Pending && company.DeletedAt == null)
            .OrderBy(company => company.CreatedAt)
            .Select(company => new CompanyVerificationDto(company.Id, company.Name, company.Cnpj, company.Area, company.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<CompanyServiceResult<bool>> UpdateVerificationAsync(Guid companyId, UpdateVerificationDto dto, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CompanyVerificationStatus>(dto.Status, ignoreCase: true, out var status) || status == CompanyVerificationStatus.Pending)
            return Fail<bool>(CompanyServiceError.Validation, "Status deve ser Verified ou Rejected.");

        var company = await dbContext.Companies.SingleOrDefaultAsync(item => item.Id == companyId && item.DeletedAt == null, cancellationToken);
        if (company is null) return Fail<bool>(CompanyServiceError.NotFound, "Empresa não encontrada.");

        company.VerificationStatus = status;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, true);
    }

    // Convite recebido pela conta: valida dono do e-mail, tipo de conta, status e expiração, nessa ordem.
    private async Task<(Account? Account, CompanyInvitation? Invitation, CompanyServiceResult<T>? Error)> FindReceivedInvitationAsync<T>(
        Guid accountId, Guid invitationId, CancellationToken cancellationToken)
    {
        var account = await GetActiveAccountAsync(accountId, cancellationToken);
        var invitation = await dbContext.CompanyInvitations
            .SingleOrDefaultAsync(item => item.Id == invitationId && item.Company.DeletedAt == null, cancellationToken);
        if (account is null || invitation is null || invitation.NormalizedEmail != account.NormalizedEmail)
            return (null, null, Fail<T>(CompanyServiceError.NotFound, "Convite não encontrado."));
        if (account.AccountType != AccountType.Participant)
            return (null, null, Fail<T>(CompanyServiceError.Forbidden, "Apenas participantes podem responder convites."));
        if (invitation.Status != CompanyInvitationStatus.Pending)
            return (null, null, Fail<T>(CompanyServiceError.Conflict, "Este convite já foi respondido."));
        if (invitation.ExpiresAt <= DateTime.UtcNow)
            return (null, null, Fail<T>(CompanyServiceError.Expired, "Este convite expirou."));
        return (account, invitation, null);
    }

    private Task<Account?> GetActiveAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(account => account.Id == accountId && account.DeletedAt == null, cancellationToken);

    private Task<Company?> GetPrimaryCompanyAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.Companies.Include(company => company.PrimaryAccount)
            .SingleOrDefaultAsync(company => company.PrimaryAccountId == accountId && company.DeletedAt == null, cancellationToken);

    private static CompanyInvitationDto ToDto(CompanyInvitation item) => new(item.Id, item.Email, item.JobTitle, item.ExpiresAt, item.CreatedAt);
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static CompanyServiceResult<T> Fail<T>(CompanyServiceError error, string message) => new(false, Error: error, Message: message);
}
