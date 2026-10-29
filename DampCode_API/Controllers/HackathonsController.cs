using System.Security.Claims;
using DampCode_API.Dto;
using DampCode_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DampCode_API.Controllers;

// Hackathons ficam no PostgreSQL, ligados à empresa; o MongoDB fica para as submissões.
[Authorize]
[Route("api/[controller]")]
[ApiController]
public sealed class HackathonsController(IHackathonService hackathonService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HackathonResponseDto>>> GetAllHackathons(CancellationToken cancellationToken) =>
        Ok(await hackathonService.GetPublicAsync(cancellationToken));

    [HttpGet("empresa")]
    public async Task<IActionResult> GetCompanyHackathons(CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await hackathonService.GetCompanyHackathonsAsync(accountId, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpPost("register/Hackathon")]
    public async Task<IActionResult> CreateHackathon([FromBody] CreateHackathonDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId)) return Unauthorized();
        var result = await hackathonService.CreateAsync(accountId, dto, cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetHackathonById), new { id = result.Value!.HackathonId }, result.Value)
            : ToActionResult(result, StatusCodes.Status201Created);
    }

    // Visitante anônimo também pode chamar; com token, quem gerencia a empresa vê rascunhos e cancelados.
    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetHackathonById(string id, CancellationToken cancellationToken)
    {
        Guid? accountId = TryGetAccountId(out var currentAccountId) ? currentAccountId : null;
        return ToActionResult(await hackathonService.GetByIdAsync(accountId, id, cancellationToken), StatusCodes.Status200OK);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateHackathon(Guid id, [FromBody] CreateHackathonDto dto, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await hackathonService.UpdateAsync(accountId, id, dto, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteHackathon(Guid id, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await hackathonService.DeleteAsync(accountId, id, cancellationToken), StatusCodes.Status204NoContent)
            : Unauthorized();

    [HttpPost("{id:guid}/publicar")]
    public async Task<IActionResult> PublishHackathon(Guid id, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await hackathonService.PublishAsync(accountId, id, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> CancelHackathon(Guid id, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await hackathonService.CancelAsync(accountId, id, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpPost("{id:guid}/encerrar")]
    public async Task<IActionResult> CloseHackathon(Guid id, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await hackathonService.CloseAsync(accountId, id, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    private bool TryGetAccountId(out Guid accountId) =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId);

    private IActionResult ToActionResult<T>(HackathonServiceResult<T> result, int successStatusCode)
    {
        if (result.Succeeded)
            return successStatusCode == StatusCodes.Status204NoContent ? NoContent() : StatusCode(successStatusCode, result.Value);
        var problem = new ProblemDetails { Detail = result.Message };
        return result.Error switch
        {
            HackathonServiceError.NotFound => NotFound(problem),
            HackathonServiceError.Forbidden => StatusCode(StatusCodes.Status403Forbidden, problem),
            HackathonServiceError.Conflict => Conflict(problem),
            HackathonServiceError.Validation => BadRequest(problem),
            _ => StatusCode(StatusCodes.Status500InternalServerError, problem)
        };
    }
}
