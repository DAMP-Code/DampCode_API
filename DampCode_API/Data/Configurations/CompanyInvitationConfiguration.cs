using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DampCode_API.Data.Configurations;

public sealed class CompanyInvitationConfiguration : IEntityTypeConfiguration<CompanyInvitation>
{
    public void Configure(EntityTypeBuilder<CompanyInvitation> builder)
    {
        builder.ToTable("company_invitations", table =>
            table.HasCheckConstraint("ck_company_invitations_status", "status IN ('Pending', 'Accepted', 'Declined', 'Revoked')"));
        builder.HasKey(item => item.Id).HasName("pk_company_invitations");
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(item => item.InvitedByAccountId).HasColumnName("invited_by_account_id").IsRequired();
        builder.Property(item => item.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(item => item.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(254).IsRequired();
        builder.Property(item => item.JobTitle).HasColumnName("job_title").HasMaxLength(150);
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(item => item.RespondedAt).HasColumnName("responded_at");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(item => item.NormalizedEmail).HasDatabaseName("ix_company_invitations_normalized_email");
        builder.HasIndex(item => item.CompanyId).HasDatabaseName("ix_company_invitations_company_id");
        builder.HasIndex(item => item.InvitedByAccountId).HasDatabaseName("ix_company_invitations_invited_by_account_id");
        builder.HasOne(item => item.Company).WithMany(company => company.Invitations)
            .HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_company_invitations_companies");
        builder.HasOne(item => item.InvitedByAccount).WithMany()
            .HasForeignKey(item => item.InvitedByAccountId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_company_invitations_accounts");
    }
}
