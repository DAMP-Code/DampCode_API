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
    public DbSet<CompanyInvitation> CompanyInvitations => Set<CompanyInvitation>();
    public DbSet<Hackathon> Hackathons => Set<Hackathon>();
    public DbSet<HackathonTechnology> HackathonTechnologies => Set<HackathonTechnology>();
    public DbSet<HackathonTeam> HackathonTeams => Set<HackathonTeam>();
    public DbSet<Participation> Participations => Set<Participation>();
    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();
    public DbSet<TeamJoinRequest> TeamJoinRequests => Set<TeamJoinRequest>();

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
