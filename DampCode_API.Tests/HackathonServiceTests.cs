using DampCode_API.Data;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Tests;

public sealed class HackathonServiceTests
{
    private readonly DampCodeDbContext _dbContext = TestDb.Create();

    [Fact]
    public async Task Hackathon_PersistsWithCompanyAndTechnologies()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var hackathon = new Hackathon
        {
            CompanyId = company.Id,
            CreatedByAccountId = company.PrimaryAccountId,
            Title = "Desafio de Rotas",
            Slug = "desafio-de-rotas",
            Description = "Descrição do desafio",
            Area = "web"
        };
        hackathon.Technologies.Add(new HackathonTechnology
        {
            Hackathon = hackathon,
            Technology = new Technology { Name = "React", NormalizedName = "REACT" }
        });
        _dbContext.Hackathons.Add(hackathon);
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.Hackathons.Include(item => item.Company).Include(item => item.Technologies).SingleAsync();
        Assert.Equal("Empresa Exemplo", saved.Company.Name);
        Assert.Single(saved.Technologies);
        Assert.Equal(HackathonStatus.Draft, saved.Status);
    }
}
