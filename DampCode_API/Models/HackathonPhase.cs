namespace DampCode_API.Models;

public enum HackathonPhase { Draft, Published, Active, InReview, Closed, Cancelled }

// Única regra de fase: o status gravado manda em rascunho, cancelado e encerrado manual;
// depois de publicado, as datas decidem (sem job em segundo plano).
public static class HackathonPhases
{
    public static HackathonPhase GetPhase(Hackathon hackathon, DateTime now) => hackathon.Status switch
    {
        HackathonStatus.Draft => HackathonPhase.Draft,
        HackathonStatus.Cancelled => HackathonPhase.Cancelled,
        HackathonStatus.Closed => HackathonPhase.Closed,
        _ when hackathon.StartsAt is null || now < hackathon.StartsAt => HackathonPhase.Published,
        _ when hackathon.EndsAt is null || now < hackathon.EndsAt => HackathonPhase.Active,
        _ when hackathon.ReviewEndsAt is null || now < hackathon.ReviewEndsAt => HackathonPhase.InReview,
        _ => HackathonPhase.Closed
    };

    public static string ToText(HackathonPhase phase) => phase switch
    {
        HackathonPhase.Draft => "Rascunho",
        HackathonPhase.Published => "Publicado",
        HackathonPhase.Active => "Ativo",
        HackathonPhase.InReview => "Em avaliação",
        HackathonPhase.Closed => "Encerrado",
        _ => "Cancelado"
    };
}
