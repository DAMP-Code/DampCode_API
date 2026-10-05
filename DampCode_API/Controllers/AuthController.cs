using DampCode_API.Dto;
using DampCode_API.Models;
using DnsClient;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using DampCode_API.Data;

namespace DampCode_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IMongoCollection<User> _users;
        public AuthController(MongoDbService mongoDbService)
        {
            _users = mongoDbService.Database.GetCollection<User>("users");
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
        [ActionName("RegisterParticipant")]
        public async Task<IActionResult> registerParticipant(ParticipantDto dto)
        {
            var user = new User
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
        [ActionName("RegisterEmpresa")]
        public async Task<IActionResult> registerCompany(CompanyDto dto) {
            var user = new User
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
        [ActionName("Login")]
        public async Task<IActionResult> login(LoginDto dto)
        {
            try
            {
                var user = await _users
                    .Find(u => u.Email == dto.Email)
                    .FirstOrDefaultAsync();

                if (user == null)
                    return Unauthorized("Email ou senha inválidos");

                bool isPasswordValid = BCrypt.Net.BCrypt.EnhancedVerify(dto.Password, user.Password);

                if (!isPasswordValid)
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
