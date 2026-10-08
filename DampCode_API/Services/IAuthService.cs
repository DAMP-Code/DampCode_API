using DampCode_API.Dto;

namespace DampCode_API.Services;

public interface IAuthService
{
    Task<AuthServiceResult> RegisterParticipantAsync(ParticipantDto dto, CancellationToken cancellationToken);
    Task<AuthServiceResult> RegisterCompanyAsync(CompanyDto dto, CancellationToken cancellationToken);
    Task<AuthServiceResult> LoginAsync(LoginDto dto, CancellationToken cancellationToken);
}

public sealed record AuthServiceResult(bool Succeeded, AuthResponseDto? Value = null, AuthServiceError Error = AuthServiceError.None, string? Message = null);
public enum AuthServiceError { None, Conflict, InvalidCredentials, InactiveAccount }
