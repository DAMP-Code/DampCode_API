using DampCode_API.Dto;
using DampCode_API.Models;
using DnsClient;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using WebApiMongoDbDemo.Data;

namespace DampCode_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IMongoCollection<Accounts> _users;
        public AuthController(MongoDbService mongoDbService)
        {
            _users = mongoDbService.Database.GetCollection<Accounts>("users");
        }

        //CREATE
        //[HttpPost]
        //public async Task<ActionResult> CreateUser(User user)
        //{
        //    await _users.InsertOneAsync(user);

        //    return CreatedAtAction(
        //        nameof(GetById), //pegar do outro controller
        //        new { id = user.Id },
        //        user
        //    );
        //}

        // Create participante
        [HttpPost("register/participante")]
        public async Task<IActionResult> RegisterParticipant(ParticipanteDto dto)
        {
            var user = new Accounts
            {
                Name = dto.Name,
                Email = dto.Email,
                Password = BCrypt.Net.BCrypt.EnhancedHashPassword(dto.Password),
                Role = "participante",
                Nivel = 1,
                Xp = 0,
                Tecnologias = dto.Tecnologias
            };

            await _users.InsertOneAsync(user);

            Console.WriteLine(user.Id);  

            return Ok(user);

           // return CreatedAtAction(
           //      nameof(user), //pegar do outro controller
           //       new { id = user.Id },
           //       user
           //);
        }

        [HttpPost("register/empresa")]
        public async Task<IActionResult> RegisterEmpresa(EmpresaDto dto) {
            var user = new Accounts
            {
                Name = dto.Name,
                Email = dto.Email,
                Password = BCrypt.Net.BCrypt.EnhancedHashPassword(dto.Password),
                Cnpj = dto.Cnpj,
                Role = "empresa",
                Verificado = dto.Verificado,
                HackatonsIds = dto.HackatonsIds
            };
            await _users.InsertOneAsync(user);
            return Ok();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            try
            {
                var user = await _users
                    .Find(u => u.Email == dto.Email)
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Unauthorized("Email ou senha inválidos");

                bool senhaValida = BCrypt.Net.BCrypt.EnhancedVerify(dto.Password, user.Password);

                if (!senhaValida)
                    return Unauthorized("Email ou senha inválidos");

                return Ok(new
                {
                    id = user.Id,
                    name = user.Name,
                    email = user.Email,
                    role = user.Role
                });
            }
            catch (Exception ex)
            {
                // log (importante)
                Console.WriteLine(ex.Message);

                return StatusCode(500, "Erro interno no servidor");
            }

          }

        }

}
