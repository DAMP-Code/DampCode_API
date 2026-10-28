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

    private Task<Company?> GetPrimaryCompanyAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.Companies.Include(company => company.PrimaryAccount)
            .SingleOrDefaultAsync(company => company.PrimaryAccountId == accountId && company.DeletedAt == null, cancellationToken);

    private static CompanyInvitationDto ToDto(CompanyInvitation item) => new(item.Id, item.Email, item.JobTitle, item.ExpiresAt, item.CreatedAt);
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static CompanyServiceResult<T> Fail<T>(CompanyServiceError error, string message) => new(false, Error: error, Message: message);
}
