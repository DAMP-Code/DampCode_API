using DampCode_API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DampCode_API.Tests;

internal static class TestDb
{
    // InMemory ignora transações, check constraints e índices únicos; essas regras são do PostgreSQL.
    public static void Configure(DbContextOptionsBuilder builder, string databaseName) =>
        builder.UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));

    public static DampCodeDbContext Create() => CreateShared(Guid.NewGuid().ToString());

    // Abre um contexto em um banco InMemory já existente (para simular outra requisição ao mesmo tempo).
    public static DampCodeDbContext CreateShared(string databaseName)
    {
        var builder = new DbContextOptionsBuilder<DampCodeDbContext>();
        Configure(builder, databaseName);
        return new DampCodeDbContext(builder.Options);
    }
}
