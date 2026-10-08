using DampCode_API.Models;

namespace DampCode_API.Services;

public interface ITokenService
{
    TokenResult GenerateToken(Account account);
}

public sealed record TokenResult(string Token, DateTime ExpiresAt);
