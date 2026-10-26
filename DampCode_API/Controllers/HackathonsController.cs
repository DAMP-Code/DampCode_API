using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace DampCode_API.Controllers;

// Hackathons ficam no MongoDB; contas e empresas continuam no PostgreSQL.
[Route("api/[controller]")]
[ApiController]
public sealed class HackathonsController(MongoDbService mongoDbService) : ControllerBase
{
    private readonly IMongoCollection<HackathonDocument> _hackathons =
        mongoDbService.Database!.GetCollection<HackathonDocument>("Hackathon");

    [HttpGet]
    public async Task<ActionResult<IEnumerable<HackathonDocument>>> GetAllHackathons(CancellationToken cancellationToken)
    {
        var hackathons = await _hackathons.Find(_ => true).ToListAsync(cancellationToken);
        return Ok(hackathons);
    }

    [HttpPost("register/Hackathon")]
    public async Task<ActionResult<HackathonDocument>> CreateHackathon([FromBody] CreateHackathonDto dto, CancellationToken cancellationToken)
    {
        var hackathon = new HackathonDocument
        {
            Titulo = dto.Titulo,
            Descricao = dto.Descricao,
            Empresa = dto.Empresa,
            Area = dto.Area,
            Tecnologias = dto.Tecnologias,
            Metodo = dto.Metodo,
            Ranking = dto.Ranking,
            Premiacao = dto.Premiacao,
            corPrincipal = dto.corPrincipal,
            corSecundaria = dto.corSecundaria,
            corFundo = dto.corFundo,
            Logo = dto.Logo,
            DataCriacao = dto.DataCriacao,
            DataFinal = dto.DataFinal,
            status = dto.status
        };
        await _hackathons.InsertOneAsync(hackathon, cancellationToken: cancellationToken);
        return CreatedAtAction(nameof(GetHackathonById), new { id = hackathon.HackathonId }, hackathon);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<HackathonDocument>> GetHackathonById(string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out _)) return NotFound(new { message = "Hackathon não encontrado." });
        var hackathon = await _hackathons.Find(item => item.HackathonId == id).FirstOrDefaultAsync(cancellationToken);
        return hackathon is null ? NotFound(new { message = "Hackathon não encontrado." }) : Ok(hackathon);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateHackathon(string id, [FromBody] HackathonDocument updateHackathon, CancellationToken cancellationToken)
    {
        if (id != updateHackathon.HackathonId) return BadRequest(new { message = "O ID da URL não corresponde ao ID do Hackathon." });
        if (!ObjectId.TryParse(id, out _)) return NotFound(new { message = "Hackathon não encontrado." });
        var result = await _hackathons.ReplaceOneAsync(item => item.HackathonId == id, updateHackathon, cancellationToken: cancellationToken);
        return result.MatchedCount == 0 ? NotFound(new { message = "Hackathon não encontrado." }) : NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteHackathon(string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out _)) return NotFound(new { message = "Hackathon não encontrado." });
        var result = await _hackathons.DeleteOneAsync(item => item.HackathonId == id, cancellationToken);
        return result.DeletedCount == 0 ? NotFound(new { message = "Hackathon não encontrado." }) : NoContent();
    }
}
