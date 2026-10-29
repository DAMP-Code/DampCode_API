namespace DampCode_API.Dto;

public sealed record ReceivedInvitationDto(Guid Id, string CompanyName, string? JobTitle, DateTime ExpiresAt);
