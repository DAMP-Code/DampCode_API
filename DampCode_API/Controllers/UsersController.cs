using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class UsersController(DampCodeDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetAllUsers(CancellationToken cancellationToken)
    {
        var users = await dbContext.Accounts.AsNoTracking().Where(account => account.DeletedAt == null)
            .Select(account => new UserResponseDto(
                account.Id,
                account.AccountType == AccountType.Company ? account.PrimaryCompany!.Name : account.UserProfile!.Name,
                account.Email,
                account.AccountType == AccountType.Company ? "empresa" : "participante",
                account.UserProfile == null ? null : account.UserProfile.Level,
                account.UserProfile == null ? null : account.UserProfile.Xp))
            .ToListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponseDto>> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Accounts.AsNoTracking().Where(account => account.Id == id && account.DeletedAt == null)
            .Select(account => new UserResponseDto(
                account.Id,
                account.AccountType == AccountType.Company ? account.PrimaryCompany!.Name : account.UserProfile!.Name,
                account.Email,
                account.AccountType == AccountType.Company ? "empresa" : "participante",
                account.UserProfile == null ? null : account.UserProfile.Level,
                account.UserProfile == null ? null : account.UserProfile.Xp))
            .SingleOrDefaultAsync(cancellationToken);
        return user is null ? NotFound(new { message = "Usuário não encontrado." }) : Ok(user);
    }
}
