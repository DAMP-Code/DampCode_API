using DampCode_API.Data;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Tests;

public sealed class SubmissionServiceTests
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly DampCodeDbContext _dbContext;

    public SubmissionServiceTests() => _dbContext = TestDb.CreateShared(_databaseName);

    [Fact]
    public async Task Submission_PersistsWithVersions()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext, status: CompanyVerificationStatus.Verified);
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Team);
        var ana = await CompanySeed.ParticipantAsync(_dbContext, "ana@exemplo.com");
        var team = new HackathonTeam { HackathonId = hackathon.Id, CreatedByAccountId = ana.Id, Name = "Equipe", NormalizedName = "EQUIPE", MemberCount = 1 };
        _dbContext.HackathonTeams.Add(team);
        var submission = new Submission { HackathonId = hackathon.Id, TeamId = team.Id, Status = "Submitted", CurrentVersion = 1 };
        submission.Versions.Add(new SubmissionVersion
        {
            Submission = submission,
            Version = 1,
            Language = "Python",
            MongoDocumentId = "abc",
            SubmittedByAccountId = ana.Id
        });
        _dbContext.Submissions.Add(submission);
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.Submissions.Include(item => item.Versions).SingleAsync();
        Assert.Single(saved.Versions);
        Assert.Null(saved.ParticipationId);
        Assert.Null(saved.StageId);
    }
}
