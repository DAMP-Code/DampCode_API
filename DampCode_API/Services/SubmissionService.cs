using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Services;

// Regras de código compartilhadas pela entrega e pela solução de referência do Hackathon.
public static class SubmissionRules
{
    public const int MaxCodeLength = 100 * 1024;
    public static readonly string[] Languages = ["JavaScript", "Python", "C#", "Java", "SQL", "Outra"];
}

public sealed class SubmissionService(DampCodeDbContext dbContext, ICompanyAccessService companyAccessService, ISubmissionVersionStore versionStore)
    : ISubmissionService
{
    private const string NotFoundMessage = "Hackathon não encontrado.";
    private const string VersionNotFoundMessage = "Versão não encontrada.";

    public async Task<SubmissionServiceResult<MySubmissionDto>> GetMineAsync(Guid accountId, Guid hackathonId, CancellationToken cancellationToken)
    {
        var (hackathon, participation, failure) = await FindParticipationAsync<MySubmissionDto>(accountId, hackathonId, cancellationToken);
        if (failure is not null) return failure;

        var teamName = participation!.TeamId is { } teamId
            ? await dbContext.HackathonTeams.AsNoTracking().Where(item => item.Id == teamId).Select(item => item.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        var submission = await FindOwnSubmissionAsync(participation, cancellationToken);
        var versions = submission is null ? [] : await LoadVersionsAsync(submission, cancellationToken);
        var current = submission is null ? null : await LoadCodeAsync(submission.Id, submission.CurrentVersion, cancellationToken);
        var open = HackathonPhases.GetPhase(hackathon!, DateTime.UtcNow) == HackathonPhase.Active;
        return new(true, new MySubmissionDto(open, hackathon!.StartsAt, hackathon.EndsAt, teamName, versions, current));
    }

    public async Task<SubmissionServiceResult<SubmissionCodeDto>> GetMyVersionAsync(Guid accountId, Guid hackathonId, int version, CancellationToken cancellationToken)
    {
        var (_, participation, failure) = await FindParticipationAsync<SubmissionCodeDto>(accountId, hackathonId, cancellationToken);
        if (failure is not null) return failure;

        var submission = await FindOwnSubmissionAsync(participation!, cancellationToken);
        var code = submission is null ? null : await LoadCodeAsync(submission.Id, version, cancellationToken);
        return code is null ? Fail<SubmissionCodeDto>(SubmissionServiceError.NotFound, VersionNotFoundMessage) : new(true, code);
    }

    public async Task<SubmissionServiceResult<SubmissionCodeDto>> SubmitAsync(Guid accountId, Guid hackathonId, SubmitCodeDto dto, CancellationToken cancellationToken)
    {
        var (hackathon, participation, failure) = await FindParticipationAsync<SubmissionCodeDto>(accountId, hackathonId, cancellationToken);
        if (failure is not null) return failure;
        if (HackathonPhases.GetPhase(hackathon!, DateTime.UtcNow) != HackathonPhase.Active)
            return Fail<SubmissionCodeDto>(SubmissionServiceError.Conflict, "As submissões deste Hackathon estão fechadas.");
        if (ValidateCode(dto.Linguagem, dto.Codigo) is { } error) return Fail<SubmissionCodeDto>(SubmissionServiceError.Validation, error);

        var submission = await FindOwnSubmissionAsync(participation!, cancellationToken, tracked: true);
        if (submission is null)
        {
            submission = new Submission
            {
                HackathonId = hackathonId,
                ParticipationId = participation!.TeamId is null ? participation.Id : null,
                TeamId = participation.TeamId,
                Status = "Submitted"
            };
            dbContext.Submissions.Add(submission);
        }

        // O código vai primeiro para o MongoDB; a versão só passa a existir quando a linha entra no PostgreSQL.
        var now = DateTime.UtcNow;
        var number = submission.CurrentVersion + 1;
        var fileName = string.IsNullOrWhiteSpace(dto.NomeArquivo) ? null : dto.NomeArquivo.Trim()[..Math.Min(dto.NomeArquivo.Trim().Length, 255)];
        var documentId = await versionStore.InsertAsync(submission.Id, dto.Codigo!, cancellationToken);
        dbContext.SubmissionVersions.Add(new SubmissionVersion
        {
            SubmissionId = submission.Id,
            Version = number,
            Language = dto.Linguagem!,
            FileName = fileName,
            MongoDocumentId = documentId,
            SubmittedByAccountId = accountId,
            SubmittedAt = now
        });
        submission.CurrentVersion = number;
        submission.CurrentMongoVersionId = documentId;
        submission.FirstSubmittedAt ??= now;
        submission.LastSubmittedAt = now;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Dois integrantes enviando ao mesmo tempo: CurrentVersion (token de concorrência) ou os índices únicos barram um deles.
            // Se apagar o documento falhar, ele fica órfão e não atrapalha: o número da versão vem do PostgreSQL.
            try
            {
                await versionStore.DeleteAsync(documentId, cancellationToken);
            }
            catch (Exception)
            {
            }
            return Fail<SubmissionCodeDto>(SubmissionServiceError.Conflict, "Outra versão foi enviada ao mesmo tempo. Tente novamente.");
        }
        return new(true, new SubmissionCodeDto(number, dto.Linguagem!, fileName, dto.Codigo!));
    }

    // Linguagem da lista, código não vazio e dentro do limite. Também usado pela solução de referência.
    public static string? ValidateCode(string? language, string? code)
    {
        if (language is null || !SubmissionRules.Languages.Contains(language)) return "Escolha uma linguagem.";
        if (string.IsNullOrWhiteSpace(code)) return "Envie algum código.";
        if (code.Length > SubmissionRules.MaxCodeLength) return "O código deve ter no máximo 100 KB.";
        return null;
    }

    // Hackathon público e inscrição ativa da conta nele.
    private async Task<(Hackathon? Hackathon, Participation? Participation, SubmissionServiceResult<T>? Failure)> FindParticipationAsync<T>(
        Guid accountId, Guid hackathonId, CancellationToken cancellationToken)
    {
        var hackathon = await dbContext.Hackathons.AsNoTracking().SingleOrDefaultAsync(item => item.Id == hackathonId, cancellationToken);
        if (hackathon is null || HackathonPhases.GetPhase(hackathon, DateTime.UtcNow) is HackathonPhase.Draft or HackathonPhase.Cancelled)
            return (null, null, Fail<T>(SubmissionServiceError.NotFound, NotFoundMessage));

        var participation = await dbContext.Participations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.HackathonId == hackathonId && item.AccountId == accountId && item.LeftAt == null, cancellationToken);
        if (participation is null) return (null, null, Fail<T>(SubmissionServiceError.Forbidden, "Você não está inscrito neste Hackathon."));
        return (hackathon, participation, null);
    }

    // A entrega é da equipe (Hackathon em equipe) ou da própria inscrição (individual).
    private Task<Submission?> FindOwnSubmissionAsync(Participation participation, CancellationToken cancellationToken, bool tracked = false)
    {
        var query = tracked ? dbContext.Submissions : dbContext.Submissions.AsNoTracking();
        return participation.TeamId is { } teamId
            ? query.SingleOrDefaultAsync(item => item.TeamId == teamId, cancellationToken)
            : query.SingleOrDefaultAsync(item => item.ParticipationId == participation.Id, cancellationToken);
    }

    private async Task<IReadOnlyList<SubmissionVersionDto>> LoadVersionsAsync(Submission submission, CancellationToken cancellationToken)
    {
        var current = submission.CurrentVersion;
        return await dbContext.SubmissionVersions.AsNoTracking()
            .Where(item => item.SubmissionId == submission.Id)
            .OrderByDescending(item => item.Version)
            .Select(item => new SubmissionVersionDto(item.Version, item.Language, item.FileName,
                item.SubmittedByAccount.UserProfile != null ? item.SubmittedByAccount.UserProfile.Name : item.SubmittedByAccount.Email,
                item.SubmittedAt, item.Version == current))
            .ToListAsync(cancellationToken);
    }

    private async Task<SubmissionCodeDto?> LoadCodeAsync(Guid submissionId, int version, CancellationToken cancellationToken)
    {
        var row = await dbContext.SubmissionVersions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.SubmissionId == submissionId && item.Version == version, cancellationToken);
        if (row is null) return null;
        var code = await versionStore.GetCodeAsync(row.MongoDocumentId, cancellationToken);
        return new SubmissionCodeDto(row.Version, row.Language, row.FileName, code ?? "");
    }

    private static SubmissionServiceResult<T> Fail<T>(SubmissionServiceError error, string message) => new(false, Error: error, Message: message);
}
