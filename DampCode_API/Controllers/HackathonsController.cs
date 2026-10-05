using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using DampCode_API.Data;

namespace DampCode_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HackathonsController : ControllerBase
    {
        private readonly IMongoCollection<Hackathon> _hackathons;

        public HackathonsController(MongoDbService mongoDbService)
        {
            _hackathons = mongoDbService.Database?.GetCollection<Hackathon>("Hackathon");
        }
        [HttpGet]

        [ActionName("GetAllHackathons")]
        public async Task<ActionResult<IEnumerable<Hackathon>>> getAllHackathons()
        {
            var hackathons = await _hackathons.Find(hackathon => true).ToListAsync();
            return Ok(hackathons);
        }

        [HttpPost("register/Hackathon")]
        [ActionName("CreateHackathon")]
        public async Task<ActionResult<Hackathon>> createHackathon([FromBody] CreateHackathonDto hackathonDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var hackathon = new Hackathon
            {
                Titulo = hackathonDto.Titulo,
                Descricao = hackathonDto.Descricao,
                Empresa = hackathonDto.Empresa,
                Area = hackathonDto.Area,
                Tecnologias = hackathonDto.Tecnologias,
                Metodo = hackathonDto.Metodo,
                Ranking = hackathonDto.Ranking,
                Premiacao = hackathonDto.Premiacao,
                corPrincipal = hackathonDto.corPrincipal,
                corSecundaria = hackathonDto.corSecundaria,
                corFundo = hackathonDto.corFundo,
                Logo = hackathonDto.Logo,
                DataCriacao = hackathonDto.DataCriacao,
                DataFinal = hackathonDto.DataFinal,
                status = hackathonDto.status
            };
            await _hackathons.InsertOneAsync(hackathon);
            return CreatedAtAction("GetHackathonById", new { id = hackathon.HackathonId }, hackathon);
        }

        [HttpGet("{id}")]
        [ActionName("GetHackathonById")]
        public async Task<ActionResult<Hackathon>> getHackathonById(string id)
        {
            var hackathon = await _hackathons.Find(h => h.HackathonId == id).FirstOrDefaultAsync();
            if (hackathon == null)
            {
                return NotFound(new { message = "Hackathon não encontrado. " });
            }
            return Ok(hackathon);
        }
        [HttpPut("{id}")]
        [ActionName("UpdateHackathon")]
        public async Task<IActionResult> updateHackathon(string id, [FromBody] Hackathon updateHackathon)
        {
            if(id != updateHackathon.HackathonId)
            {
                return BadRequest(new { message = "O ID da URL não corresponde ao ID do usuário." });
            }

            var result = await _hackathons.ReplaceOneAsync(h =>  h.HackathonId == id, updateHackathon);

            //matched count, ver
            if (result.MatchedCount == 0) 
            {
                return NotFound(new {message = "Usuário não encontrado para atualização." });
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        [ActionName("DeleteHackathon")]
        public async Task<IActionResult> deleteHackathon(string id)
        {
            var result = await _hackathons.DeleteOneAsync(h => h.HackathonId == id);
            if(result.DeletedCount == 0)
            {
                return NotFound(new { message = "Usuário não encontrado. " });
            }
            return NoContent();
        }
    }
}
