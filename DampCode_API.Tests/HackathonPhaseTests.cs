using DampCode_API.Models;

namespace DampCode_API.Tests;

public sealed class HackathonPhaseTests
{
    private static readonly DateTime Start = new(2030, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    private static Hackathon NewHackathon(HackathonStatus status) => new()
    {
        Title = "Desafio",
        Slug = "desafio",
        Description = "Descrição",
        Area = "web",
        Status = status,
        StartsAt = Start,
        EndsAt = Start.AddDays(2),
        ReviewEndsAt = Start.AddDays(4)
    };

    [Theory]
    [InlineData(HackathonStatus.Draft, HackathonPhase.Draft)]
    [InlineData(HackathonStatus.Cancelled, HackathonPhase.Cancelled)]
    [InlineData(HackathonStatus.Closed, HackathonPhase.Closed)]
    public void GetPhase_StoredStatus(HackathonStatus status, HackathonPhase expected)
    {
        var hackathon = NewHackathon(status);
        Assert.Equal(expected, HackathonPhases.GetPhase(hackathon, Start.AddDays(-10)));
        Assert.Equal(expected, HackathonPhases.GetPhase(hackathon, Start.AddDays(1)));
    }

    [Theory]
    [InlineData(-1, HackathonPhase.Published)]
    [InlineData(0, HackathonPhase.Active)]
    [InlineData(2 * 86400 - 1, HackathonPhase.Active)]
    [InlineData(2 * 86400, HackathonPhase.InReview)]
    [InlineData(4 * 86400 - 1, HackathonPhase.InReview)]
    [InlineData(4 * 86400, HackathonPhase.Closed)]
    public void GetPhase_Scheduled_FollowsDates(int secondsFromStart, HackathonPhase expected) =>
        Assert.Equal(expected, HackathonPhases.GetPhase(NewHackathon(HackathonStatus.Scheduled), Start.AddSeconds(secondsFromStart)));

    [Theory]
    [InlineData(HackathonPhase.Draft, "Rascunho")]
    [InlineData(HackathonPhase.Published, "Publicado")]
    [InlineData(HackathonPhase.Active, "Ativo")]
    [InlineData(HackathonPhase.InReview, "Em avaliação")]
    [InlineData(HackathonPhase.Closed, "Encerrado")]
    [InlineData(HackathonPhase.Cancelled, "Cancelado")]
    public void ToText_ReturnsPortugueseNames(HackathonPhase phase, string expected) =>
        Assert.Equal(expected, HackathonPhases.ToText(phase));
}
