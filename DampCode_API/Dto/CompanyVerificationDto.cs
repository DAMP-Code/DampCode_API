namespace DampCode_API.Dto;

public sealed record CompanyVerificationDto(Guid Id, string Name, string Cnpj, string? Area, DateTime CreatedAt);
