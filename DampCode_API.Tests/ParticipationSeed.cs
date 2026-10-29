using DampCode_API.Data;
using DampCode_API.Models;

namespace DampCode_API.Tests;

internal static class ParticipationSeed
{
    // Hackathon publicado e completo gravado direto no banco, com início em 2 dias por padrão.
    public static async Task<Hackathon> PublishedHackathonAsync(DampCodeDbContext db, Company company,
        HackathonParticipationMode mode, DateTime? startsAt = null)
    {
        var start = startsAt ?? DateTime.UtcNow.AddDays(2);
        var hackathon = new Hackathon
        {
            CompanyId = company.Id,
            CreatedByAccountId = company.PrimaryAccountId,
            Title = "Hackathon publicado",
            Slug = $"hackathon-{Guid.NewGuid():N}",
            Description = "Descrição do Hackathon publicado",
            Area = "web",
            ParticipationMode = mode,
            Status = HackathonStatus.Scheduled,
            EvaluationMethod = "Avaliação Manual",
            PrizeAmount = 1000,
            RewardType = HackathonRewardType.Money,
            WinnersCount = 10,
            StartsAt = start,
            EndsAt = start.AddDays(3),
            ReviewEndsAt = start.AddDays(6),
            ScheduledAt = DateTime.UtcNow
        };
        db.Hackathons.Add(hackathon);
        await db.SaveChangesAsync();
        return hackathon;
    }
}
