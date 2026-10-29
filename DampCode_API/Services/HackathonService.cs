using System.Globalization;
using System.Text;
using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Services;

public sealed class HackathonService(DampCodeDbContext dbContext, ICompanyAccessService companyAccessService) : IHackathonService
{
    private const int MaxLogoLength = 2 * 1024 * 1024;
    private static readonly int[] WinnersCounts = [1, 10, 15, 20, 50, 100];
    private const string NoCompanyMessage = "Apenas empresas e seus funcionários podem gerenciar Hackathons.";
    private const string NotFoundMessage = "Hackathon não encontrado.";
    private const string DatesOrderMessage = "As datas precisam seguir: início, fim das submissões, fim da avaliação.";
    private const string FinishedMessage = "Este Hackathon já foi encerrado ou cancelado.";

    public async Task<HackathonServiceResult<HackathonResponseDto>> CreateAsync(Guid accountId, CreateHackathonDto dto, CancellationToken cancellationToken)
    {
        var company = await companyAccessService.GetManagedCompanyAsync(accountId, cancellationToken);
        if (company is null) return Fail<HackathonResponseDto>(HackathonServiceError.Forbidden, NoCompanyMessage);
        if (ValidateDraft(dto) is { } error) return Fail<HackathonResponseDto>(HackathonServiceError.Validation, error);

        var now = DateTime.UtcNow;
        var title = dto.Titulo!.Trim();
        var hackathon = new Hackathon
        {
            CompanyId = company.CompanyId,
            CreatedByAccountId = accountId,
            Title = title,
            Slug = await GenerateSlugAsync(title, cancellationToken),
            Description = dto.Descricao!.Trim(),
            Area = dto.Area!.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        ApplyContent(hackathon, dto);
        ApplyVisual(hackathon, dto);
        await SetTechnologiesAsync(hackathon, dto.Tecnologias ?? [], cancellationToken);
        dbContext.Hackathons.Add(hackathon);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(hackathon).Reference(item => item.Company).LoadAsync(cancellationToken);
        return new(true, ToDto(hackathon, now));
    }

    public async Task<HackathonServiceResult<HackathonResponseDto>> UpdateAsync(Guid accountId, Guid hackathonId, CreateHackathonDto dto, CancellationToken cancellationToken)
    {
        var (hackathon, failure) = await FindManagedAsync<HackathonResponseDto>(accountId, hackathonId, cancellationToken);
        if (hackathon is null) return failure!;

        var now = DateTime.UtcNow;
        var phase = HackathonPhases.GetPhase(hackathon, now);
        if (phase is HackathonPhase.Closed or HackathonPhase.Cancelled) return Fail<HackathonResponseDto>(HackathonServiceError.Conflict, FinishedMessage);

        if (phase == HackathonPhase.Draft)
        {
            if (ValidateDraft(dto) is { } error) return Fail<HackathonResponseDto>(HackathonServiceError.Validation, error);
            hackathon.Title = dto.Titulo!.Trim();
            hackathon.Description = dto.Descricao!.Trim();
            hackathon.Area = dto.Area!.Trim();
            ApplyContent(hackathon, dto);
            await SetTechnologiesAsync(hackathon, dto.Tecnologias ?? [], cancellationToken);
        }
        // Depois de publicado, só o visual muda; os campos cruciais que vierem são ignorados.
        else if (dto.Logo?.Length > MaxLogoLength)
        {
            return Fail<HackathonResponseDto>(HackathonServiceError.Validation, "O logo deve ter no máximo 2 MB.");
        }
        ApplyVisual(hackathon, dto);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, ToDto(hackathon, now));
    }

