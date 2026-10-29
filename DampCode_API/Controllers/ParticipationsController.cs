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
public sealed class ParticipationsController(IParticipationService participationService) : ControllerBase
{
    // Painel do participante: inscrições, equipe, convites e pedidos.
    [HttpGet("me")]
    public async Task<ActionResult<ParticipationsOverviewDto>> Me(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var accountId)) return Unauthorized();
        return Ok(await participationService.GetMineAsync(accountId, cancellationToken));
    }
}
