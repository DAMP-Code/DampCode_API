using DampCode_API.Dto;

namespace DampCode_API.Services;

public interface ISubmissionService
{
    Task<SubmissionServiceResult<MySubmissionDto>> GetMineAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken);
    Task<SubmissionServiceResult<SubmissionCodeDto>> GetMyVersionAsync(Guid accountId, Guid hackathonId, int version, CancellationToken cancellationToken);
    Task<SubmissionServiceResult<SubmissionCodeDto>> SubmitAsync(Guid accountId, Guid hackathonId, SubmitCodeDto dto, CancellationToken cancellationToken);
}

public sealed record SubmissionServiceResult<T>(bool Succeeded, T? Value = default, SubmissionServiceError Error = SubmissionServiceError.None, string? Message = null);
public enum SubmissionServiceError { None, NotFound, Forbidden, Conflict, Validation }
