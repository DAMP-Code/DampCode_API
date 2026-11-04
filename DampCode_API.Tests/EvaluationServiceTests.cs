using DampCode_API.Data;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Tests;

public sealed class EvaluationServiceTests
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly DampCodeDbContext _dbContext;

    public EvaluationServiceTests()
    {
        _dbContext = TestDb.CreateShared(_databaseName);
    }

    [Fact]
    public async Task Prizes_PersistWithHackathon()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext, status: CompanyVerificationStatus.Verified);
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Individual,
            DateTime.UtcNow.AddHours(-1));
        _dbContext.HackathonPrizes.AddRange(
            new HackathonPrize { HackathonId = hackathon.Id, Position = 2, RewardType = HackathonRewardType.Interview, Description = "Entrevista com o time" },
            new HackathonPrize { HackathonId = hackathon.Id, Position = 1, RewardType = HackathonRewardType.Money, Amount = 1000m });
        await _dbContext.SaveChangesAsync();

        var reloaded = await TestDb.CreateShared(_databaseName).Hackathons.Include(item => item.Prizes)
            .SingleAsync(item => item.Id == hackathon.Id);

        var prizes = reloaded.Prizes.OrderBy(prize => prize.Position).ToList();
        Assert.Equal(2, prizes.Count);
        Assert.Equal(1000m, prizes[0].Amount);
        Assert.Null(prizes[1].Amount);
        Assert.Equal("Entrevista com o time", prizes[1].Description);
    }
}
