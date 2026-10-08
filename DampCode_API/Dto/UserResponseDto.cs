namespace DampCode_API.Dto;

public sealed record UserResponseDto(Guid Id, string Name, string Email, string Role, int? Level, decimal? Xp);
