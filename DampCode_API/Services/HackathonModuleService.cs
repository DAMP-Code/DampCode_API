using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Services;

public sealed class HackathonModuleService(DampCodeDbContext dbContext, ICompanyAccessService companyAccessService) : IHackathonModuleService
{
    private const int MaxModules = 20;
    private const int MaxContents = 30;
    private const int MaxModuleTitle = 120;
    private const int MaxModuleDescription = 1000;
    private const int MaxContentTitle = 160;
    private const int MaxContentBody = 20_000;
    private const int MaxContentDescription = 1000;
    private const int MaxUrl = 2048;
    private const string NotFoundMessage = "Hackathon não encontrado.";
    private const string ModuleNotFoundMessage = "Módulo não encontrado.";
    private const string ContentNotFoundMessage = "Conteúdo não encontrado.";
    private const string ForbiddenMessage = "Apenas quem gerencia a empresa dona do Hackathon pode editar os módulos.";
    private const string FinishedMessage = "Hackathon encerrado ou cancelado não aceita mudanças nos módulos.";
    private const string ChangedMessage = "Os módulos mudaram. Recarregue a página.";

    // Mesma visibilidade de GET /api/Hackathons/{id}: rascunho e cancelado só para quem gerencia a empresa.
    public async Task<HackathonModuleServiceResult<IReadOnlyList<HackathonModuleDto>>> GetAsync(Guid? accountId, string id,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Hackathons.AsNoTracking();
        var hackathon = Guid.TryParse(id, out var hackathonId)
            ? await query.SingleOrDefaultAsync(item => item.Id == hackathonId, cancellationToken)
            : await query.SingleOrDefaultAsync(item => item.Slug == id, cancellationToken);
        if (hackathon is null) return Fail<IReadOnlyList<HackathonModuleDto>>(HackathonModuleServiceError.NotFound, NotFoundMessage);

        if (HackathonPhases.GetPhase(hackathon, DateTime.UtcNow) is HackathonPhase.Draft or HackathonPhase.Cancelled)
        {
            var company = accountId is null ? null : await companyAccessService.GetManagedCompanyAsync(accountId.Value, cancellationToken);
            if (company?.CompanyId != hackathon.CompanyId)
                return Fail<IReadOnlyList<HackathonModuleDto>>(HackathonModuleServiceError.NotFound, NotFoundMessage);
        }
        return new(true, await LoadModulesAsync(hackathon.Id, cancellationToken));
    }

    public async Task<HackathonModuleServiceResult<HackathonModuleDto>> CreateModuleAsync(Guid accountId, Guid hackathonId, SaveHackathonModuleDto dto,
        CancellationToken cancellationToken)
    {
        if (await FindEditableAsync<HackathonModuleDto>(accountId, hackathonId, cancellationToken) is { } failure) return failure;
        if (ValidateModule(dto) is { } error) return Fail<HackathonModuleDto>(HackathonModuleServiceError.Validation, error);

        var count = await dbContext.HackathonModules.CountAsync(item => item.HackathonId == hackathonId, cancellationToken);
        if (count >= MaxModules)
            return Fail<HackathonModuleDto>(HackathonModuleServiceError.Conflict, $"O Hackathon já tem {MaxModules} módulos, o máximo permitido.");

        // Entra no fim; duas inclusões ao mesmo tempo esbarram no índice único da posição.
        var module = new HackathonModule
        {
            HackathonId = hackathonId,
            Position = count + 1,
            Title = dto.Titulo!.Trim(),
            Description = Blank(dto.Descricao)
        };
        dbContext.HackathonModules.Add(module);
        if (!await TrySaveAsync(cancellationToken)) return Fail<HackathonModuleDto>(HackathonModuleServiceError.Conflict, ChangedMessage);
        return new(true, ToDto(module));
    }

