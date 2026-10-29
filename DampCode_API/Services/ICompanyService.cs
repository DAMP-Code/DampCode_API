using DampCode_API.Dto;

namespace DampCode_API.Services;

public interface ICompanyService
{
    Task<CompanyServiceResult<CompanyInvitationDto>> InviteEmployeeAsync(Guid accountId, InviteEmployeeDto dto, CancellationToken cancellationToken);
    Task<CompanyServiceResult<IReadOnlyList<CompanyInvitationDto>>> GetSentInvitationsAsync(Guid accountId, CancellationToken cancellationToken);
    Task<CompanyServiceResult<bool>> RevokeInvitationAsync(Guid accountId, Guid invitationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReceivedInvitationDto>> GetReceivedInvitationsAsync(Guid accountId, CancellationToken cancellationToken);
    Task<CompanyServiceResult<CompanyAccessDto>> AcceptInvitationAsync(Guid accountId, Guid invitationId, CancellationToken cancellationToken);
    Task<CompanyServiceResult<bool>> DeclineInvitationAsync(Guid accountId, Guid invitationId, CancellationToken cancellationToken);
    Task<CompanyServiceResult<IReadOnlyList<CompanyEmployeeDto>>> GetEmployeesAsync(Guid accountId, CancellationToken cancellationToken);
    Task<CompanyServiceResult<bool>> RemoveEmployeeAsync(Guid accountId, Guid employeeAccountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyVerificationDto>> GetPendingVerificationsAsync(CancellationToken cancellationToken);
    Task<CompanyServiceResult<bool>> UpdateVerificationAsync(Guid companyId, UpdateVerificationDto dto, CancellationToken cancellationToken);
}

public sealed record CompanyServiceResult<T>(bool Succeeded, T? Value = default, CompanyServiceError Error = CompanyServiceError.None, string? Message = null);
public enum CompanyServiceError { None, NotFound, Forbidden, Conflict, Expired, Validation }
