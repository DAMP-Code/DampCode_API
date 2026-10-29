using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DampCode_API.Data.Configurations;

public sealed class HackathonConfiguration : IEntityTypeConfiguration<Hackathon>
{
    public void Configure(EntityTypeBuilder<Hackathon> builder)
    {
        builder.ToTable("hackathons", table =>
        {
            table.HasCheckConstraint("ck_hackathons_status", "status IN ('Draft', 'Scheduled', 'Active', 'InReview', 'Closed', 'Cancelled')");
            table.HasCheckConstraint("ck_hackathons_participation_mode", "participation_mode IN ('Individual', 'Team')");
            table.HasCheckConstraint("ck_hackathons_reward_type", "reward_type IS NULL OR reward_type IN ('Money', 'Interview')");
            table.HasCheckConstraint("ck_hackathons_winners_count", "winners_count IS NULL OR winners_count IN (1, 10, 15, 20, 50, 100)");
            table.HasCheckConstraint("ck_hackathons_dates",
                "starts_at IS NULL OR ends_at IS NULL OR review_ends_at IS NULL OR (starts_at < ends_at AND ends_at < review_ends_at)");
        });
        builder.HasKey(item => item.Id).HasName("pk_hackathons");
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(item => item.CreatedByAccountId).HasColumnName("created_by_account_id").IsRequired();
        builder.Property(item => item.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(item => item.Slug).HasColumnName("slug").HasMaxLength(220).IsRequired();
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(4000).IsRequired();
        builder.Property(item => item.Area).HasColumnName("area").HasMaxLength(100).IsRequired();
        builder.Property(item => item.ParticipationMode).HasColumnName("participation_mode").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.EvaluationMethod).HasColumnName("evaluation_method").HasMaxLength(100);
        builder.Property(item => item.PrizeAmount).HasColumnName("prize_amount").HasPrecision(12, 2);
        builder.Property(item => item.RewardType).HasColumnName("reward_type").HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.WinnersCount).HasColumnName("winners_count");
        builder.Property(item => item.StartsAt).HasColumnName("starts_at");
        builder.Property(item => item.EndsAt).HasColumnName("ends_at");
        builder.Property(item => item.ReviewEndsAt).HasColumnName("review_ends_at");
        builder.Property(item => item.ScheduledAt).HasColumnName("scheduled_at");
        builder.Property(item => item.ClosedAt).HasColumnName("closed_at");
        builder.Property(item => item.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(item => item.PrimaryColor).HasColumnName("primary_color").HasMaxLength(7);
        builder.Property(item => item.SecondaryColor).HasColumnName("secondary_color").HasMaxLength(7);
        builder.Property(item => item.BackgroundColor).HasColumnName("background_color").HasMaxLength(7);
        builder.Property(item => item.Logo).HasColumnName("logo").HasColumnType("text");
        builder.Property(item => item.LogoStorageKey).HasColumnName("logo_storage_key").HasMaxLength(500);
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        builder.HasIndex(item => item.Slug).IsUnique().HasDatabaseName("ux_hackathons_slug");
        builder.HasIndex(item => item.CompanyId).HasDatabaseName("ix_hackathons_company_id");
        builder.HasIndex(item => item.Status).HasDatabaseName("ix_hackathons_status");
        builder.HasIndex(item => item.CreatedByAccountId).HasDatabaseName("ix_hackathons_created_by_account_id");
        builder.HasOne(item => item.Company).WithMany(company => company.Hackathons)
            .HasForeignKey(item => item.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_hackathons_companies");
        builder.HasOne<Account>().WithMany()
            .HasForeignKey(item => item.CreatedByAccountId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_hackathons_accounts");
    }
}

public sealed class HackathonTechnologyConfiguration : IEntityTypeConfiguration<HackathonTechnology>
{
    public void Configure(EntityTypeBuilder<HackathonTechnology> builder)
    {
        builder.ToTable("hackathon_technologies");
        builder.HasKey(item => new { item.HackathonId, item.TechnologyId }).HasName("pk_hackathon_technologies");
        builder.Property(item => item.HackathonId).HasColumnName("hackathon_id");
        builder.Property(item => item.TechnologyId).HasColumnName("technology_id");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(item => item.TechnologyId).HasDatabaseName("ix_hackathon_technologies_technology_id");
        builder.HasOne(item => item.Hackathon).WithMany(hackathon => hackathon.Technologies)
            .HasForeignKey(item => item.HackathonId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_hackathon_technologies_hackathons");
        builder.HasOne(item => item.Technology).WithMany(technology => technology.Hackathons)
            .HasForeignKey(item => item.TechnologyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_hackathon_technologies_technologies");
    }
}
