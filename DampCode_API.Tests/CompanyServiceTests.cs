using DampCode_API.Data;
using DampCode_API.Models;

namespace DampCode_API.Tests;

public sealed class CompanyServiceTests
{
    private readonly DampCodeDbContext _dbContext = TestDb.Create();

    [Fact]
    public async Task CompanyInvitation_PersistsWithCompany()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        _dbContext.CompanyInvitations.Add(new CompanyInvitation
        {
            CompanyId = company.Id,
            InvitedByAccountId = company.PrimaryAccountId,
            Email = "maria@exemplo.com",
            NormalizedEmail = "MARIA@EXEMPLO.COM",
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await _dbContext.SaveChangesAsync();

        Assert.Equal(CompanyInvitationStatus.Pending, _dbContext.CompanyInvitations.Single().Status);
    }
}
