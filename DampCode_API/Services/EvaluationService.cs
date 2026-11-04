using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Services;

public sealed class EvaluationService(DampCodeDbContext dbContext, ICompanyAccessService companyAccessService, ISubmissionVersionStore versionStore)
    : IEvaluationService
{
    private const int MaxScore = 100;
    private const int MaxFeedbackLength = 1000;
    private const string NotFoundMessage = "Hackathon não encontrado.";
    private const string FinishedMessage = "Este Hackathon já foi encerrado ou cancelado.";
    private const string PublishedMessage = "O resultado já foi publicado.";
    private const string NotOpenMessage = "A avaliação abre quando as submissões terminam.";
    private const string WinnersMessage = "Escolha um vencedor diferente para cada prêmio.";

    public async Task<EvaluationServiceResult<EvaluationRankingDto>> GetRankingAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken)
    {
        var (hackathon, failure) = await FindManagedHackathonAsync<EvaluationRankingDto>(accountId, hackathonId, cancellationToken);
        if (hackathon is null) return failure!;
        return new(true, await BuildRankingAsync(hackathon, cancellationToken));
    }

    public async Task<EvaluationServiceResult<EvaluationEntryDto>> EvaluateAsync(Guid accountId, Guid hackathonId, Guid submissionId, EvaluateDto dto,
        CancellationToken cancellationToken)
    {
        var (hackathon, failure) = await FindManagedHackathonAsync<EvaluationEntryDto>(accountId, hackathonId, cancellationToken);
        if (hackathon is null) return failure!;
        if (CheckWritable<EvaluationEntryDto>(hackathon) is { } locked) return locked;
        if (dto.Nota is null or < 0 or > MaxScore) return Fail<EvaluationEntryDto>(EvaluationServiceError.Validation, "A nota deve ficar entre 0 e 100.");
        var feedback = string.IsNullOrWhiteSpace(dto.Comentario) ? null : dto.Comentario.Trim();
        if (feedback?.Length > MaxFeedbackLength)
            return Fail<EvaluationEntryDto>(EvaluationServiceError.Validation, "O comentário deve ter no máximo 1000 caracteres.");

        var submission = await SubmissionsQuery(hackathonId, tracked: true).SingleOrDefaultAsync(item => item.Id == submissionId, cancellationToken);
        if (submission is null) return Fail<EvaluationEntryDto>(EvaluationServiceError.NotFound, "Entrega não encontrada.");

        var now = DateTime.UtcNow;
        submission.Score = dto.Nota;
        submission.Feedback = feedback;
        submission.EvaluatedAt = now;
        submission.EvaluatedByAccountId = accountId;
        submission.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, (await BuildEntriesAsync(hackathon, [submission], cancellationToken)).Single());
    }

    public async Task<EvaluationServiceResult<EvaluationRankingDto>> SetWinnersAsync(Guid accountId, Guid hackathonId, SetWinnersDto dto,
        CancellationToken cancellationToken)
    {
        var (hackathon, failure) = await FindManagedHackathonAsync<EvaluationRankingDto>(accountId, hackathonId, cancellationToken);
        if (hackathon is null) return failure!;
        if (CheckWritable<EvaluationRankingDto>(hackathon) is { } locked) return locked;

        var winners = dto.Vencedores ?? [];
        var positions = hackathon.Prizes.Select(prize => prize.Position).ToHashSet();
        if (winners.Any(item => !positions.Contains(item.Posicao))
            || winners.Select(item => item.Posicao).Distinct().Count() != winners.Count
            || winners.Select(item => item.SubmissionId).Distinct().Count() != winners.Count)
            return Fail<EvaluationRankingDto>(EvaluationServiceError.Validation, WinnersMessage);

        var submissions = await SubmissionsQuery(hackathonId, tracked: true).ToListAsync(cancellationToken);
        if (winners.Any(item => submissions.All(submission => submission.Id != item.SubmissionId)))
            return Fail<EvaluationRankingDto>(EvaluationServiceError.NotFound, "Entrega não encontrada.");

        // Zera tudo antes de atribuir, para a troca de posições não esbarrar no índice único no meio do caminho.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            foreach (var submission in submissions) submission.PrizePosition = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            foreach (var winner in winners) submissions.Single(item => item.Id == winner.SubmissionId).PrizePosition = winner.Posicao;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Outra pessoa da empresa salvou os vencedores ao mesmo tempo.
            return Fail<EvaluationRankingDto>(EvaluationServiceError.Conflict, "Os vencedores mudaram. Recarregue a página.");
        }
        return new(true, await BuildRankingAsync(hackathon, cancellationToken));
    }

    public async Task<EvaluationServiceResult<bool>> PublishResultsAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken)
    {
        var (hackathon, failure) = await FindManagedHackathonAsync<bool>(accountId, hackathonId, cancellationToken, tracked: true);
        if (hackathon is null) return failure!;
        if (CheckWritable<bool>(hackathon) is { } locked) return locked;

        var submissions = await SubmissionsQuery(hackathonId, tracked: true).ToListAsync(cancellationToken);
        if (submissions.Any(item => item.Score is null))
            return Fail<bool>(EvaluationServiceError.Conflict, "Dê nota a todas as entregas antes de publicar.");

        // Premiados ficam na posição do prêmio; os demais seguem o ranking a partir da primeira posição depois dos prêmios.
        var now = DateTime.UtcNow;
        var next = hackathon.Prizes.Count + 1;
        foreach (var submission in Rank(submissions))
        {
            submission.FinalPosition = submission.PrizePosition ?? next++;
            submission.Status = "Evaluated";
            submission.UpdatedAt = now;
        }
        if (HackathonPhases.GetPhase(hackathon, now) == HackathonPhase.InReview)
        {
            hackathon.Status = HackathonStatus.Closed;
            hackathon.ClosedAt = now;
        }
        hackathon.ResultsPublishedAt = now;
        hackathon.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, true);
    }

    // Nota e vencedores só mudam depois do fim das submissões, enquanto o resultado não foi publicado.
    public static bool CanEvaluate(Hackathon hackathon, DateTime now) =>
        hackathon.Status != HackathonStatus.Cancelled
        && hackathon.ResultsPublishedAt is null
        && HackathonPhases.GetPhase(hackathon, now) is HackathonPhase.InReview or HackathonPhase.Closed;

    // Semelhança da versão que vale com a referência; nula sem referência ou sem código.
    public static async Task<int?> SuggestAsync(Hackathon hackathon, Submission submission, string? language, ISubmissionVersionStore store,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hackathon.ReferenceCode) || submission.CurrentMongoVersionId is null) return null;
        var code = await store.GetCodeAsync(submission.CurrentMongoVersionId, cancellationToken);
        return code is null ? null : SimilarityScore.Compute(hackathon.ReferenceCode, code, language ?? hackathon.ReferenceLanguage ?? "Outra");
    }

    private static EvaluationServiceResult<T>? CheckWritable<T>(Hackathon hackathon)
    {
        if (hackathon.Status == HackathonStatus.Cancelled) return Fail<T>(EvaluationServiceError.Conflict, FinishedMessage);
        if (hackathon.ResultsPublishedAt is not null) return Fail<T>(EvaluationServiceError.Conflict, PublishedMessage);
        return CanEvaluate(hackathon, DateTime.UtcNow) ? null : Fail<T>(EvaluationServiceError.Conflict, NotOpenMessage);
    }

    private async Task<EvaluationRankingDto> BuildRankingAsync(Hackathon hackathon, CancellationToken cancellationToken)
    {
        var submissions = await SubmissionsQuery(hackathon.Id, tracked: false).ToListAsync(cancellationToken);
        var entries = await BuildEntriesAsync(hackathon, Rank(submissions), cancellationToken);
        return new EvaluationRankingDto(CanEvaluate(hackathon, DateTime.UtcNow), hackathon.ResultsPublishedAt is not null,
            HackathonService.ToPrizeDtos(hackathon.Prizes), entries);
    }

    // Nome da equipe ou da pessoa, como na lista de entregas; linguagem da versão que vale.
    private async Task<IReadOnlyList<EvaluationEntryDto>> BuildEntriesAsync(Hackathon hackathon, IEnumerable<Submission> submissions,
        CancellationToken cancellationToken)
    {
        var list = submissions.ToList();
        var ids = list.Select(item => item.Id).ToList();
        var languages = await dbContext.SubmissionVersions.AsNoTracking()
            .Where(item => ids.Contains(item.SubmissionId))
            .Select(item => new { item.SubmissionId, item.Version, item.Language })
            .ToListAsync(cancellationToken);

        var entries = new List<EvaluationEntryDto>();
        foreach (var submission in list)
        {
            var language = languages.FirstOrDefault(item => item.SubmissionId == submission.Id && item.Version == submission.CurrentVersion)?.Language;
            var name = submission.Team?.Name ?? submission.Participation?.Account.UserProfile?.Name ?? submission.Participation?.Account.Email ?? "";
            entries.Add(new EvaluationEntryDto(submission.Id, name, language,
                await SuggestAsync(hackathon, submission, language, versionStore, cancellationToken),
                submission.Score, submission.Feedback, submission.PrizePosition, submission.LastSubmittedAt));
        }
        return entries;
    }

    // Maior nota primeiro, sem nota no fim; empate para quem enviou a versão que vale antes.
    private static IEnumerable<Submission> Rank(IEnumerable<Submission> submissions) =>
        submissions.OrderBy(item => item.Score is null).ThenByDescending(item => item.Score).ThenBy(item => item.LastSubmittedAt);

    // Só entra quem entregou alguma versão.
    private IQueryable<Submission> SubmissionsQuery(Guid hackathonId, bool tracked)
    {
        var query = tracked ? dbContext.Submissions : dbContext.Submissions.AsNoTracking();
        return query
            .Include(item => item.Team)
            .Include(item => item.Participation).ThenInclude(item => item!.Account).ThenInclude(item => item.UserProfile)
            .Where(item => item.HackathonId == hackathonId && item.CurrentVersion > 0);
    }

    // Mesma regra de acesso da etapa 4: conta principal ou funcionário da empresa dona.
    private async Task<(Hackathon? Hackathon, EvaluationServiceResult<T>? Failure)> FindManagedHackathonAsync<T>(
        Guid accountId, Guid hackathonId, CancellationToken cancellationToken, bool tracked = false)
    {
        var company = await companyAccessService.GetManagedCompanyAsync(accountId, cancellationToken);
        if (company is null)
            return (null, Fail<T>(EvaluationServiceError.Forbidden, "Apenas empresas e seus funcionários podem gerenciar Hackathons."));
        var query = tracked ? dbContext.Hackathons : dbContext.Hackathons.AsNoTracking();
        var hackathon = await query.Include(item => item.Prizes)
            .SingleOrDefaultAsync(item => item.Id == hackathonId && item.CompanyId == company.CompanyId, cancellationToken);
        return hackathon is null ? (null, Fail<T>(EvaluationServiceError.NotFound, NotFoundMessage)) : (hackathon, null);
    }

    private static EvaluationServiceResult<T> Fail<T>(EvaluationServiceError error, string message) => new(false, Error: error, Message: message);
}
