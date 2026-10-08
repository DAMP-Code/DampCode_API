using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DampCode_API.Data.Configurations;

public sealed class TechnologyConfiguration : IEntityTypeConfiguration<Technology>
{
    public void Configure(EntityTypeBuilder<Technology> builder)
    {
        builder.ToTable("technologies");
        builder.HasKey(item => item.Id).HasName("pk_technologies");
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(100).IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(item => item.NormalizedName).IsUnique().HasDatabaseName("ux_technologies_normalized_name");
    }
}

public sealed class UserTechnologyConfiguration : IEntityTypeConfiguration<UserTechnology>
{
    public void Configure(EntityTypeBuilder<UserTechnology> builder)
    {
        builder.ToTable("user_technologies");
        builder.HasKey(item => new { item.UserProfileId, item.TechnologyId }).HasName("pk_user_technologies");
        builder.Property(item => item.UserProfileId).HasColumnName("user_profile_id");
        builder.Property(item => item.TechnologyId).HasColumnName("technology_id");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasOne(item => item.UserProfile).WithMany(profile => profile.Technologies)
            .HasForeignKey(item => item.UserProfileId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_user_technologies_user_profiles");
        builder.HasOne(item => item.Technology).WithMany(technology => technology.Users)
            .HasForeignKey(item => item.TechnologyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_user_technologies_technologies");
    }
}

public sealed class CompanyTechnologyConfiguration : IEntityTypeConfiguration<CompanyTechnology>
{
    public void Configure(EntityTypeBuilder<CompanyTechnology> builder)
    {
        builder.ToTable("company_technologies");
        builder.HasKey(item => new { item.CompanyId, item.TechnologyId }).HasName("pk_company_technologies");
        builder.Property(item => item.CompanyId).HasColumnName("company_id");
        builder.Property(item => item.TechnologyId).HasColumnName("technology_id");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasOne(item => item.Company).WithMany(company => company.Technologies)
            .HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_company_technologies_companies");
        builder.HasOne(item => item.Technology).WithMany(technology => technology.Companies)
            .HasForeignKey(item => item.TechnologyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_company_technologies_technologies");
    }
}
