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
public sealed class HackathonsController(IHackathonService hackathonService, IParticipationService participationService,
    ISubmissionService submissionService) : ControllerBase
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

    [HttpPost("{id:guid}/inscricao")]
    public async Task<IActionResult> RegisterParticipant(Guid id, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await participationService.RegisterAsync(accountId, id, cancellationToken), StatusCodes.Status201Created)
            : Unauthorized();

    [HttpDelete("{id:guid}/inscricao")]
    public async Task<IActionResult> CancelParticipant(Guid id, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await participationService.CancelRegistrationAsync(accountId, id, cancellationToken), StatusCodes.Status204NoContent)
            : Unauthorized();

    // Equipes abertas com vaga, para quem quer pedir para entrar.
    [AllowAnonymous]
    [HttpGet("{id:guid}/equipes")]
    public async Task<IActionResult> GetOpenTeams(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await participationService.GetOpenTeamsAsync(id, cancellationToken), StatusCodes.Status200OK);

    [HttpPost("{id:guid}/equipes")]
    public async Task<IActionResult> CreateTeam(Guid id, [FromBody] CreateTeamDto dto, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await participationService.CreateTeamAsync(accountId, id, dto, cancellationToken), StatusCodes.Status201Created)
            : Unauthorized();

    // Entrega de código de quem está inscrito (individual ou equipe).
    [HttpGet("{id:guid}/entrega")]
    public async Task<IActionResult> GetMySubmission(Guid id, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await submissionService.GetMineAsync(accountId, id, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpGet("{id:guid}/entrega/versoes/{versao:int}")]
    public async Task<IActionResult> GetMySubmissionVersion(Guid id, int versao, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await submissionService.GetMyVersionAsync(accountId, id, versao, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpPost("{id:guid}/entrega")]
    public async Task<IActionResult> SubmitCode(Guid id, [FromBody] SubmitCodeDto dto, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await submissionService.SubmitAsync(accountId, id, dto, cancellationToken), StatusCodes.Status201Created)
            : Unauthorized();

    // Visão da empresa: entregas, versões e a solução de referência.
    [HttpGet("{id:guid}/entregas")]
    public async Task<IActionResult> GetSubmissions(Guid id, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await submissionService.GetEntriesAsync(accountId, id, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpGet("{id:guid}/entregas/{submissionId:guid}")]
    public async Task<IActionResult> GetSubmission(Guid id, Guid submissionId, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await submissionService.GetEntryAsync(accountId, id, submissionId, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpGet("{id:guid}/entregas/{submissionId:guid}/versoes/{versao:int}")]
    public async Task<IActionResult> GetSubmissionVersion(Guid id, Guid submissionId, int versao, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await submissionService.GetEntryVersionAsync(accountId, id, submissionId, versao, cancellationToken), StatusCodes.Status200OK)
            : Unauthorized();

    [HttpGet("{id:guid}/referencia")]
    public async Task<IActionResult> GetReferenceSolution(Guid id, CancellationToken cancellationToken) =>
        TryGetAccountId(out var accountId)
            ? ToActionResult(await submissionService.GetReferenceAsync(accountId, id, cancellationToken), StatusCodes.Status200OK)
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

    private IActionResult ToActionResult<T>(SubmissionServiceResult<T> result, int successStatusCode)
    {
        if (result.Succeeded) return StatusCode(successStatusCode, result.Value);
        var problem = new ProblemDetails { Detail = result.Message };
        return result.Error switch
        {
            SubmissionServiceError.NotFound => NotFound(problem),
            SubmissionServiceError.Forbidden => StatusCode(StatusCodes.Status403Forbidden, problem),
            SubmissionServiceError.Conflict => Conflict(problem),
            SubmissionServiceError.Validation => BadRequest(problem),
            _ => StatusCode(StatusCodes.Status500InternalServerError, problem)
        };
    }

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
