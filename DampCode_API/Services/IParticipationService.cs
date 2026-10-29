using DampCode_API.Dto;

namespace DampCode_API.Services;

public interface IParticipationService
{
    Task<ParticipationServiceResult<MyParticipationDto>> RegisterAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken);
    Task<ParticipationServiceResult<bool>> CancelRegistrationAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken);
    Task<ParticipationServiceResult<MyTeamDto>> CreateTeamAsync(Guid accountId, Guid hackathonId, CreateTeamDto dto, CancellationToken cancellationToken);
    Task<ParticipationServiceResult<MyTeamDto>> UpdateTeamAsync(Guid accountId, Guid teamId, UpdateTeamDto dto, CancellationToken cancellationToken);
    Task<ParticipationServiceResult<TeamPendingInvitationDto>> InviteAsync(Guid accountId, Guid teamId, InviteTeamMemberDto dto, CancellationToken cancellationToken);
    Task<ParticipationServiceResult<bool>> RevokeInvitationAsync(Guid accountId, Guid teamId, Guid invitationId, CancellationToken cancellationToken);
    Task<ParticipationServiceResult<MyParticipationDto>> AcceptInvitationAsync(Guid accountId, Guid invitationId, CancellationToken cancellationToken);
    Task<ParticipationServiceResult<bool>> DeclineInvitationAsync(Guid accountId, Guid invitationId, CancellationToken cancellationToken);
}

public sealed record ParticipationServiceResult<T>(bool Succeeded, T? Value = default, ParticipationServiceError Error = ParticipationServiceError.None, string? Message = null);
public enum ParticipationServiceError { None, NotFound, Forbidden, Conflict, Expired, Validation }
