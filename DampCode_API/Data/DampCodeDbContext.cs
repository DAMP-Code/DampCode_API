using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Data;

public sealed class DampCodeDbContext(DbContextOptions<DampCodeDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMembership> CompanyMemberships => Set<CompanyMembership>();
    public DbSet<Technology> Technologies => Set<Technology>();
    public DbSet<UserTechnology> UserTechnologies => Set<UserTechnology>();
    public DbSet<CompanyTechnology> CompanyTechnologies => Set<CompanyTechnology>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DampCodeDbContext).Assembly);

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State == EntityState.Modified))
        {
            if (entry.Metadata.FindProperty("UpdatedAt") is not null)
            {
                entry.Property("UpdatedAt").CurrentValue = now;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
