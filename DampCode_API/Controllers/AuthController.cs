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

    // Campos que o front deixa a pessoa mudar: nome (só participante) e cor do avatar.
    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateAccountDto dto, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var accountId)) return Unauthorized();
        var result = await authService.UpdateAccountAsync(accountId, dto, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : ToProblem(result.Error, result.Message);
    }

    // Devolve um token novo: os tokens antigos deixam de valer com a troca.
    [Authorize]
    [HttpPut("me/senha")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var accountId)) return Unauthorized();
        return ToActionResult(await authService.ChangePasswordAsync(accountId, dto, cancellationToken), StatusCodes.Status200OK);
    }

    private IActionResult ToActionResult(AuthServiceResult result, int successStatusCode) =>
        result.Succeeded ? StatusCode(successStatusCode, result.Value) : ToProblem(result.Error, result.Message);

    private IActionResult ToProblem(AuthServiceError error, string? message)
    {
        var problem = new ProblemDetails { Detail = message };
        return error switch
        {
            AuthServiceError.Conflict => Conflict(problem),
            AuthServiceError.InvalidCredentials => Unauthorized(problem),
            AuthServiceError.InactiveAccount => StatusCode(StatusCodes.Status403Forbidden, problem),
            AuthServiceError.Validation => BadRequest(problem),
            AuthServiceError.NotFound => NotFound(problem),
            _ => StatusCode(StatusCodes.Status500InternalServerError, problem)
        };
    }
}
