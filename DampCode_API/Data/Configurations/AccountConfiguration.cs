using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DampCode_API.Data.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", table =>
        {
            table.HasCheckConstraint("ck_accounts_account_type", "account_type IN ('Participant', 'Company', 'Admin')");
            table.HasCheckConstraint("ck_accounts_status", "status IN ('Active', 'Suspended', 'Disabled')");
        });
        builder.HasKey(account => account.Id).HasName("pk_accounts");
        builder.Property(account => account.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(account => account.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(account => account.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(254).IsRequired();
        builder.Property(account => account.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        builder.Property(account => account.AccountType).HasColumnName("account_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(account => account.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(account => account.EmailVerifiedAt).HasColumnName("email_verified_at");
        builder.Property(account => account.CredentialsChangedAt).HasColumnName("credentials_changed_at");
        builder.Property(account => account.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(account => account.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(account => account.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(account => account.DeletedAt).HasColumnName("deleted_at");
        builder.HasIndex(account => account.NormalizedEmail).IsUnique().HasDatabaseName("ux_accounts_normalized_email");
    }
}
