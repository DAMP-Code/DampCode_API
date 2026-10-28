using DampCode_API.Data;
using DampCode_API.Models;
using DampCode_API.Services;

namespace DampCode_API.Tests;

public sealed class CompanyAccessServiceTests
{
    private readonly DampCodeDbContext _dbContext = TestDb.Create();
    private readonly CompanyAccessService _service;

    public CompanyAccessServiceTests() => _service = new CompanyAccessService(_dbContext);

    private async Task<CompanyMembership> AddMembershipAsync(Company company, Account account, DateTime? endedAt = null)
    {
        var membership = new CompanyMembership { CompanyId = company.Id, AccountId = account.Id, EndedAt = endedAt };
        _dbContext.CompanyMemberships.Add(membership);
        await _dbContext.SaveChangesAsync();
        return membership;
    }

    [Fact]
    public async Task GetManagedCompany_PrimaryAccount_ReturnsCompanyAsPrimary()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);

        var result = await _service.GetManagedCompanyAsync(company.PrimaryAccountId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(company.Id, result.CompanyId);
        Assert.Equal("Empresa Exemplo", result.Name);
        Assert.True(result.IsPrimaryAccount);
    }

    [Fact]
    public async Task GetManagedCompany_ActiveEmployee_ReturnsCompanyAsEmployee()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var employee = await CompanySeed.ParticipantAsync(_dbContext, "maria@exemplo.com");
        await AddMembershipAsync(company, employee);

        var result = await _service.GetManagedCompanyAsync(employee.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(company.Id, result.CompanyId);
        Assert.False(result.IsPrimaryAccount);
    }

    [Fact]
    public async Task GetManagedCompany_RemovedEmployee_ReturnsNull()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var employee = await CompanySeed.ParticipantAsync(_dbContext, "maria@exemplo.com");
        await AddMembershipAsync(company, employee, DateTime.UtcNow);

        Assert.Null(await _service.GetManagedCompanyAsync(employee.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetManagedCompany_ParticipantWithoutCompany_ReturnsNull()
    {
        var participant = await CompanySeed.ParticipantAsync(_dbContext, "maria@exemplo.com");

        Assert.Null(await _service.GetManagedCompanyAsync(participant.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetManagedCompany_DeletedCompany_ReturnsNull()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var employee = await CompanySeed.ParticipantAsync(_dbContext, "maria@exemplo.com");
        await AddMembershipAsync(company, employee);
        company.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        Assert.Null(await _service.GetManagedCompanyAsync(company.PrimaryAccountId, CancellationToken.None));
        Assert.Null(await _service.GetManagedCompanyAsync(employee.Id, CancellationToken.None));
    }

    [Theory]
    [InlineData(CompanyVerificationStatus.Pending, false)]
    [InlineData(CompanyVerificationStatus.Verified, true)]
    [InlineData(CompanyVerificationStatus.Rejected, false)]
    public async Task GetManagedCompany_CanPublishOnlyWhenVerified(CompanyVerificationStatus status, bool canPublish)
    {
        var company = await CompanySeed.CompanyAsync(_dbContext, status: status);

        var result = await _service.GetManagedCompanyAsync(company.PrimaryAccountId, CancellationToken.None);

        Assert.Equal(canPublish, result!.CanPublish);
        Assert.Equal(status.ToString(), result.VerificationStatus);
    }
}