    public async Task<HackathonServiceResult<bool>> DeleteAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken)
    {
        var (hackathon, failure) = await FindManagedAsync<bool>(accountId, hackathonId, cancellationToken);
        if (hackathon is null) return failure!;
        if (hackathon.Status != HackathonStatus.Draft)
            return Fail<bool>(HackathonServiceError.Conflict, "Só é possível apagar rascunhos. Use cancelar.");

        dbContext.Hackathons.Remove(hackathon);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, true);
    }

    private async Task<(Hackathon? Hackathon, HackathonServiceResult<T>? Failure)> FindManagedAsync<T>(
        Guid accountId, Guid hackathonId, CancellationToken cancellationToken)
    {
        var company = await companyAccessService.GetManagedCompanyAsync(accountId, cancellationToken);
        if (company is null) return (null, Fail<T>(HackathonServiceError.Forbidden, NoCompanyMessage));

        var hackathon = await WithDetails(dbContext.Hackathons)
            .SingleOrDefaultAsync(item => item.Id == hackathonId && item.CompanyId == company.CompanyId, cancellationToken);
        return hackathon is null ? (null, Fail<T>(HackathonServiceError.NotFound, NotFoundMessage)) : (hackathon, null);
    }

    private static IQueryable<Hackathon> WithDetails(IQueryable<Hackathon> query) =>
        query.Include(item => item.Company).Include(item => item.Technologies).ThenInclude(item => item.Technology);

    private static string? ValidateDraft(CreateHackathonDto dto)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.Titulo)) missing.Add("título");
        if (string.IsNullOrWhiteSpace(dto.Descricao)) missing.Add("descrição");
        if (string.IsNullOrWhiteSpace(dto.Area)) missing.Add("área");
        if (ParseParticipationMode(dto.Formato) is null) missing.Add("formato");
        if (missing.Count > 0) return $"Preencha: {string.Join(", ", missing)}.";
        if (dto.Logo?.Length > MaxLogoLength) return "O logo deve ter no máximo 2 MB.";

        var startsAt = ToUtc(dto.DataInicio);
        var endsAt = ToUtc(dto.DataFinal);
        var reviewEndsAt = ToUtc(dto.DataFimAvaliacao);
        if (startsAt is not null && endsAt is not null && reviewEndsAt is not null && !(startsAt < endsAt && endsAt < reviewEndsAt))
            return DatesOrderMessage;
        return null;
    }

    private static void ApplyContent(Hackathon hackathon, CreateHackathonDto dto)
    {
        hackathon.ParticipationMode = ParseParticipationMode(dto.Formato)!.Value;
        hackathon.EvaluationMethod = Blank(dto.Metodo);
        hackathon.PrizeAmount = dto.Premiacao;
        hackathon.RewardType = ParseRewardType(dto.TipoPremiacao);
        hackathon.WinnersCount = ParseWinnersCount(dto.Ranking);
        hackathon.StartsAt = ToUtc(dto.DataInicio);
        hackathon.EndsAt = ToUtc(dto.DataFinal);
        hackathon.ReviewEndsAt = ToUtc(dto.DataFimAvaliacao);
    }

    private static void ApplyVisual(Hackathon hackathon, CreateHackathonDto dto)
    {
        hackathon.PrimaryColor = Blank(dto.corPrincipal);
        hackathon.SecondaryColor = Blank(dto.corSecundaria);
        hackathon.BackgroundColor = Blank(dto.corFundo);
        hackathon.Logo = Blank(dto.Logo);
    }

    // Remove só as que saíram e adiciona só as novas, para não reanexar a mesma chave.
    private async Task SetTechnologiesAsync(Hackathon hackathon, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var technologies = await ResolveTechnologiesAsync(names, cancellationToken);
        var ids = technologies.Select(item => item.Id).ToHashSet();
        foreach (var item in hackathon.Technologies.Where(item => !ids.Contains(item.TechnologyId)).ToList())
        {
            hackathon.Technologies.Remove(item);
            dbContext.HackathonTechnologies.Remove(item);
        }
        foreach (var technology in technologies.Where(technology => hackathon.Technologies.All(item => item.TechnologyId != technology.Id)))
            hackathon.Technologies.Add(new HackathonTechnology { Hackathon = hackathon, Technology = technology, TechnologyId = technology.Id });
    }

    private async Task<IReadOnlyCollection<Technology>> ResolveTechnologiesAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var distinctNames = names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (distinctNames.Length == 0) return [];

        var normalizedNames = distinctNames.Select(Normalize).ToArray();
        var existing = await dbContext.Technologies.Where(item => normalizedNames.Contains(item.NormalizedName))
            .ToDictionaryAsync(item => item.NormalizedName, cancellationToken);
        var result = new List<Technology>(distinctNames.Length);
        foreach (var name in distinctNames)
        {
            var normalizedName = Normalize(name);
            if (!existing.TryGetValue(normalizedName, out var technology))
            {
                technology = new Technology { Name = name, NormalizedName = normalizedName };
                dbContext.Technologies.Add(technology);
                existing.Add(normalizedName, technology);
            }
            result.Add(technology);
        }
        return result;
    }

    private async Task<string> GenerateSlugAsync(string title, CancellationToken cancellationToken)
    {
        var slug = Slugify(title);
        var taken = await dbContext.Hackathons.Where(item => item.Slug == slug || item.Slug.StartsWith(slug + "-"))
            .Select(item => item.Slug).ToListAsync(cancellationToken);
        if (!taken.Contains(slug)) return slug;
        var suffix = 2;
        while (taken.Contains($"{slug}-{suffix}")) suffix++;
        return $"{slug}-{suffix}";
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(character)) builder.Append(char.ToLowerInvariant(character));
            else if (builder.Length > 0 && builder[^1] != '-') builder.Append('-');
        }
        var slug = builder.ToString().Trim('-');
        if (slug.Length > 200) slug = slug[..200].TrimEnd('-');
        return slug.Length == 0 ? "hackathon" : slug;
    }

    private static HackathonResponseDto ToDto(Hackathon hackathon, DateTime now)
    {
        var phase = HackathonPhases.GetPhase(hackathon, now);
        return new HackathonResponseDto(
            hackathon.Id.ToString(),
            hackathon.Title,
            hackathon.Description,
            hackathon.Area,
            hackathon.Company.Name,
            hackathon.Technologies.Select(item => item.Technology.Name).Order(StringComparer.OrdinalIgnoreCase).ToList(),
            hackathon.EvaluationMethod,
            hackathon.WinnersCount is null ? null : $"Top {hackathon.WinnersCount}",
            hackathon.PrizeAmount ?? 0,
            hackathon.PrimaryColor,
            hackathon.SecondaryColor,
            hackathon.BackgroundColor,
            hackathon.Logo,
            hackathon.CreatedAt,
            hackathon.EndsAt,
            phase is HackathonPhase.Published or HackathonPhase.Active,
            "0 pessoas já entraram",
            HackathonPhases.ToText(phase),
            hackathon.StartsAt,
            hackathon.ReviewEndsAt,
            hackathon.ParticipationMode == HackathonParticipationMode.Team ? "Equipe" : "Individual",
            hackathon.RewardType switch
            {
                HackathonRewardType.Money => "Dinheiro",
                HackathonRewardType.Interview => "Entrevista",
                _ => null
            },
            hackathon.CompanyId);
    }

    private static HackathonParticipationMode? ParseParticipationMode(string? value) => Normalize(value ?? "") switch
    {
        "INDIVIDUAL" => HackathonParticipationMode.Individual,
        "EQUIPE" => HackathonParticipationMode.Team,
        _ => null
    };

    private static HackathonRewardType? ParseRewardType(string? value) => Normalize(value ?? "") switch
    {
        "DINHEIRO" => HackathonRewardType.Money,
        "ENTREVISTA" => HackathonRewardType.Interview,
        _ => null
    };

    // "Top 10" vira 10; só os valores do formulário são aceitos.
    private static int? ParseWinnersCount(string? value)
    {
        var text = Normalize(value ?? "");
        return text.StartsWith("TOP ") && int.TryParse(text[4..], out var count) && WinnersCounts.Contains(count) ? count : null;
    }

    private static DateTime? ToUtc(DateTime? value)
    {
        if (value is null) return null;
        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static HackathonServiceResult<T> Fail<T>(HackathonServiceError error, string message) => new(false, Error: error, Message: message);
}
