using Microsoft.AspNetCore.Mvc;

namespace DampCode_API.Controllers;

// O CRUD legado dependia do documento Mongo anterior. Ele será reativado quando
// o fluxo de empresa, autorização e publicação for implementado sobre o novo modelo.
[Route("api/[controller]")]
[ApiController]
public sealed class HackathonsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAllHackathons() => StatusCode(
        StatusCodes.Status501NotImplemented,
        new { message = "O fluxo de Hackathons ainda não foi migrado para o novo modelo de dados." });
}
