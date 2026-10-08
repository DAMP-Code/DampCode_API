using System.Security.Claims;
using DampCode_API.Dto;
using DampCode_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DampCode_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register/participante")]
    public async Task<IActionResult> RegisterParticipant([FromBody] ParticipantDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await authService.RegisterParticipantAsync(dto, cancellationToken), StatusCodes.Status201Created);

    [AllowAnonymous]
    [HttpPost("register/empresa")]
    public async Task<IActionResult> RegisterCompany([FromBody] CompanyDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await authService.RegisterCompanyAsync(dto, cancellationToken), StatusCodes.Status201Created);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await authService.LoginAsync(dto, cancellationToken), StatusCodes.Status200OK);

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponseDto>> Me(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var accountId)) return Unauthorized();
        var user = await authService.GetAccountAsync(accountId, cancellationToken);
        return user is null ? NotFound(new { message = "Usuário não encontrado." }) : Ok(user);
    }

    private IActionResult ToActionResult(AuthServiceResult result, int successStatusCode)
    {
        if (result.Succeeded) return StatusCode(successStatusCode, result.Value);
        var problem = new ProblemDetails { Detail = result.Message };
        return result.Error switch
        {
            AuthServiceError.Conflict => Conflict(problem),
            AuthServiceError.InvalidCredentials => Unauthorized(problem),
            AuthServiceError.InactiveAccount => StatusCode(StatusCodes.Status403Forbidden, problem),
            _ => StatusCode(StatusCodes.Status500InternalServerError, problem)
        };
    }
}
