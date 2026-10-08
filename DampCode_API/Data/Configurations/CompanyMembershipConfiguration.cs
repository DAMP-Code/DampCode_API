using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DampCode_API.Data.Configurations;

public sealed class CompanyMembershipConfiguration : IEntityTypeConfiguration<CompanyMembership>
{
    public void Configure(EntityTypeBuilder<CompanyMembership> builder)
    {
        builder.ToTable("company_memberships");
        builder.HasKey(item => item.Id).HasName("pk_company_memberships");
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(item => item.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(item => item.JobTitle).HasColumnName("job_title").HasMaxLength(150);
        builder.Property(item => item.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(item => item.EndedAt).HasColumnName("ended_at");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(item => new { item.CompanyId, item.AccountId, item.StartedAt }).IsUnique().HasDatabaseName("ux_company_memberships_history");
        builder.HasIndex(item => item.AccountId).IsUnique().HasFilter("ended_at IS NULL").HasDatabaseName("ux_company_memberships_active_account");
        builder.HasOne(item => item.Company).WithMany(company => company.Memberships)
            .HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_company_memberships_companies");
        builder.HasOne(item => item.Account).WithMany(account => account.CompanyMemberships)
            .HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_company_memberships_accounts");
    }
}
