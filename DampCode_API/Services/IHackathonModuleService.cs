using DampCode_API.Dto;

namespace DampCode_API.Services;

public interface IHackathonModuleService
{
    // id aceita o Guid ou o slug; accountId é nulo para visitante anônimo.
    Task<HackathonModuleServiceResult<IReadOnlyList<HackathonModuleDto>>> GetAsync(Guid? accountId, string id, CancellationToken cancellationToken);
    Task<HackathonModuleServiceResult<HackathonModuleDto>> CreateModuleAsync(Guid accountId, Guid hackathonId, SaveHackathonModuleDto dto, CancellationToken cancellationToken);
    Task<HackathonModuleServiceResult<HackathonModuleDto>> UpdateModuleAsync(Guid accountId, Guid hackathonId, Guid moduleId, SaveHackathonModuleDto dto, CancellationToken cancellationToken);
    Task<HackathonModuleServiceResult<bool>> DeleteModuleAsync(Guid accountId, Guid hackathonId, Guid moduleId, CancellationToken cancellationToken);
    Task<HackathonModuleServiceResult<IReadOnlyList<HackathonModuleDto>>> ReorderModulesAsync(Guid accountId, Guid hackathonId, HackathonModuleOrderDto dto, CancellationToken cancellationToken);
    Task<HackathonModuleServiceResult<HackathonModuleContentDto>> CreateContentAsync(Guid accountId, Guid hackathonId, Guid moduleId, SaveHackathonModuleContentDto dto, CancellationToken cancellationToken);
    Task<HackathonModuleServiceResult<HackathonModuleContentDto>> UpdateContentAsync(Guid accountId, Guid hackathonId, Guid moduleId, Guid contentId, SaveHackathonModuleContentDto dto, CancellationToken cancellationToken);
    Task<HackathonModuleServiceResult<bool>> DeleteContentAsync(Guid accountId, Guid hackathonId, Guid moduleId, Guid contentId, CancellationToken cancellationToken);
    Task<HackathonModuleServiceResult<HackathonModuleDto>> ReorderContentsAsync(Guid accountId, Guid hackathonId, Guid moduleId, HackathonModuleOrderDto dto, CancellationToken cancellationToken);
}

public sealed record HackathonModuleServiceResult<T>(bool Succeeded, T? Value = default, HackathonModuleServiceError Error = HackathonModuleServiceError.None, string? Message = null);
public enum HackathonModuleServiceError { None, NotFound, Forbidden, Conflict, Validation }
