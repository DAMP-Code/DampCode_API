using DampCode_API.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using WebApiMongoDbDemo.Data;
using Microsoft.AspNetCore.Http;

namespace DampCode_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IMongoCollection<Accounts> _users;

        public UsersController(MongoDbService mongoDbService)
        {
            _users = mongoDbService.Database.GetCollection<Accounts>("users");
        }

        [HttpGet]
        public async Task<IEnumerable<Accounts>> GetAllUsers()
        {

            // sem filtros
            // var users = await _users.Find(FilterDefinition<User>.Empty).ToListAsync();

            // aceita todos
            var users = await _users.Find(_ => true).ToListAsync();

            return users;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Accounts>> GetUserById(string id)
        {
            var user = await _users.Find(u => u.Id == id).FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { message = "Usuário não encontrado!" });
            }

            return Ok(user);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] Accounts updatedUser)
        {
            if (id != updatedUser.Id)
            {
                return BadRequest(new { message = "O ID da URL não corresponde ao ID do usuário." });
            }

            var result = await _users.ReplaceOneAsync(u => u.Id == id, updatedUser);

            if (result.MatchedCount == 0)
            {
                return NotFound(new { message = "Usuário não encontrado para atualização." });
            }

            return Ok ();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var result = await _users.DeleteOneAsync(u => u.Id == id);

            if (result.DeletedCount == 0)
            {
                return NotFound(new { message = "Usuário não encontrado." });
            }

            return Ok();
        }
    }
}