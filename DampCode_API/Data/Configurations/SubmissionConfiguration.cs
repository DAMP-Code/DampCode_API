using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DampCode_API.Data.Configurations;

public sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("submissions", table =>
            table.HasCheckConstraint("ck_submissions_owner", "(participation_id IS NULL) <> (team_id IS NULL)"));
        builder.HasKey(item => item.Id).HasName("pk_submissions");
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.HackathonId).HasColumnName("hackathon_id").IsRequired();
        builder.Property(item => item.ParticipationId).HasColumnName("participation_id");
        builder.Property(item => item.TeamId).HasColumnName("team_id");
        builder.Property(item => item.StageId).HasColumnName("stage_id");
        builder.Property(item => item.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(item => item.CurrentVersion).HasColumnName("current_version").IsRequired().IsConcurrencyToken();
        builder.Property(item => item.CurrentMongoVersionId).HasColumnName("current_mongo_version_id").HasMaxLength(24);
        builder.Property(item => item.FirstSubmittedAt).HasColumnName("first_submitted_at");
        builder.Property(item => item.LastSubmittedAt).HasColumnName("last_submitted_at");
        builder.Property(item => item.FinalizedAt).HasColumnName("finalized_at");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(item => item.ParticipationId).IsUnique().HasFilter("participation_id IS NOT NULL")
            .HasDatabaseName("ux_submissions_participation");
        builder.HasIndex(item => item.TeamId).IsUnique().HasFilter("team_id IS NOT NULL").HasDatabaseName("ux_submissions_team");
        builder.HasIndex(item => item.HackathonId).HasDatabaseName("ix_submissions_hackathon_id");
        builder.HasOne(item => item.Hackathon).WithMany(hackathon => hackathon.Submissions)
            .HasForeignKey(item => item.HackathonId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_submissions_hackathons");
        builder.HasOne(item => item.Team).WithMany()
            .HasForeignKey(item => item.TeamId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_submissions_hackathon_teams");
        builder.HasOne(item => item.Participation).WithMany()
            .HasForeignKey(item => item.ParticipationId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_submissions_participations");
    }
}

public sealed class SubmissionVersionConfiguration : IEntityTypeConfiguration<SubmissionVersion>
{
    public void Configure(EntityTypeBuilder<SubmissionVersion> builder)
    {
        builder.ToTable("submission_versions");
        builder.HasKey(item => item.Id).HasName("pk_submission_versions");
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.SubmissionId).HasColumnName("submission_id").IsRequired();
        builder.Property(item => item.Version).HasColumnName("version").IsRequired();
        builder.Property(item => item.Language).HasColumnName("language").HasMaxLength(32).IsRequired();
        builder.Property(item => item.FileName).HasColumnName("file_name").HasMaxLength(255);
        builder.Property(item => item.MongoDocumentId).HasColumnName("mongo_document_id").HasMaxLength(24).IsRequired();
        builder.Property(item => item.SubmittedByAccountId).HasColumnName("submitted_by_account_id").IsRequired();
        builder.Property(item => item.SubmittedAt).HasColumnName("submitted_at").IsRequired();
        builder.HasIndex(item => new { item.SubmissionId, item.Version }).IsUnique().HasDatabaseName("ux_submission_versions_version");
        builder.HasIndex(item => item.SubmittedByAccountId).HasDatabaseName("ix_submission_versions_submitted_by_account_id");
        builder.HasOne(item => item.Submission).WithMany(submission => submission.Versions)
            .HasForeignKey(item => item.SubmissionId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_submission_versions_submissions");
        builder.HasOne(item => item.SubmittedByAccount).WithMany()
            .HasForeignKey(item => item.SubmittedByAccountId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_submission_versions_accounts");
    }
}
