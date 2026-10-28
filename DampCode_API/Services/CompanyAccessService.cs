using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Services;

public sealed class CompanyAccessService(DampCodeDbContext dbContext) : ICompanyAccessService
{
    public async Task<CompanyAccessDto?> GetManagedCompanyAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var primary = await dbContext.Companies.AsNoTracking()
            .SingleOrDefaultAsync(company => company.PrimaryAccountId == accountId && company.DeletedAt == null, cancellationToken);
        if (primary is not null) return ToAccess(primary, true);

        var employer = await dbContext.CompanyMemberships.AsNoTracking()
            .Where(item => item.AccountId == accountId && item.EndedAt == null && item.Company.DeletedAt == null)
            .Select(item => item.Company)
            .SingleOrDefaultAsync(cancellationToken);
        return employer is null ? null : ToAccess(employer, false);
    }

    private static CompanyAccessDto ToAccess(Company company, bool isPrimaryAccount) =>
        new(company.Id, company.Name, company.VerificationStatus.ToString(), isPrimaryAccount,
            company.VerificationStatus == CompanyVerificationStatus.Verified);
}
