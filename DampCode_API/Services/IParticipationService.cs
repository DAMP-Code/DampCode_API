using DampCode_API.Dto;

namespace DampCode_API.Services;

public interface IParticipationService
{
    Task<ParticipationServiceResult<MyParticipationDto>> RegisterAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken);
    Task<ParticipationServiceResult<bool>> CancelRegistrationAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken);
}

public sealed record ParticipationServiceResult<T>(bool Succeeded, T? Value = default, ParticipationServiceError Error = ParticipationServiceError.None, string? Message = null);
public enum ParticipationServiceError { None, NotFound, Forbidden, Conflict, Expired, Validation }
