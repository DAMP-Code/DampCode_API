namespace DampCode_API.Dto;

public sealed record CompanyAccessDto(Guid CompanyId, string Name, string VerificationStatus, bool IsPrimaryAccount, bool CanPublish);
