using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DampCode_API.Data.Configurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles", table =>
        {
            table.HasCheckConstraint("ck_user_profiles_level", "level >= 1");
            table.HasCheckConstraint("ck_user_profiles_xp", "xp >= 0");
            table.HasCheckConstraint("ck_user_profiles_visibility", "profile_visibility IN ('Public', 'Private')");
        });
        builder.HasKey(profile => profile.Id).HasName("pk_user_profiles");
        builder.Property(profile => profile.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(profile => profile.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(profile => profile.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(profile => profile.Biography).HasColumnName("biography").HasMaxLength(1000);
        builder.Property(profile => profile.Level).HasColumnName("level").IsRequired();
        builder.Property(profile => profile.Xp).HasColumnName("xp").HasPrecision(12, 2).IsRequired();
        builder.Property(profile => profile.AvatarStorageKey).HasColumnName("avatar_storage_key").HasMaxLength(500);
        builder.Property(profile => profile.ProfileVisibility).HasColumnName("profile_visibility").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(profile => profile.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(profile => profile.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(profile => profile.AccountId).IsUnique().HasDatabaseName("ux_user_profiles_account_id");
        builder.HasOne(profile => profile.Account).WithOne(account => account.UserProfile)
            .HasForeignKey<UserProfile>(profile => profile.AccountId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_profiles_accounts");
    }
}
