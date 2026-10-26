using DampCode_API.Data;
using DampCode_API.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public sealed class UsersController(DampCodeDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetAllUsers(CancellationToken cancellationToken)
    {
        var users = await dbContext.Accounts.AsNoTracking().Where(account => account.DeletedAt == null)
            .SelectUserResponse()
            .ToListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponseDto>> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Accounts.AsNoTracking().Where(account => account.Id == id && account.DeletedAt == null)
            .SelectUserResponse()
            .SingleOrDefaultAsync(cancellationToken);
        return user is null ? NotFound(new { message = "Usuário não encontrado." }) : Ok(user);
    }
}
