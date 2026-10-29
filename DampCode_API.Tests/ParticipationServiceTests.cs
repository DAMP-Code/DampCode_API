using DampCode_API.Data;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Tests;

public sealed class ParticipationServiceTests
{
    private readonly DampCodeDbContext _dbContext = TestDb.Create();

    [Fact]
    public async Task Participation_PersistsWithTeam()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext, status: CompanyVerificationStatus.Verified);
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Team);
        var participant = await CompanySeed.ParticipantAsync(_dbContext, "ana@exemplo.com");
        var team = new HackathonTeam
        {
            HackathonId = hackathon.Id,
            CreatedByAccountId = participant.Id,
            Name = "Os Rápidos",
            NormalizedName = "OS RÁPIDOS",
            MemberCount = 1
        };
        _dbContext.HackathonTeams.Add(team);
        _dbContext.Participations.Add(new Participation { HackathonId = hackathon.Id, AccountId = participant.Id, TeamId = team.Id });
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.Participations.Include(item => item.Team).SingleAsync();
        Assert.Equal("Os Rápidos", saved.Team!.Name);
        Assert.Null(saved.LeftAt);
    }
}
