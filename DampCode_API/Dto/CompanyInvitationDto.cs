namespace DampCode_API.Dto;

public sealed record CompanyInvitationDto(Guid Id, string Email, string? JobTitle, DateTime ExpiresAt, DateTime CreatedAt);
