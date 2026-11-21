using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using DampCode_API.Services;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Tests;

public sealed class HackathonModuleServiceTests
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly DampCodeDbContext _dbContext;
    private readonly HackathonModuleService _service;

    public HackathonModuleServiceTests()
    {
        _dbContext = TestDb.CreateShared(_databaseName);
        _service = new HackathonModuleService(_dbContext, new CompanyAccessService(_dbContext));
    }

    // Hackathon publicado (início em 2 dias) de uma empresa verificada.
    private async Task<(Company Company, Hackathon Hackathon)> PublishedAsync(string email = "empresa@exemplo.com")
    {
        var company = await CompanySeed.CompanyAsync(_dbContext, email, CompanyVerificationStatus.Verified);
        var hackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Individual);
        return (company, hackathon);
    }

    private async Task<HackathonModuleDto> AddModuleAsync(Company company, Hackathon hackathon, string title)
    {
        var result = await _service.CreateModuleAsync(company.PrimaryAccountId, hackathon.Id, new SaveHackathonModuleDto { Titulo = title },
            CancellationToken.None);
        Assert.True(result.Succeeded, result.Message);
        return result.Value!;
    }

    private async Task<HackathonModuleContentDto> AddContentAsync(Company company, Hackathon hackathon, Guid moduleId, string title)
    {
        var result = await _service.CreateContentAsync(company.PrimaryAccountId, hackathon.Id, moduleId,
            new SaveHackathonModuleContentDto { Tipo = "Texto", Titulo = title, Texto = "Leia com atenção." }, CancellationToken.None);
        Assert.True(result.Succeeded, result.Message);
        return result.Value!;
    }

    private static HackathonModuleOrderDto Order(params Guid[] ids) => new() { Ids = ids.ToList() };

    [Fact]
    public async Task Modules_PersistWithHackathon()
    {
        var (_, hackathon) = await PublishedAsync();
        var module = new HackathonModule { HackathonId = hackathon.Id, Position = 1, Title = "Semana 1", Description = "Fundamentos" };
        module.Contents.Add(new HackathonModuleContent
        {
            Position = 1,
            ContentType = HackathonModuleContentType.Video,
            Title = "Aula de abertura",
            Url = "https://youtu.be/abc"
        });
        _dbContext.HackathonModules.Add(module);
        await _dbContext.SaveChangesAsync();

        var reloaded = await TestDb.CreateShared(_databaseName).Hackathons
            .Include(item => item.Modules).ThenInclude(item => item.Contents)
            .SingleAsync(item => item.Id == hackathon.Id);

        var saved = Assert.Single(reloaded.Modules);
        Assert.Equal("Fundamentos", saved.Description);
        var content = Assert.Single(saved.Contents);
        Assert.Equal(HackathonModuleContentType.Video, content.ContentType);
        Assert.Equal("https://youtu.be/abc", content.Url);
    }

    [Fact]
    public async Task CreateModule_AppendsAtTheEnd()
    {
        var (company, hackathon) = await PublishedAsync();
        await AddModuleAsync(company, hackathon, "Semana 1");

        var result = await _service.CreateModuleAsync(company.PrimaryAccountId, hackathon.Id,
            new SaveHackathonModuleDto { Titulo = "  Semana 2  ", Descricao = "   " }, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Semana 2", result.Value!.Titulo);
        Assert.Null(result.Value.Descricao);
        Assert.Equal(2, result.Value.Posicao);
        Assert.Empty(result.Value.Conteudos);
    }

    [Fact]
    public async Task CreateModule_ByEmployee_Succeeds()
    {
        var (company, hackathon) = await PublishedAsync();
        var employee = await CompanySeed.ParticipantAsync(_dbContext, "funcionario@exemplo.com");
        _dbContext.CompanyMemberships.Add(new CompanyMembership { CompanyId = company.Id, AccountId = employee.Id });
        await _dbContext.SaveChangesAsync();

        var result = await _service.CreateModuleAsync(employee.Id, hackathon.Id, new SaveHackathonModuleDto { Titulo = "Semana 1" },
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
    }

    [Fact]
    public async Task CreateModule_ByOtherCompany_ReturnsForbidden()
    {
        var (_, hackathon) = await PublishedAsync();
        var other = await CompanySeed.CompanyAsync(_dbContext, "outra@exemplo.com");

        var result = await _service.CreateModuleAsync(other.PrimaryAccountId, hackathon.Id, new SaveHackathonModuleDto { Titulo = "Semana 1" },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(HackathonModuleServiceError.Forbidden, result.Error);
    }

    [Fact]
    public async Task CreateModule_ByParticipant_ReturnsForbidden()
    {
        var (_, hackathon) = await PublishedAsync();
        var participant = await CompanySeed.ParticipantAsync(_dbContext, "maria@exemplo.com");

        var result = await _service.CreateModuleAsync(participant.Id, hackathon.Id, new SaveHackathonModuleDto { Titulo = "Semana 1" },
            CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.Forbidden, result.Error);
    }

    [Fact]
    public async Task CreateModule_OnOtherCompanyDraft_ReturnsNotFound()
    {
        var (_, hackathon) = await PublishedAsync();
        hackathon.Status = HackathonStatus.Draft;
        await _dbContext.SaveChangesAsync();
        var other = await CompanySeed.CompanyAsync(_dbContext, "outra@exemplo.com");

        var result = await _service.CreateModuleAsync(other.PrimaryAccountId, hackathon.Id, new SaveHackathonModuleDto { Titulo = "Semana 1" },
            CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.NotFound, result.Error);
    }

    [Fact]
    public async Task CreateModule_UnknownHackathon_ReturnsNotFound()
    {
        var (company, _) = await PublishedAsync();

        var result = await _service.CreateModuleAsync(company.PrimaryAccountId, Guid.NewGuid(), new SaveHackathonModuleDto { Titulo = "Semana 1" },
            CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.NotFound, result.Error);
    }

    [Theory]
    [InlineData(HackathonStatus.Draft)]
    [InlineData(HackathonStatus.Scheduled)]
    public async Task CreateModule_DraftOrActive_Succeeds(HackathonStatus status)
    {
        var (company, hackathon) = await PublishedAsync();
        hackathon.Status = status;
        // já começou: fase Ativo quando publicado
        hackathon.StartsAt = DateTime.UtcNow.AddHours(-1);
        await _dbContext.SaveChangesAsync();

        var result = await _service.CreateModuleAsync(company.PrimaryAccountId, hackathon.Id, new SaveHackathonModuleDto { Titulo = "Semana 1" },
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
    }

    [Theory]
    [InlineData(HackathonStatus.Closed)]
    [InlineData(HackathonStatus.Cancelled)]
    public async Task CreateModule_ClosedOrCancelled_ReturnsConflict(HackathonStatus status)
    {
        var (company, hackathon) = await PublishedAsync();
        hackathon.Status = status;
        await _dbContext.SaveChangesAsync();

        var result = await _service.CreateModuleAsync(company.PrimaryAccountId, hackathon.Id, new SaveHackathonModuleDto { Titulo = "Semana 1" },
            CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.Conflict, result.Error);
        Assert.Equal("Hackathon encerrado ou cancelado não aceita mudanças nos módulos.", result.Message);
    }

    [Fact]
    public async Task CreateModule_AfterReviewEnds_ReturnsConflict()
    {
        var (company, hackathon) = await PublishedAsync();
        hackathon.StartsAt = DateTime.UtcNow.AddDays(-10);
        hackathon.EndsAt = DateTime.UtcNow.AddDays(-5);
        hackathon.ReviewEndsAt = DateTime.UtcNow.AddDays(-1);
        await _dbContext.SaveChangesAsync();

        var result = await _service.CreateModuleAsync(company.PrimaryAccountId, hackathon.Id, new SaveHackathonModuleDto { Titulo = "Semana 1" },
            CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.Conflict, result.Error);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("long-title", null)]
    [InlineData("Semana 1", "long-description")]
    public async Task CreateModule_InvalidFields_ReturnsValidation(string? title, string? description)
    {
        var (company, hackathon) = await PublishedAsync();
        var dto = new SaveHackathonModuleDto
        {
            Titulo = title == "long-title" ? new string('a', 121) : title,
            Descricao = description == "long-description" ? new string('a', 1001) : description
        };

        var result = await _service.CreateModuleAsync(company.PrimaryAccountId, hackathon.Id, dto, CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.Validation, result.Error);
    }

    [Fact]
    public async Task CreateModule_AtLimit_ReturnsConflict()
    {
        var (company, hackathon) = await PublishedAsync();
        for (var index = 1; index <= 20; index++) await AddModuleAsync(company, hackathon, $"Módulo {index}");

        var result = await _service.CreateModuleAsync(company.PrimaryAccountId, hackathon.Id, new SaveHackathonModuleDto { Titulo = "Módulo 21" },
            CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.Conflict, result.Error);
    }

    [Fact]
    public async Task UpdateModule_ChangesTitleAndDescription()
    {
        var (company, hackathon) = await PublishedAsync();
        var module = await AddModuleAsync(company, hackathon, "Semana 1");

        var result = await _service.UpdateModuleAsync(company.PrimaryAccountId, hackathon.Id, module.ModuloId,
            new SaveHackathonModuleDto { Titulo = "Semana 1 — Fundamentos", Descricao = "Base do desafio" }, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Semana 1 — Fundamentos", result.Value!.Titulo);
        Assert.Equal("Base do desafio", result.Value.Descricao);
        Assert.Equal(1, result.Value.Posicao);
    }

    [Fact]
    public async Task UpdateModule_FromOtherHackathon_ReturnsNotFound()
    {
        var (company, hackathon) = await PublishedAsync();
        var otherHackathon = await ParticipationSeed.PublishedHackathonAsync(_dbContext, company, HackathonParticipationMode.Individual);
        var module = await AddModuleAsync(company, otherHackathon, "Semana 1");

        var result = await _service.UpdateModuleAsync(company.PrimaryAccountId, hackathon.Id, module.ModuloId,
            new SaveHackathonModuleDto { Titulo = "Semana 1" }, CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.NotFound, result.Error);
    }

    [Fact]
    public async Task DeleteModule_RemovesContentsAndClosesTheGap()
    {
        var (company, hackathon) = await PublishedAsync();
        var first = await AddModuleAsync(company, hackathon, "Semana 1");
        var second = await AddModuleAsync(company, hackathon, "Semana 2");
        var third = await AddModuleAsync(company, hackathon, "Semana 3");
        await AddContentAsync(company, hackathon, second.ModuloId, "Leitura");

        var result = await _service.DeleteModuleAsync(company.PrimaryAccountId, hackathon.Id, second.ModuloId, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var modules = (await _service.GetAsync(null, hackathon.Id.ToString(), CancellationToken.None)).Value!;
        Assert.Equal([first.ModuloId, third.ModuloId], modules.Select(item => item.ModuloId));
        Assert.Equal([1, 2], modules.Select(item => item.Posicao));
        Assert.Empty(await TestDb.CreateShared(_databaseName).HackathonModuleContents.ToListAsync());
    }

    [Fact]
    public async Task ReorderModules_AppliesTheNewOrder()
    {
        var (company, hackathon) = await PublishedAsync();
        var first = await AddModuleAsync(company, hackathon, "Semana 1");
        var second = await AddModuleAsync(company, hackathon, "Semana 2");
        var third = await AddModuleAsync(company, hackathon, "Semana 3");

        var result = await _service.ReorderModulesAsync(company.PrimaryAccountId, hackathon.Id,
            Order(third.ModuloId, first.ModuloId, second.ModuloId), CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal([third.ModuloId, first.ModuloId, second.ModuloId], result.Value!.Select(item => item.ModuloId));
        Assert.Equal([1, 2, 3], result.Value!.Select(item => item.Posicao));
        var saved = await TestDb.CreateShared(_databaseName).HackathonModules.SingleAsync(item => item.Id == third.ModuloId);
        Assert.Equal(1, saved.Position);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("repeated")]
    [InlineData("empty")]
    public async Task ReorderModules_InconsistentList_ReturnsValidation(string kind)
    {
        var (company, hackathon) = await PublishedAsync();
        var first = await AddModuleAsync(company, hackathon, "Semana 1");
        var second = await AddModuleAsync(company, hackathon, "Semana 2");
        var ids = kind switch
        {
            "missing" => [second.ModuloId],
            "extra" => [second.ModuloId, first.ModuloId, Guid.NewGuid()],
            "repeated" => [second.ModuloId, first.ModuloId, first.ModuloId],
            _ => Array.Empty<Guid>()
        };

        var result = await _service.ReorderModulesAsync(company.PrimaryAccountId, hackathon.Id, Order(ids), CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.Validation, result.Error);
    }

    [Fact]
    public async Task CreateContent_AppendsAtTheEnd()
    {
        var (company, hackathon) = await PublishedAsync();
        var module = await AddModuleAsync(company, hackathon, "Semana 1");
        await AddContentAsync(company, hackathon, module.ModuloId, "Leitura");

        var result = await _service.CreateContentAsync(company.PrimaryAccountId, hackathon.Id, module.ModuloId,
            new SaveHackathonModuleContentDto { Tipo = "Video", Titulo = "Aula de abertura", Url = " https://youtu.be/abc " }, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Video", result.Value!.Tipo);
        Assert.Equal("https://youtu.be/abc", result.Value.Url);
        Assert.Null(result.Value.Texto);
        Assert.Equal(2, result.Value.Posicao);
    }

    [Fact]
    public async Task CreateContent_Text_IgnoresUrl()
    {
        var (company, hackathon) = await PublishedAsync();
        var module = await AddModuleAsync(company, hackathon, "Semana 1");

        var result = await _service.CreateContentAsync(company.PrimaryAccountId, hackathon.Id, module.ModuloId,
            new SaveHackathonModuleContentDto { Tipo = "texto", Titulo = "Regras", Texto = "Leia tudo.", Url = "nada" }, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Texto", result.Value!.Tipo);
        Assert.Equal("Leia tudo.", result.Value.Texto);
        Assert.Null(result.Value.Url);
    }

    [Fact]
    public async Task CreateContent_LinkWithDescription_KeepsText()
    {
        var (company, hackathon) = await PublishedAsync();
        var module = await AddModuleAsync(company, hackathon, "Semana 1");

        var result = await _service.CreateContentAsync(company.PrimaryAccountId, hackathon.Id, module.ModuloId,
            new SaveHackathonModuleContentDto { Tipo = "Link", Titulo = "Documentação", Texto = "Guia oficial", Url = "http://docs.exemplo.com" },
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Link", result.Value!.Tipo);
        Assert.Equal("Guia oficial", result.Value.Texto);
    }

    [Theory]
    [InlineData("Pdf", "Título", "texto", null)]
    [InlineData(null, "Título", "texto", null)]
    [InlineData("Texto", " ", "texto", null)]
    [InlineData("Texto", "long-title", "texto", null)]
    [InlineData("Texto", "Título", null, null)]
    [InlineData("Texto", "Título", "long-body", null)]
    [InlineData("Link", "Título", null, null)]
    [InlineData("Link", "Título", null, "ftp://arquivos.exemplo.com")]
    [InlineData("Video", "Título", null, "javascript:alert(1)")]
    [InlineData("Video", "Título", null, "youtu.be/abc")]
    [InlineData("Link", "Título", null, "long-url")]
    [InlineData("Link", "Título", "long-description", "https://exemplo.com")]
    public async Task CreateContent_InvalidFields_ReturnsValidation(string? type, string? title, string? text, string? url)
    {
        var (company, hackathon) = await PublishedAsync();
        var module = await AddModuleAsync(company, hackathon, "Semana 1");
        var dto = new SaveHackathonModuleContentDto
        {
            Tipo = type,
            Titulo = title == "long-title" ? new string('a', 161) : title,
            Texto = text switch
            {
                "long-body" => new string('a', 20_001),
                "long-description" => new string('a', 1001),
                _ => text
            },
            Url = url == "long-url" ? "https://exemplo.com/" + new string('a', 2048) : url
        };

        var result = await _service.CreateContentAsync(company.PrimaryAccountId, hackathon.Id, module.ModuloId, dto, CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.Validation, result.Error);
    }

    [Fact]
    public async Task CreateContent_LongTextUpToLimit_Succeeds()
    {
        var (company, hackathon) = await PublishedAsync();
        var module = await AddModuleAsync(company, hackathon, "Semana 1");

        var result = await _service.CreateContentAsync(company.PrimaryAccountId, hackathon.Id, module.ModuloId,
            new SaveHackathonModuleContentDto { Tipo = "Texto", Titulo = "Apostila", Texto = new string('a', 20_000) }, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
    }

    [Fact]
    public async Task CreateContent_AtLimit_ReturnsConflict()
    {
        var (company, hackathon) = await PublishedAsync();
        var module = await AddModuleAsync(company, hackathon, "Semana 1");
        for (var index = 1; index <= 30; index++) await AddContentAsync(company, hackathon, module.ModuloId, $"Conteúdo {index}");

        var result = await _service.CreateContentAsync(company.PrimaryAccountId, hackathon.Id, module.ModuloId,
            new SaveHackathonModuleContentDto { Tipo = "Texto", Titulo = "Conteúdo 31", Texto = "Mais um." }, CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.Conflict, result.Error);
    }

    [Fact]
    public async Task CreateContent_ByOtherCompany_ReturnsForbidden()
    {
        var (company, hackathon) = await PublishedAsync();
        var module = await AddModuleAsync(company, hackathon, "Semana 1");
        var other = await CompanySeed.CompanyAsync(_dbContext, "outra@exemplo.com");

        var result = await _service.CreateContentAsync(other.PrimaryAccountId, hackathon.Id, module.ModuloId,
            new SaveHackathonModuleContentDto { Tipo = "Texto", Titulo = "Regras", Texto = "Leia tudo." }, CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.Forbidden, result.Error);
    }

    [Fact]
    public async Task CreateContent_Cancelled_ReturnsConflict()
    {
        var (company, hackathon) = await PublishedAsync();
        var module = await AddModuleAsync(company, hackathon, "Semana 1");
        hackathon.Status = HackathonStatus.Cancelled;
        await _dbContext.SaveChangesAsync();

        var result = await _service.CreateContentAsync(company.PrimaryAccountId, hackathon.Id, module.ModuloId,
            new SaveHackathonModuleContentDto { Tipo = "Texto", Titulo = "Regras", Texto = "Leia tudo." }, CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.Conflict, result.Error);
    }

    [Fact]
    public async Task CreateContent_UnknownModule_ReturnsNotFound()
    {
        var (company, hackathon) = await PublishedAsync();

        var result = await _service.CreateContentAsync(company.PrimaryAccountId, hackathon.Id, Guid.NewGuid(),
            new SaveHackathonModuleContentDto { Tipo = "Texto", Titulo = "Regras", Texto = "Leia tudo." }, CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.NotFound, result.Error);
    }

    [Fact]
    public async Task UpdateContent_LinkToText_ClearsUrl()
    {
        var (company, hackathon) = await PublishedAsync();
        var module = await AddModuleAsync(company, hackathon, "Semana 1");
        var created = await _service.CreateContentAsync(company.PrimaryAccountId, hackathon.Id, module.ModuloId,
            new SaveHackathonModuleContentDto { Tipo = "Link", Titulo = "Curso", Url = "https://cursos.exemplo.com" }, CancellationToken.None);

        var result = await _service.UpdateContentAsync(company.PrimaryAccountId, hackathon.Id, module.ModuloId, created.Value!.ConteudoId,
            new SaveHackathonModuleContentDto { Tipo = "Texto", Titulo = "Resumo do curso", Texto = "Tudo em um texto." }, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Texto", result.Value!.Tipo);
        Assert.Equal("Resumo do curso", result.Value.Titulo);
        Assert.Null(result.Value.Url);
        Assert.Equal(1, result.Value.Posicao);
    }

    [Fact]
    public async Task UpdateContent_FromOtherModule_ReturnsNotFound()
    {
        var (company, hackathon) = await PublishedAsync();
        var first = await AddModuleAsync(company, hackathon, "Semana 1");
        var second = await AddModuleAsync(company, hackathon, "Semana 2");
        var content = await AddContentAsync(company, hackathon, second.ModuloId, "Leitura");

        var result = await _service.UpdateContentAsync(company.PrimaryAccountId, hackathon.Id, first.ModuloId, content.ConteudoId,
            new SaveHackathonModuleContentDto { Tipo = "Texto", Titulo = "Leitura", Texto = "Outro texto." }, CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.NotFound, result.Error);
    }

    [Fact]
    public async Task DeleteContent_ClosesTheGap()
    {
        var (company, hackathon) = await PublishedAsync();
        var module = await AddModuleAsync(company, hackathon, "Semana 1");
        var first = await AddContentAsync(company, hackathon, module.ModuloId, "Primeiro");
        var second = await AddContentAsync(company, hackathon, module.ModuloId, "Segundo");
        var third = await AddContentAsync(company, hackathon, module.ModuloId, "Terceiro");

        var result = await _service.DeleteContentAsync(company.PrimaryAccountId, hackathon.Id, module.ModuloId, first.ConteudoId,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var contents = (await _service.GetAsync(null, hackathon.Id.ToString(), CancellationToken.None)).Value!.Single().Conteudos;
        Assert.Equal([second.ConteudoId, third.ConteudoId], contents.Select(item => item.ConteudoId));
        Assert.Equal([1, 2], contents.Select(item => item.Posicao));
    }

    [Fact]
    public async Task ReorderContents_ReturnsModuleInTheNewOrder()
    {
        var (company, hackathon) = await PublishedAsync();
        var module = await AddModuleAsync(company, hackathon, "Semana 1");
        var first = await AddContentAsync(company, hackathon, module.ModuloId, "Primeiro");
        var second = await AddContentAsync(company, hackathon, module.ModuloId, "Segundo");

        var result = await _service.ReorderContentsAsync(company.PrimaryAccountId, hackathon.Id, module.ModuloId,
            Order(second.ConteudoId, first.ConteudoId), CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(module.ModuloId, result.Value!.ModuloId);
        Assert.Equal([second.ConteudoId, first.ConteudoId], result.Value.Conteudos.Select(item => item.ConteudoId));
        Assert.Equal([1, 2], result.Value.Conteudos.Select(item => item.Posicao));
    }

    [Fact]
    public async Task ReorderContents_WithIdFromOtherModule_ReturnsValidation()
    {
        var (company, hackathon) = await PublishedAsync();
        var first = await AddModuleAsync(company, hackathon, "Semana 1");
        var second = await AddModuleAsync(company, hackathon, "Semana 2");
        var mine = await AddContentAsync(company, hackathon, first.ModuloId, "Meu");
        var other = await AddContentAsync(company, hackathon, second.ModuloId, "Outro");

        var result = await _service.ReorderContentsAsync(company.PrimaryAccountId, hackathon.Id, first.ModuloId,
            Order(other.ConteudoId, mine.ConteudoId), CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.Validation, result.Error);
    }

    [Fact]
    public async Task Get_PublishedAsAnonymous_ReturnsModulesInOrder()
    {
        var (company, hackathon) = await PublishedAsync();
        var first = await AddModuleAsync(company, hackathon, "Semana 1");
        var second = await AddModuleAsync(company, hackathon, "Semana 2");
        await AddContentAsync(company, hackathon, second.ModuloId, "Leitura");
        await _service.ReorderModulesAsync(company.PrimaryAccountId, hackathon.Id, Order(second.ModuloId, first.ModuloId), CancellationToken.None);

        var result = await _service.GetAsync(null, hackathon.Id.ToString(), CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(["Semana 2", "Semana 1"], result.Value!.Select(item => item.Titulo));
        Assert.Equal("Leitura", Assert.Single(result.Value![0].Conteudos).Titulo);
    }

    [Fact]
    public async Task Get_BySlug_ReturnsModules()
    {
        var (company, hackathon) = await PublishedAsync();
        await AddModuleAsync(company, hackathon, "Semana 1");

        var result = await _service.GetAsync(null, hackathon.Slug, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task Get_DraftForAnonymousOrOtherCompany_ReturnsNotFound()
    {
        var (company, hackathon) = await PublishedAsync();
        await AddModuleAsync(company, hackathon, "Semana 1");
        hackathon.Status = HackathonStatus.Draft;
        await _dbContext.SaveChangesAsync();
        var other = await CompanySeed.CompanyAsync(_dbContext, "outra@exemplo.com");

        var anonymous = await _service.GetAsync(null, hackathon.Id.ToString(), CancellationToken.None);
        var otherCompany = await _service.GetAsync(other.PrimaryAccountId, hackathon.Id.ToString(), CancellationToken.None);
        var owner = await _service.GetAsync(company.PrimaryAccountId, hackathon.Id.ToString(), CancellationToken.None);

        Assert.Equal(HackathonModuleServiceError.NotFound, anonymous.Error);
        Assert.Equal(HackathonModuleServiceError.NotFound, otherCompany.Error);
        Assert.True(owner.Succeeded, owner.Message);
        Assert.Single(owner.Value!);
    }

    [Fact]
    public async Task Get_Closed_StillReturnsModules()
    {
        var (company, hackathon) = await PublishedAsync();
        await AddModuleAsync(company, hackathon, "Semana 1");
        hackathon.Status = HackathonStatus.Closed;
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetAsync(null, hackathon.Id.ToString(), CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Single(result.Value!);
    }
}
