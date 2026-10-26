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

    public static DampCodeDbContext Create()
    {
        var builder = new DbContextOptionsBuilder<DampCodeDbContext>();
        Configure(builder, Guid.NewGuid().ToString());
        return new DampCodeDbContext(builder.Options);
    }
}
