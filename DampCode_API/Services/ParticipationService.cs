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
    private const string FullTeamMessage = "Esta equipe já está completa.";
    private const string InvitationNotFoundMessage = "Convite não encontrado.";
    private const string InvitationAnsweredMessage = "Este convite já foi respondido.";
    private const int MaxTeamSize = 5;
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromHours(24);

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

    public async Task<ParticipationServiceResult<MyTeamDto>> CreateTeamAsync(Guid accountId, Guid hackathonId, CreateTeamDto dto, CancellationToken cancellationToken)
    {
        var (account, hackathon, failure) = await CheckCanJoinAsync<MyTeamDto>(accountId, hackathonId, AlreadyRegisteredMessage, cancellationToken);
        if (failure is not null) return failure;
        if (hackathon!.ParticipationMode == HackathonParticipationMode.Individual)
            return Fail<MyTeamDto>(ParticipationServiceError.Conflict, "Este Hackathon é individual.");
        if (await ValidateTeamNameAsync<MyTeamDto>(hackathonId, dto.Nome, null, cancellationToken) is { } nameError) return nameError;

        var name = dto.Nome!.Trim();
        var team = new HackathonTeam
        {
            HackathonId = hackathonId,
            CreatedByAccountId = accountId,
            Name = name,
            NormalizedName = Normalize(name),
            IsOpen = dto.Aberta,
            MemberCount = 1
        };
        dbContext.HackathonTeams.Add(team);
        dbContext.Participations.Add(new Participation { HackathonId = hackathonId, AccountId = accountId, TeamId = team.Id });
        await CancelPendingForAccountAsync(account!, hackathonId, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Fail<MyTeamDto>(ParticipationServiceError.Conflict, AlreadyRegisteredMessage);
        }
        return new(true, await BuildTeamDtoAsync(team.Id, cancellationToken));
    }

    public async Task<ParticipationServiceResult<MyTeamDto>> UpdateTeamAsync(Guid accountId, Guid teamId, UpdateTeamDto dto, CancellationToken cancellationToken)
    {
        var (team, failure) = await FindMemberTeamAsync<MyTeamDto>(accountId, teamId, cancellationToken);
        if (team is null) return failure!;
        if (dto.Nome is not null)
        {
            if (await ValidateTeamNameAsync<MyTeamDto>(team.HackathonId, dto.Nome, team.Id, cancellationToken) is { } nameError) return nameError;
            team.Name = dto.Nome.Trim();
            team.NormalizedName = Normalize(dto.Nome);
        }
        if (dto.Aberta is not null) team.IsOpen = dto.Aberta.Value;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, await BuildTeamDtoAsync(team.Id, cancellationToken));
    }

    public async Task<ParticipationServiceResult<TeamPendingInvitationDto>> InviteAsync(Guid accountId, Guid teamId, InviteTeamMemberDto dto, CancellationToken cancellationToken)
    {
        var (team, failure) = await FindMemberTeamAsync<TeamPendingInvitationDto>(accountId, teamId, cancellationToken);
        if (team is null) return failure!;
        if (team.MemberCount >= MaxTeamSize) return Fail<TeamPendingInvitationDto>(ParticipationServiceError.Conflict, FullTeamMessage);

        var email = dto.Email.Trim();
        var normalizedEmail = Normalize(email);
        var invited = await dbContext.Accounts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail && item.DeletedAt == null, cancellationToken);
        if (invited is null || invited.AccountType != AccountType.Participant)
            return Fail<TeamPendingInvitationDto>(ParticipationServiceError.NotFound, "Nenhum participante encontrado com este e-mail.");
        if (await dbContext.Participations.AnyAsync(item => item.TeamId == teamId && item.AccountId == invited.Id && item.LeftAt == null, cancellationToken))
            return Fail<TeamPendingInvitationDto>(ParticipationServiceError.Conflict, "Esta pessoa já está na equipe.");

        var now = DateTime.UtcNow;
        if (await dbContext.TeamInvitations.AnyAsync(item => item.TeamId == teamId && item.NormalizedEmail == normalizedEmail
                && item.Status == TeamInvitationStatus.Pending && item.ExpiresAt > now, cancellationToken))
            return Fail<TeamPendingInvitationDto>(ParticipationServiceError.Conflict, "Já existe um convite pendente para este e-mail.");

        var invitation = new TeamInvitation
        {
            TeamId = teamId,
            InvitedByAccountId = accountId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.Add(InvitationLifetime)
        };
        dbContext.TeamInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, new TeamPendingInvitationDto(invitation.Id, invitation.Email, invitation.ExpiresAt));
    }

    public async Task<ParticipationServiceResult<bool>> RevokeInvitationAsync(Guid accountId, Guid teamId, Guid invitationId, CancellationToken cancellationToken)
    {
        var (team, failure) = await FindMemberTeamAsync<bool>(accountId, teamId, cancellationToken);
        if (team is null) return failure!;
        var invitation = await dbContext.TeamInvitations.SingleOrDefaultAsync(item => item.Id == invitationId && item.TeamId == teamId, cancellationToken);
        if (invitation is null) return Fail<bool>(ParticipationServiceError.NotFound, InvitationNotFoundMessage);
        if (invitation.Status != TeamInvitationStatus.Pending) return Fail<bool>(ParticipationServiceError.Conflict, InvitationAnsweredMessage);

        invitation.Status = TeamInvitationStatus.Revoked;
        invitation.RespondedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, true);
    }

    public async Task<ParticipationServiceResult<MyParticipationDto>> AcceptInvitationAsync(Guid accountId, Guid invitationId, CancellationToken cancellationToken)
    {
        var (invitation, invitationFailure) = await FindReceivedInvitationAsync<MyParticipationDto>(accountId, invitationId, cancellationToken);
        if (invitation is null) return invitationFailure!;
        var (account, hackathon, failure) = await CheckCanJoinAsync<MyParticipationDto>(accountId, invitation.Team.HackathonId,
            "Você já está inscrito neste Hackathon. Saia da equipe atual antes.", cancellationToken);
        if (failure is not null) return failure;

        invitation.Status = TeamInvitationStatus.Accepted;
        invitation.RespondedAt = DateTime.UtcNow;
        if (await JoinTeamAsync<MyParticipationDto>(account!, invitation.Team, cancellationToken) is { } joinFailure) return joinFailure;
        return new(true, ToDto(hackathon!, await BuildTeamDtoAsync(invitation.TeamId, cancellationToken), DateTime.UtcNow));
    }

    public async Task<ParticipationServiceResult<bool>> DeclineInvitationAsync(Guid accountId, Guid invitationId, CancellationToken cancellationToken)
    {
        var (invitation, failure) = await FindReceivedInvitationAsync<bool>(accountId, invitationId, cancellationToken);
        if (invitation is null) return failure!;

        invitation.Status = TeamInvitationStatus.Declined;
        invitation.RespondedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, true);
    }

    // Convite pendente e válido endereçado ao e-mail da conta.
    private async Task<(TeamInvitation? Invitation, ParticipationServiceResult<T>? Failure)> FindReceivedInvitationAsync<T>(
        Guid accountId, Guid invitationId, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == accountId && item.DeletedAt == null, cancellationToken);
        var invitation = await dbContext.TeamInvitations.Include(item => item.Team)
            .SingleOrDefaultAsync(item => item.Id == invitationId, cancellationToken);
        if (account is null || invitation is null || invitation.NormalizedEmail != account.NormalizedEmail)
            return (null, Fail<T>(ParticipationServiceError.NotFound, InvitationNotFoundMessage));
        if (invitation.Status != TeamInvitationStatus.Pending)
            return (null, Fail<T>(ParticipationServiceError.Conflict, InvitationAnsweredMessage));
        if (invitation.ExpiresAt <= DateTime.UtcNow)
            return (null, Fail<T>(ParticipationServiceError.Expired, "Este convite expirou."));
        return (invitation, null);
    }

    // Equipe em que a conta está, com o Hackathon ainda aceitando mudanças.
    private async Task<(HackathonTeam? Team, ParticipationServiceResult<T>? Failure)> FindMemberTeamAsync<T>(
        Guid accountId, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await dbContext.HackathonTeams.Include(item => item.Hackathon)
            .SingleOrDefaultAsync(item => item.Id == teamId
                && item.Members.Any(member => member.AccountId == accountId && member.LeftAt == null), cancellationToken);
        if (team is null) return (null, Fail<T>(ParticipationServiceError.NotFound, "Equipe não encontrada."));
        if (HackathonPhases.GetPhase(team.Hackathon, DateTime.UtcNow) != HackathonPhase.Published)
            return (null, Fail<T>(ParticipationServiceError.Conflict, ClosedMessage));
        return (team, null);
    }

    // Coloca a pessoa na equipe. MemberCount é token de concorrência: se outra entrada mudou a equipe
    // entre a leitura e o salvamento, o EF recusa e respondemos que a equipe encheu.
    private async Task<ParticipationServiceResult<T>?> JoinTeamAsync<T>(Account account, HackathonTeam team, CancellationToken cancellationToken)
    {
        if (team.MemberCount >= MaxTeamSize) return Fail<T>(ParticipationServiceError.Conflict, FullTeamMessage);

        dbContext.Participations.Add(new Participation { HackathonId = team.HackathonId, AccountId = account.Id, TeamId = team.Id });
        team.MemberCount++;
        await CancelPendingForAccountAsync(account, team.HackathonId, cancellationToken);
        if (team.MemberCount >= MaxTeamSize) await CancelPendingForTeamAsync(team.Id, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail<T>(ParticipationServiceError.Conflict, FullTeamMessage);
        }
        catch (DbUpdateException)
        {
            // Dois aceites da mesma pessoa: o índice ux_participations_active_account barra o segundo.
            return Fail<T>(ParticipationServiceError.Conflict, AlreadyRegisteredMessage);
        }
        return null;
    }

    private async Task<ParticipationServiceResult<T>?> ValidateTeamNameAsync<T>(Guid hackathonId, string? name, Guid? currentTeamId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            return Fail<T>(ParticipationServiceError.Validation, "Informe um nome de equipe com até 100 caracteres.");
        var normalizedName = Normalize(name);
        if (await dbContext.HackathonTeams.AnyAsync(item => item.HackathonId == hackathonId && item.NormalizedName == normalizedName && item.Id != currentTeamId, cancellationToken))
            return Fail<T>(ParticipationServiceError.Conflict, "Já existe uma equipe com este nome neste Hackathon.");
        return null;
    }

    private async Task<MyTeamDto> BuildTeamDtoAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var team = await dbContext.HackathonTeams.AsNoTracking().SingleAsync(item => item.Id == teamId, cancellationToken);
        var members = await dbContext.Participations.AsNoTracking()
            .Where(item => item.TeamId == teamId && item.LeftAt == null)
            .OrderBy(item => item.JoinedAt)
            .Select(item => new TeamMemberDto(item.AccountId, item.Account.UserProfile != null ? item.Account.UserProfile.Name : item.Account.Email))
            .ToListAsync(cancellationToken);
        var invitations = await dbContext.TeamInvitations.AsNoTracking()
            .Where(item => item.TeamId == teamId && item.Status == TeamInvitationStatus.Pending && item.ExpiresAt > now)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new TeamPendingInvitationDto(item.Id, item.Email, item.ExpiresAt))
            .ToListAsync(cancellationToken);
        var requests = await dbContext.TeamJoinRequests.AsNoTracking()
            .Where(item => item.TeamId == teamId && item.Status == TeamJoinRequestStatus.Pending)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new TeamPendingRequestDto(item.Id, item.AccountId, item.Account.UserProfile != null ? item.Account.UserProfile.Name : item.Account.Email))
            .ToListAsync(cancellationToken);
        return new MyTeamDto(team.Id, team.Name, team.IsOpen, members, invitations, requests);
    }

    // Equipe completa: convites e pedidos pendentes dela não teriam como ser aceitos.
    private async Task CancelPendingForTeamAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        foreach (var invitation in (await dbContext.TeamInvitations.Where(item => item.TeamId == teamId && item.Status == TeamInvitationStatus.Pending)
                     .ToListAsync(cancellationToken)).Where(item => item.Status == TeamInvitationStatus.Pending))
        {
            invitation.Status = TeamInvitationStatus.Cancelled;
            invitation.RespondedAt = now;
        }
        foreach (var request in (await dbContext.TeamJoinRequests.Where(item => item.TeamId == teamId && item.Status == TeamJoinRequestStatus.Pending)
                     .ToListAsync(cancellationToken)).Where(item => item.Status == TeamJoinRequestStatus.Pending))
        {
            request.Status = TeamJoinRequestStatus.Cancelled;
            request.RespondedAt = now;
        }
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
    // O filtro em memória deixa de fora o convite ou pedido que está sendo aceito agora (já marcado, ainda não salvo).
    private async Task CancelPendingForAccountAsync(Account account, Guid hackathonId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var invitations = (await dbContext.TeamInvitations
            .Where(item => item.Team.HackathonId == hackathonId && item.NormalizedEmail == account.NormalizedEmail && item.Status == TeamInvitationStatus.Pending)
            .ToListAsync(cancellationToken)).Where(item => item.Status == TeamInvitationStatus.Pending);
        foreach (var invitation in invitations)
        {
            invitation.Status = TeamInvitationStatus.Cancelled;
            invitation.RespondedAt = now;
        }
        var requests = (await dbContext.TeamJoinRequests
            .Where(item => item.Team.HackathonId == hackathonId && item.AccountId == account.Id && item.Status == TeamJoinRequestStatus.Pending)
            .ToListAsync(cancellationToken)).Where(item => item.Status == TeamJoinRequestStatus.Pending);
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

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static ParticipationServiceResult<T> Fail<T>(ParticipationServiceError error, string message) => new(false, Error: error, Message: message);
}
