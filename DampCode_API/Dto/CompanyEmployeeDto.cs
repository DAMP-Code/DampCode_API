namespace DampCode_API.Dto;

public sealed record CompanyEmployeeDto(Guid AccountId, string Name, string Email, string? JobTitle, DateTime StartedAt);
