using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DampCode_API.Data.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies", table =>
        {
            table.HasCheckConstraint("ck_companies_cnpj", "cnpj ~ '^[0-9]{14}$'");
            table.HasCheckConstraint("ck_companies_verification_status", "verification_status IN ('Pending', 'Verified', 'Rejected')");
            table.HasCheckConstraint("ck_companies_reputation_score", "reputation_score >= 0 AND reputation_score <= 5");
        });
        builder.HasKey(company => company.Id).HasName("pk_companies");
        builder.Property(company => company.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(company => company.PrimaryAccountId).HasColumnName("primary_account_id").IsRequired();
        builder.Property(company => company.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(company => company.Cnpj).HasColumnName("cnpj").HasMaxLength(14).IsRequired();
        builder.Property(company => company.Area).HasColumnName("area").HasMaxLength(100);
        builder.Property(company => company.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(company => company.VerificationStatus).HasColumnName("verification_status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(company => company.ReputationScore).HasColumnName("reputation_score").HasPrecision(2, 1).IsRequired();
        builder.Property(company => company.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(company => company.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(company => company.DeletedAt).HasColumnName("deleted_at");
        builder.HasIndex(company => company.Cnpj).IsUnique().HasDatabaseName("ux_companies_cnpj");
        builder.HasIndex(company => company.PrimaryAccountId).IsUnique().HasDatabaseName("ux_companies_primary_account_id");
        builder.HasOne(company => company.PrimaryAccount).WithOne(account => account.PrimaryCompany)
            .HasForeignKey<Company>(company => company.PrimaryAccountId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_companies_primary_accounts");
    }
}
