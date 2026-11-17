using DampCode_API.Dto;

namespace DampCode_API.Services;

public interface IEvaluationService
{
    Task<EvaluationServiceResult<EvaluationRankingDto>> GetRankingAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken);
    Task<EvaluationServiceResult<EvaluationEntryDto>> EvaluateAsync(Guid accountId, Guid hackathonId, Guid submissionId, EvaluateDto dto, CancellationToken cancellationToken);
    Task<EvaluationServiceResult<EvaluationRankingDto>> SetWinnersAsync(Guid accountId, Guid hackathonId, SetWinnersDto dto, CancellationToken cancellationToken);
    Task<EvaluationServiceResult<bool>> PublishResultsAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken);
}

public sealed record EvaluationServiceResult<T>(bool Succeeded, T? Value = default, EvaluationServiceError Error = EvaluationServiceError.None, string? Message = null);
public enum EvaluationServiceError { None, NotFound, Forbidden, Conflict, Validation }
