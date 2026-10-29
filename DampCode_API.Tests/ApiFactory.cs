using DampCode_API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DampCode_API.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly string _jwtSecret;

    // xUnit exige um único construtor público em class fixtures.
    public ApiFactory() : this(TestJwt.Secret) { }

    internal ApiFactory(string jwtSecret) => _jwtSecret = jwtSecret;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" evita carregar os User Secrets reais do desenvolvedor.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PostgreSql", "Host=nao-usado-nos-testes");
        builder.UseSetting("Jwt:Secret", _jwtSecret);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<DampCodeDbContext>>();
            services.AddDbContext<DampCodeDbContext>(options => TestDb.Configure(options, _databaseName));
            // O código das versões fica em memória: os testes não dependem de um MongoDB rodando.
            services.RemoveAll<ISubmissionVersionStore>();
            services.AddSingleton<ISubmissionVersionStore, InMemorySubmissionVersionStore>();
        });
    }
}