    public async Task<HackathonModuleServiceResult<HackathonModuleDto>> UpdateModuleAsync(Guid accountId, Guid hackathonId, Guid moduleId,
        SaveHackathonModuleDto dto, CancellationToken cancellationToken)
    {
        if (await FindEditableAsync<HackathonModuleDto>(accountId, hackathonId, cancellationToken) is { } failure) return failure;
        var module = await FindModuleAsync(hackathonId, moduleId, cancellationToken);
        if (module is null) return Fail<HackathonModuleDto>(HackathonModuleServiceError.NotFound, ModuleNotFoundMessage);
        if (ValidateModule(dto) is { } error) return Fail<HackathonModuleDto>(HackathonModuleServiceError.Validation, error);

        module.Title = dto.Titulo!.Trim();
        module.Description = Blank(dto.Descricao);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, ToDto(module));
    }

    public async Task<HackathonModuleServiceResult<bool>> DeleteModuleAsync(Guid accountId, Guid hackathonId, Guid moduleId,
        CancellationToken cancellationToken)
    {
        if (await FindEditableAsync<bool>(accountId, hackathonId, cancellationToken) is { } failure) return failure;
        var modules = await ModulesQuery(hackathonId).ToListAsync(cancellationToken);
        var module = modules.SingleOrDefault(item => item.Id == moduleId);
        if (module is null) return Fail<bool>(HackathonModuleServiceError.NotFound, ModuleNotFoundMessage);

        // Os conteúdos saem junto (cascade); os módulos seguintes sobem uma posição.
        dbContext.HackathonModules.Remove(module);
        modules.Remove(module);
        if (!await TryRepositionAsync(modules, (item, position) => item.Position = position, cancellationToken))
            return Fail<bool>(HackathonModuleServiceError.Conflict, ChangedMessage);
        return new(true, true);
    }

    public async Task<HackathonModuleServiceResult<IReadOnlyList<HackathonModuleDto>>> ReorderModulesAsync(Guid accountId, Guid hackathonId,
        HackathonModuleOrderDto dto, CancellationToken cancellationToken)
    {
        if (await FindEditableAsync<IReadOnlyList<HackathonModuleDto>>(accountId, hackathonId, cancellationToken) is { } failure) return failure;
        var modules = await ModulesQuery(hackathonId).ToListAsync(cancellationToken);
        var ordered = Reorder(modules, dto.Ids, item => item.Id);
        if (ordered is null)
            return Fail<IReadOnlyList<HackathonModuleDto>>(HackathonModuleServiceError.Validation, "Envie todos os módulos na nova ordem, sem repetir.");

        if (!await TryRepositionAsync(ordered, (item, position) => item.Position = position, cancellationToken))
            return Fail<IReadOnlyList<HackathonModuleDto>>(HackathonModuleServiceError.Conflict, ChangedMessage);
        return new(true, ordered.Select(ToDto).ToList());
    }

    public async Task<HackathonModuleServiceResult<HackathonModuleContentDto>> CreateContentAsync(Guid accountId, Guid hackathonId, Guid moduleId,
        SaveHackathonModuleContentDto dto, CancellationToken cancellationToken)
    {
        if (await FindEditableAsync<HackathonModuleContentDto>(accountId, hackathonId, cancellationToken) is { } failure) return failure;
        var module = await FindModuleAsync(hackathonId, moduleId, cancellationToken);
        if (module is null) return Fail<HackathonModuleContentDto>(HackathonModuleServiceError.NotFound, ModuleNotFoundMessage);
        if (ValidateContent(dto) is { } error) return Fail<HackathonModuleContentDto>(HackathonModuleServiceError.Validation, error);
        if (module.Contents.Count >= MaxContents)
            return Fail<HackathonModuleContentDto>(HackathonModuleServiceError.Conflict, $"O módulo já tem {MaxContents} conteúdos, o máximo permitido.");

        var content = new HackathonModuleContent { ModuleId = module.Id, Position = module.Contents.Count + 1, Title = "" };
        ApplyContent(content, dto);
        dbContext.HackathonModuleContents.Add(content);
        if (!await TrySaveAsync(cancellationToken)) return Fail<HackathonModuleContentDto>(HackathonModuleServiceError.Conflict, ChangedMessage);
        return new(true, ToDto(content));
    }

    public async Task<HackathonModuleServiceResult<HackathonModuleContentDto>> UpdateContentAsync(Guid accountId, Guid hackathonId, Guid moduleId,
        Guid contentId, SaveHackathonModuleContentDto dto, CancellationToken cancellationToken)
    {
        if (await FindEditableAsync<HackathonModuleContentDto>(accountId, hackathonId, cancellationToken) is { } failure) return failure;
        var module = await FindModuleAsync(hackathonId, moduleId, cancellationToken);
        if (module is null) return Fail<HackathonModuleContentDto>(HackathonModuleServiceError.NotFound, ModuleNotFoundMessage);
        var content = module.Contents.SingleOrDefault(item => item.Id == contentId);
        if (content is null) return Fail<HackathonModuleContentDto>(HackathonModuleServiceError.NotFound, ContentNotFoundMessage);
        if (ValidateContent(dto) is { } error) return Fail<HackathonModuleContentDto>(HackathonModuleServiceError.Validation, error);

        ApplyContent(content, dto);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(true, ToDto(content));
    }

    public async Task<HackathonModuleServiceResult<bool>> DeleteContentAsync(Guid accountId, Guid hackathonId, Guid moduleId, Guid contentId,
        CancellationToken cancellationToken)
    {
        if (await FindEditableAsync<bool>(accountId, hackathonId, cancellationToken) is { } failure) return failure;
        var module = await FindModuleAsync(hackathonId, moduleId, cancellationToken);
        if (module is null) return Fail<bool>(HackathonModuleServiceError.NotFound, ModuleNotFoundMessage);
        var content = module.Contents.SingleOrDefault(item => item.Id == contentId);
        if (content is null) return Fail<bool>(HackathonModuleServiceError.NotFound, ContentNotFoundMessage);

        dbContext.HackathonModuleContents.Remove(content);
        var remaining = module.Contents.Where(item => item.Id != contentId).OrderBy(item => item.Position).ToList();
        if (!await TryRepositionAsync(remaining, (item, position) => item.Position = position, cancellationToken))
            return Fail<bool>(HackathonModuleServiceError.Conflict, ChangedMessage);
        return new(true, true);
    }

    public async Task<HackathonModuleServiceResult<HackathonModuleDto>> ReorderContentsAsync(Guid accountId, Guid hackathonId, Guid moduleId,
        HackathonModuleOrderDto dto, CancellationToken cancellationToken)
    {
        if (await FindEditableAsync<HackathonModuleDto>(accountId, hackathonId, cancellationToken) is { } failure) return failure;
        var module = await FindModuleAsync(hackathonId, moduleId, cancellationToken);
        if (module is null) return Fail<HackathonModuleDto>(HackathonModuleServiceError.NotFound, ModuleNotFoundMessage);
        var ordered = Reorder(module.Contents.ToList(), dto.Ids, item => item.Id);
        if (ordered is null)
            return Fail<HackathonModuleDto>(HackathonModuleServiceError.Validation, "Envie todos os conteúdos do módulo na nova ordem, sem repetir.");

        if (!await TryRepositionAsync(ordered, (item, position) => item.Position = position, cancellationToken))
            return Fail<HackathonModuleDto>(HackathonModuleServiceError.Conflict, ChangedMessage);
        return new(true, ToDto(module));
    }

    // Quem não gerencia a empresa dona recebe 404 se não pode nem ver o Hackathon (rascunho, cancelado) e 403 se pode.
    private async Task<HackathonModuleServiceResult<T>?> FindEditableAsync<T>(Guid accountId, Guid hackathonId, CancellationToken cancellationToken)
    {
        var hackathon = await dbContext.Hackathons.AsNoTracking().SingleOrDefaultAsync(item => item.Id == hackathonId, cancellationToken);
        if (hackathon is null) return Fail<T>(HackathonModuleServiceError.NotFound, NotFoundMessage);

        var phase = HackathonPhases.GetPhase(hackathon, DateTime.UtcNow);
        var company = await companyAccessService.GetManagedCompanyAsync(accountId, cancellationToken);
        if (company?.CompanyId != hackathon.CompanyId)
            return phase is HackathonPhase.Draft or HackathonPhase.Cancelled
                ? Fail<T>(HackathonModuleServiceError.NotFound, NotFoundMessage)
                : Fail<T>(HackathonModuleServiceError.Forbidden, ForbiddenMessage);
        // Material cresce durante o desafio: muda em qualquer fase, menos depois de encerrado ou cancelado.
        return phase is HackathonPhase.Closed or HackathonPhase.Cancelled ? Fail<T>(HackathonModuleServiceError.Conflict, FinishedMessage) : null;
    }

    private IQueryable<HackathonModule> ModulesQuery(Guid hackathonId) =>
        dbContext.HackathonModules.Include(item => item.Contents)
            .Where(item => item.HackathonId == hackathonId)
            .OrderBy(item => item.Position);

    private Task<HackathonModule?> FindModuleAsync(Guid hackathonId, Guid moduleId, CancellationToken cancellationToken) =>
        dbContext.HackathonModules.Include(item => item.Contents)
            .SingleOrDefaultAsync(item => item.Id == moduleId && item.HackathonId == hackathonId, cancellationToken);

    private async Task<IReadOnlyList<HackathonModuleDto>> LoadModulesAsync(Guid hackathonId, CancellationToken cancellationToken)
    {
        var modules = await ModulesQuery(hackathonId).AsNoTracking().ToListAsync(cancellationToken);
        return modules.Select(ToDto).ToList();
    }

    // A lista precisa ter exatamente os ids atuais, cada um uma vez; devolve os itens na nova ordem.
    private static List<TItem>? Reorder<TItem>(List<TItem> items, List<Guid>? ids, Func<TItem, Guid> getId)
    {
        if (ids is null || ids.Count != items.Count || ids.Distinct().Count() != ids.Count) return null;
        var byId = items.ToDictionary(getId);
        return ids.All(byId.ContainsKey) ? ids.Select(id => byId[id]).ToList() : null;
    }

    // Grava 1, 2, 3... na ordem da lista. Passa antes por posições negativas para não esbarrar no índice único no meio do caminho.
    private async Task<bool> TryRepositionAsync<TItem>(List<TItem> items, Action<TItem, int> setPosition, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            for (var index = 0; index < items.Count; index++) setPosition(items[index], -(index + 1));
            await dbContext.SaveChangesAsync(cancellationToken);
            for (var index = 0; index < items.Count; index++) setPosition(items[index], index + 1);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Outra pessoa da empresa mexeu nos mesmos itens ao mesmo tempo.
            return false;
        }
    }

    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private static string? ValidateModule(SaveHackathonModuleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Titulo)) return "Preencha o título do módulo.";
        if (dto.Titulo.Trim().Length > MaxModuleTitle) return $"O título do módulo deve ter no máximo {MaxModuleTitle} caracteres.";
        if (dto.Descricao?.Trim().Length > MaxModuleDescription)
            return $"A descrição do módulo deve ter no máximo {MaxModuleDescription} caracteres.";
        return null;
    }

    private static string? ValidateContent(SaveHackathonModuleContentDto dto)
    {
        var type = HackathonModuleContentTypes.Parse(dto.Tipo);
        if (type is null) return "Escolha o tipo do conteúdo: Texto, Link ou Video.";
        if (string.IsNullOrWhiteSpace(dto.Titulo)) return "Preencha o título do conteúdo.";
        if (dto.Titulo.Trim().Length > MaxContentTitle) return $"O título do conteúdo deve ter no máximo {MaxContentTitle} caracteres.";

        if (type == HackathonModuleContentType.Text)
        {
            if (string.IsNullOrWhiteSpace(dto.Texto)) return "Preencha o texto do conteúdo.";
            return dto.Texto.Trim().Length > MaxContentBody ? $"O texto do conteúdo deve ter no máximo {MaxContentBody} caracteres." : null;
        }
        if (string.IsNullOrWhiteSpace(dto.Url)) return "Informe a URL do conteúdo.";
        var url = dto.Url.Trim();
        if (url.Length > MaxUrl) return $"A URL deve ter no máximo {MaxUrl} caracteres.";
        if (!IsWebUrl(url)) return "A URL deve começar com http:// ou https://.";
        return dto.Texto?.Trim().Length > MaxContentDescription
            ? $"A descrição do conteúdo deve ter no máximo {MaxContentDescription} caracteres."
            : null;
    }

    // A URL vai direto para href/src no front: só http e https.
    private static bool IsWebUrl(string url) =>
        (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);

    // Texto ignora a url; Link e Video guardam o texto só como descrição curta.
    private static void ApplyContent(HackathonModuleContent content, SaveHackathonModuleContentDto dto)
    {
        content.ContentType = HackathonModuleContentTypes.Parse(dto.Tipo)!.Value;
        content.Title = dto.Titulo!.Trim();
        content.Body = Blank(dto.Texto);
        content.Url = content.ContentType == HackathonModuleContentType.Text ? null : dto.Url!.Trim();
    }

    private static HackathonModuleDto ToDto(HackathonModule module) =>
        new(module.Id, module.Title, module.Description, module.Position,
            module.Contents.OrderBy(item => item.Position).Select(ToDto).ToList());

    private static HackathonModuleContentDto ToDto(HackathonModuleContent content) =>
        new(content.Id, HackathonModuleContentTypes.ToText(content.ContentType), content.Title, content.Body, content.Url, content.Position);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static HackathonModuleServiceResult<T> Fail<T>(HackathonModuleServiceError error, string message) => new(false, Error: error, Message: message);
}
