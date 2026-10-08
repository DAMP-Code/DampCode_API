using DampCode_API.Dto;
using DampCode_API.Services;
using Microsoft.AspNetCore.Mvc;

namespace DampCode_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register/participante")]
    public async Task<IActionResult> RegisterParticipant([FromBody] ParticipanteDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await authService.RegisterParticipantAsync(dto, cancellationToken), StatusCodes.Status201Created);

    [HttpPost("register/empresa")]
    public async Task<IActionResult> RegisterCompany([FromBody] EmpresaDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await authService.RegisterCompanyAsync(dto, cancellationToken), StatusCodes.Status201Created);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await authService.LoginAsync(dto, cancellationToken), StatusCodes.Status200OK);

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
