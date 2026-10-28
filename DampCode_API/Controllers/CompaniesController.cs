using System.Security.Claims;
using DampCode_API.Dto;
using DampCode_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DampCode_API.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public sealed class CompaniesController(ICompanyService companyService, ICompanyAccessService companyAccessService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<CompanyAccessDto>> Me(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId)) return Unauthorized();
        var company = await companyAccessService.GetManagedCompanyAsync(accountId, cancellationToken);
        return company is null ? NotFound(new ProblemDetails { Detail = "Empresa não encontrada." }) : Ok(company);
    }

    [HttpGet("me/employees")]
    public async Task<IActionResult> GetEmployees(CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await companyService.GetEmployeesAsync(accountId, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpGet("me/invitations")]
    public async Task<IActionResult> GetSentInvitations(CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await companyService.GetSentInvitationsAsync(accountId, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpPost("me/invitations")]
    public async Task<IActionResult> InviteEmployee([FromBody] InviteEmployeeDto dto, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await companyService.InviteEmployeeAsync(accountId, dto, cancellationToken), StatusCodes.Status201Created)
            : Unauthorized();

    [HttpDelete("me/invitations/{id:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid id, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await companyService.RevokeInvitationAsync(accountId, id, cancellationToken), StatusCodes.Status204NoContent)
            : Unauthorized();

    [HttpDelete("me/employees/{accountId:guid}")]
    public async Task<IActionResult> RemoveEmployee(Guid accountId, CancellationToken cancellationToken) =>
        TryGetAccountId(out var currentAccountId)
            ? ToActionResult(await companyService.RemoveEmployeeAsync(currentAccountId, accountId, cancellationToken), StatusCodes.Status204NoContent)
            : Unauthorized();

    [HttpGet("invitations/received")]
    public async Task<ActionResult<IReadOnlyList<ReceivedInvitationDto>>> GetReceivedInvitations(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId)) return Unauthorized();
        return Ok(await companyService.GetReceivedInvitationsAsync(accountId, cancellationToken));
    }

    [HttpPost("invitations/{id:guid}/accept")]
    public async Task<IActionResult> AcceptInvitation(Guid id, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await companyService.AcceptInvitationAsync(accountId, id, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpPost("invitations/{id:guid}/decline")]
    public async Task<IActionResult> DeclineInvitation(Guid id, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await companyService.DeclineInvitationAsync(accountId, id, cancellationToken), StatusCodes.Status204NoContent)
            : Unauthorized();

    [Authorize(Roles = "Admin")]
    [HttpGet("pending-verification")]
    public async Task<ActionResult<IReadOnlyList<CompanyVerificationDto>>> GetPendingVerifications(CancellationToken cancellationToken) =>
        Ok(await companyService.GetPendingVerificationsAsync(cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/verification")]
    public async Task<IActionResult> UpdateVerification(Guid id, [FromBody] UpdateVerificationDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await companyService.UpdateVerificationAsync(id, dto, cancellationToken), StatusCodes.Status204NoContent);

    private bool TryGetAccountId(out Guid accountId) =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId);

    private IActionResult ToActionResult<T>(CompanyServiceResult<T> result, int successStatusCode)
    {
        if (result.Succeeded)
            return successStatusCode == StatusCodes.Status204NoContent ? NoContent() : StatusCode(successStatusCode, result.Value);
        var problem = new ProblemDetails { Detail = result.Message };
        return result.Error switch
        {
            CompanyServiceError.NotFound => NotFound(problem),
            CompanyServiceError.Forbidden => StatusCode(StatusCodes.Status403Forbidden, problem),
            CompanyServiceError.Conflict => Conflict(problem),
            CompanyServiceError.Expired => StatusCode(StatusCodes.Status410Gone, problem),
            CompanyServiceError.Validation => BadRequest(problem),
            _ => StatusCode(StatusCodes.Status500InternalServerError, problem)
        };
    }
}
