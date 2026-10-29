using DampCode_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DampCode_API.Data.Configurations;

public sealed class HackathonTeamConfiguration : IEntityTypeConfiguration<HackathonTeam>
{
    public void Configure(EntityTypeBuilder<HackathonTeam> builder)
    {
        builder.ToTable("hackathon_teams", table =>
            table.HasCheckConstraint("ck_hackathon_teams_member_count", "member_count BETWEEN 0 AND 5"));
        builder.HasKey(item => item.Id).HasName("pk_hackathon_teams");
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.HackathonId).HasColumnName("hackathon_id").IsRequired();
        builder.Property(item => item.CreatedByAccountId).HasColumnName("created_by_account_id").IsRequired();
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(100).IsRequired();
        builder.Property(item => item.IsOpen).HasColumnName("is_open").IsRequired();
        builder.Property(item => item.MemberCount).HasColumnName("member_count").IsRequired().IsConcurrencyToken();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(item => new { item.HackathonId, item.NormalizedName }).IsUnique().HasDatabaseName("ux_hackathon_teams_hackathon_name");
        builder.HasIndex(item => item.CreatedByAccountId).HasDatabaseName("ix_hackathon_teams_created_by_account_id");
        builder.HasOne(item => item.Hackathon).WithMany(hackathon => hackathon.Teams)
            .HasForeignKey(item => item.HackathonId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_hackathon_teams_hackathons");
        builder.HasOne<Account>().WithMany()
            .HasForeignKey(item => item.CreatedByAccountId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_hackathon_teams_accounts");
    }
}

public sealed class ParticipationConfiguration : IEntityTypeConfiguration<Participation>
{
    public void Configure(EntityTypeBuilder<Participation> builder)
    {
        builder.ToTable("participations");
        builder.HasKey(item => item.Id).HasName("pk_participations");
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.HackathonId).HasColumnName("hackathon_id").IsRequired();
        builder.Property(item => item.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(item => item.TeamId).HasColumnName("team_id");
        builder.Property(item => item.JoinedAt).HasColumnName("joined_at").IsRequired();
        builder.Property(item => item.LeftAt).HasColumnName("left_at");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(item => new { item.HackathonId, item.AccountId }).IsUnique().HasFilter("left_at IS NULL")
            .HasDatabaseName("ux_participations_active_account");
        builder.HasIndex(item => item.TeamId).HasDatabaseName("ix_participations_team_id");
        builder.HasIndex(item => item.AccountId).HasDatabaseName("ix_participations_account_id");
        builder.HasOne(item => item.Hackathon).WithMany(hackathon => hackathon.Participations)
            .HasForeignKey(item => item.HackathonId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_participations_hackathons");
        builder.HasOne(item => item.Account).WithMany()
            .HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_participations_accounts");
        builder.HasOne(item => item.Team).WithMany(team => team.Members)
            .HasForeignKey(item => item.TeamId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_participations_hackathon_teams");
    }
}

public sealed class TeamInvitationConfiguration : IEntityTypeConfiguration<TeamInvitation>
{
    public void Configure(EntityTypeBuilder<TeamInvitation> builder)
    {
        builder.ToTable("team_invitations", table =>
            table.HasCheckConstraint("ck_team_invitations_status", "status IN ('Pending', 'Accepted', 'Declined', 'Revoked', 'Cancelled')"));
        builder.HasKey(item => item.Id).HasName("pk_team_invitations");
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.TeamId).HasColumnName("team_id").IsRequired();
        builder.Property(item => item.InvitedByAccountId).HasColumnName("invited_by_account_id").IsRequired();
        builder.Property(item => item.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(item => item.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(254).IsRequired();
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(item => item.RespondedAt).HasColumnName("responded_at");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(item => item.NormalizedEmail).HasDatabaseName("ix_team_invitations_normalized_email");
        builder.HasIndex(item => item.TeamId).HasDatabaseName("ix_team_invitations_team_id");
        builder.HasIndex(item => item.InvitedByAccountId).HasDatabaseName("ix_team_invitations_invited_by_account_id");
        builder.HasOne(item => item.Team).WithMany(team => team.Invitations)
            .HasForeignKey(item => item.TeamId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_team_invitations_hackathon_teams");
        builder.HasOne(item => item.InvitedByAccount).WithMany()
            .HasForeignKey(item => item.InvitedByAccountId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_team_invitations_accounts");
    }
}

public sealed class TeamJoinRequestConfiguration : IEntityTypeConfiguration<TeamJoinRequest>
{
    public void Configure(EntityTypeBuilder<TeamJoinRequest> builder)
    {
        builder.ToTable("team_join_requests", table =>
            table.HasCheckConstraint("ck_team_join_requests_status", "status IN ('Pending', 'Accepted', 'Declined', 'Cancelled')"));
        builder.HasKey(item => item.Id).HasName("pk_team_join_requests");
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.TeamId).HasColumnName("team_id").IsRequired();
        builder.Property(item => item.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.RespondedByAccountId).HasColumnName("responded_by_account_id");
        builder.Property(item => item.RespondedAt).HasColumnName("responded_at");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(item => item.TeamId).HasDatabaseName("ix_team_join_requests_team_id");
        builder.HasIndex(item => item.AccountId).HasDatabaseName("ix_team_join_requests_account_id");
        builder.HasOne(item => item.Team).WithMany(team => team.JoinRequests)
            .HasForeignKey(item => item.TeamId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_team_join_requests_hackathon_teams");
        builder.HasOne(item => item.Account).WithMany()
            .HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_team_join_requests_accounts");
    }
}
