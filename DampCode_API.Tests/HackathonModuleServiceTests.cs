using DampCode_API.Data;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Tests;

public sealed class HackathonModuleServiceTests
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly DampCodeDbContext _dbContext;

    public HackathonModuleServiceTests()
    {
        _dbContext = TestDb.CreateShared(_databaseName);
    }

    [Fact]
    public async Task Modules_PersistWithHackathon()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext, status: CompanyVerificationStatus.Verified);
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Individual);
        var module = new HackathonModule { HackathonId = hackathon.Id, Position = 1, Title = "Semana 1", Description = "Fundamentos" };
        module.Contents.Add(new HackathonModuleContent
        {
            Position = 1,
            ContentType = HackathonModuleContentType.Video,
            Title = "Aula de abertura",
            Url = "https://youtu.be/abc"
        });
        _dbContext.HackathonModules.Add(module);
        await _dbContext.SaveChangesAsync();

        var reloaded = await TestDb.CreateShared(_databaseName).Hackathons
            .Include(item => item.Modules).ThenInclude(item => item.Contents)
            .SingleAsync(item => item.Id == hackathon.Id);

        var saved = Assert.Single(reloaded.Modules);
        Assert.Equal("Fundamentos", saved.Description);
        var content = Assert.Single(saved.Contents);
        Assert.Equal(HackathonModuleContentType.Video, content.ContentType);
        Assert.Equal("https://youtu.be/abc", content.Url);
    }
}
