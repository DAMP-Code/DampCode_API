using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Services;

public sealed class ParticipationService(DampCodeDbContext dbContext, ICompanyAccessService companyAccessService) : IParticipationService
{
    private const string NotFoundMessage = "Hackathon não encontrado.";
    private const string AlreadyRegisteredMessage = "Você já está inscrito neste Hackathon.";
    private const string ClosedMessage = "As inscrições deste Hackathon estão encerradas.";

    public async Task<ParticipationServiceResult<MyParticipationDto>> RegisterAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken)
    {
        var (account, hackathon, failure) = await CheckCanJoinAsync<MyParticipationDto>(accountId, hackathonId, AlreadyRegisteredMessage, cancellationToken);
        if (failure is not null) return failure;
        if (hackathon!.ParticipationMode == HackathonParticipationMode.Team)
            return Fail<MyParticipationDto>(ParticipationServiceError.Conflict, "Este Hackathon é em equipe. Crie uma equipe ou entre em uma.");

        dbContext.Participations.Add(new Participation { HackathonId = hackathon.Id, AccountId = account!.Id });
        await CancelPendingForAccountAsync(account, hackathon.Id, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Inscrição simultânea: o índice ux_participations_active_account barra a segunda.
            return Fail<MyParticipationDto>(ParticipationServiceError.Conflict, AlreadyRegisteredMessage);
        }
        return new(true, ToDto(hackathon, null, DateTime.UtcNow));
    }

    public async Task<ParticipationServiceResult<bool>> CancelRegistrationAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken)
    {
        var hackathon = await dbContext.Hackathons.AsNoTracking().SingleOrDefaultAsync(item => item.Id == hackathonId, cancellationToken);
        var now = DateTime.UtcNow;
        if (hackathon is null || HackathonPhases.GetPhase(hackathon, now) is HackathonPhase.Draft or HackathonPhase.Cancelled)
            return Fail<bool>(ParticipationServiceError.NotFound, NotFoundMessage);
        if (hackathon.ParticipationMode == HackathonParticipationMode.Team)
            return Fail<bool>(ParticipationServiceError.Conflict, "Use sair da equipe.");
        if (HackathonPhases.GetPhase(hackathon, now) != HackathonPhase.Published)
            return Fail<bool>(ParticipationServiceError.Conflict, ClosedMessage);

        var participation = await dbContext.Participations
            .SingleOrDefaultAsync(item => item.HackathonId == hackathonId && item.AccountId == accountId && item.LeftAt == null, cancellationToken);
        if (participation is null) return Fail<bool>(ParticipationServiceError.NotFound, "Inscrição não encontrada.");

        participation.LeftAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, true);
    }

    // Regras comuns a toda entrada (inscrição individual, criar equipe, aceitar convite ou pedido), nesta ordem:
    // participante ativo, Hackathon público, não organizador, fase Publicado, sem inscrição ativa.
    private async Task<(Account? Account, Hackathon? Hackathon, ParticipationServiceResult<T>? Failure)> CheckCanJoinAsync<T>(
        Guid accountId, Guid hackathonId, string alreadyRegisteredMessage, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.AsNoTracking().Include(item => item.UserProfile)
            .SingleOrDefaultAsync(item => item.Id == accountId && item.DeletedAt == null, cancellationToken);
        if (account is null || account.AccountType != AccountType.Participant || account.Status != AccountStatus.Active)
            return (null, null, Fail<T>(ParticipationServiceError.Forbidden, "Apenas participantes podem se inscrever em Hackathons."));

        var hackathon = await dbContext.Hackathons.Include(item => item.Company)
            .SingleOrDefaultAsync(item => item.Id == hackathonId, cancellationToken);
        var phase = hackathon is null ? HackathonPhase.Draft : HackathonPhases.GetPhase(hackathon, DateTime.UtcNow);
        if (hackathon is null || phase is HackathonPhase.Draft or HackathonPhase.Cancelled)
            return (null, null, Fail<T>(ParticipationServiceError.NotFound, NotFoundMessage));

        var managed = await companyAccessService.GetManagedCompanyAsync(accountId, cancellationToken);
        if (managed?.CompanyId == hackathon.CompanyId)
            return (null, null, Fail<T>(ParticipationServiceError.Forbidden, "Quem organiza o Hackathon não pode participar dele."));
        if (phase != HackathonPhase.Published)
            return (null, null, Fail<T>(ParticipationServiceError.Conflict, ClosedMessage));
        if (await dbContext.Participations.AnyAsync(item => item.HackathonId == hackathonId && item.AccountId == accountId && item.LeftAt == null, cancellationToken))
            return (null, null, Fail<T>(ParticipationServiceError.Conflict, alreadyRegisteredMessage));
        return (account, hackathon, null);
    }

    // Quem acabou de se inscrever não pode ter convite ou pedido pendente que só daria erro ao ser aceito.
    private async Task CancelPendingForAccountAsync(Account account, Guid hackathonId, CancellationToken cancellationToken, Guid? exceptRequestId = null)
    {
        var now = DateTime.UtcNow;
        var invitations = await dbContext.TeamInvitations
            .Where(item => item.Team.HackathonId == hackathonId && item.NormalizedEmail == account.NormalizedEmail && item.Status == TeamInvitationStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var invitation in invitations)
        {
            invitation.Status = TeamInvitationStatus.Cancelled;
            invitation.RespondedAt = now;
        }
        var requests = await dbContext.TeamJoinRequests
            .Where(item => item.Team.HackathonId == hackathonId && item.AccountId == account.Id
                && item.Status == TeamJoinRequestStatus.Pending && item.Id != exceptRequestId)
            .ToListAsync(cancellationToken);
        foreach (var request in requests)
        {
            request.Status = TeamJoinRequestStatus.Cancelled;
            request.RespondedAt = now;
        }
    }

    private static MyParticipationDto ToDto(Hackathon hackathon, MyTeamDto? team, DateTime now) => new(
        hackathon.Id,
        hackathon.Title,
        hackathon.Company.Name,
        HackathonPhases.ToText(HackathonPhases.GetPhase(hackathon, now)),
        hackathon.ParticipationMode == HackathonParticipationMode.Team ? "Equipe" : "Individual",
        hackathon.StartsAt,
        team);

    private static ParticipationServiceResult<T> Fail<T>(ParticipationServiceError error, string message) => new(false, Error: error, Message: message);
}
