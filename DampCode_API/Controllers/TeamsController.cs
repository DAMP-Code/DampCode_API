using System.Security.Claims;
using DampCode_API.Dto;
using DampCode_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DampCode_API.Controllers;

// Equipes de Hackathon: convites por e-mail, pedidos para entrar e saída. Criar fica em HackathonsController.
[Authorize]
[Route("api/[controller]")]
[ApiController]
public sealed class TeamsController(IParticipationService participationService) : ControllerBase
{
    [HttpPatch("{teamId:guid}")]
    public async Task<IActionResult> UpdateTeam(Guid teamId, [FromBody] UpdateTeamDto dto, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await participationService.UpdateTeamAsync(accountId, teamId, dto, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpPost("{teamId:guid}/convites")]
    public async Task<IActionResult> Invite(Guid teamId, [FromBody] InviteTeamMemberDto dto, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await participationService.InviteAsync(accountId, teamId, dto, cancellationToken), StatusCodes.Status201Created)
            : Unauthorized();

    [HttpDelete("{teamId:guid}/convites/{inviteId:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid teamId, Guid inviteId, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await participationService.RevokeInvitationAsync(accountId, teamId, inviteId, cancellationToken), StatusCodes.Status204NoContent)
            : Unauthorized();

    [HttpPost("convites/{inviteId:guid}/aceitar")]
    public async Task<IActionResult> AcceptInvitation(Guid inviteId, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await participationService.AcceptInvitationAsync(accountId, inviteId, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpPost("convites/{inviteId:guid}/recusar")]
    public async Task<IActionResult> DeclineInvitation(Guid inviteId, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await participationService.DeclineInvitationAsync(accountId, inviteId, cancellationToken), StatusCodes.Status204NoContent)
            : Unauthorized();

    [HttpPost("{teamId:guid}/pedidos")]
    public async Task<IActionResult> RequestToJoin(Guid teamId, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await participationService.RequestToJoinAsync(accountId, teamId, cancellationToken), StatusCodes.Status201Created)
            : Unauthorized();

    [HttpDelete("pedidos/{requestId:guid}")]
    public async Task<IActionResult> CancelRequest(Guid requestId, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await participationService.CancelRequestAsync(accountId, requestId, cancellationToken), StatusCodes.Status204NoContent)
            : Unauthorized();

    [HttpPost("pedidos/{requestId:guid}/aceitar")]
    public async Task<IActionResult> AcceptRequest(Guid requestId, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await participationService.AcceptRequestAsync(accountId, requestId, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpPost("pedidos/{requestId:guid}/recusar")]
    public async Task<IActionResult> DeclineRequest(Guid requestId, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await participationService.DeclineRequestAsync(accountId, requestId, cancellationToken), StatusCodes.Status204NoContent)
            : Unauthorized();

    [HttpDelete("{teamId:guid}/membros/me")]
    public async Task<IActionResult> LeaveTeam(Guid teamId, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await participationService.LeaveTeamAsync(accountId, teamId, cancellationToken), StatusCodes.Status204NoContent)
            : Unauthorized();

    private bool TryGetAccountId(out Guid accountId) =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId);

    private IActionResult ToActionResult<T>(ParticipationServiceResult<T> result, int successStatusCode)
    {
        if (result.Succeeded)
            return successStatusCode == StatusCodes.Status204NoContent ? NoContent() : StatusCode(successStatusCode, result.Value);
        var problem = new ProblemDetails { Detail = result.Message };
        return result.Error switch
        {
            ParticipationServiceError.NotFound => NotFound(problem),
            ParticipationServiceError.Forbidden => StatusCode(StatusCodes.Status403Forbidden, problem),
            ParticipationServiceError.Conflict => Conflict(problem),
            ParticipationServiceError.Expired => StatusCode(StatusCodes.Status410Gone, problem),
            ParticipationServiceError.Validation => BadRequest(problem),
            _ => StatusCode(StatusCodes.Status500InternalServerError, problem)
        };
    }
}
