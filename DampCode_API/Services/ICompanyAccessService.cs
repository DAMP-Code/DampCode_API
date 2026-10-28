using DampCode_API.Dto;

namespace DampCode_API.Services;

public interface ICompanyAccessService
{
    // Empresa que a conta pode gerenciar: a própria (conta principal) ou a do vínculo ativo.
    Task<CompanyAccessDto?> GetManagedCompanyAsync(Guid accountId, CancellationToken cancellationToken);
}
