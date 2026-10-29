using DampCode_API.Dto;

namespace DampCode_API.Services;

public interface IHackathonService
{
    Task<HackathonServiceResult<HackathonResponseDto>> CreateAsync(Guid accountId, CreateHackathonDto dto, CancellationToken cancellationToken);
    Task<HackathonServiceResult<HackathonResponseDto>> UpdateAsync(Guid accountId, Guid hackathonId, CreateHackathonDto dto, CancellationToken cancellationToken);
    Task<HackathonServiceResult<bool>> DeleteAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken);
    Task<HackathonServiceResult<HackathonResponseDto>> PublishAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken);
    Task<HackathonServiceResult<HackathonResponseDto>> CancelAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken);
    Task<HackathonServiceResult<HackathonResponseDto>> CloseAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken);
    Task<IReadOnlyList<HackathonResponseDto>> GetPublicAsync(CancellationToken cancellationToken);
    Task<HackathonServiceResult<IReadOnlyList<HackathonResponseDto>>> GetCompanyHackathonsAsync(Guid accountId, CancellationToken cancellationToken);
    // id aceita o Guid ou o slug; accountId é nulo para visitante anônimo.
    Task<HackathonServiceResult<HackathonResponseDto>> GetByIdAsync(Guid? accountId, string id, CancellationToken cancellationToken);
}

public sealed record HackathonServiceResult<T>(bool Succeeded, T? Value = default, HackathonServiceError Error = HackathonServiceError.None, string? Message = null);
public enum HackathonServiceError { None, NotFound, Forbidden, Conflict, Validation }
