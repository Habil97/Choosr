using Choosr.Domain.Entities;
using Choosr.Domain.ValueObjects;
using Choosr.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Choosr.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizChoice> QuizChoices => Set<QuizChoice>();
    public DbSet<Choosr.Domain.Entities.QuizChoiceStat> QuizChoiceStats => Set<Choosr.Domain.Entities.QuizChoiceStat>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<QuizTag> QuizTags => Set<QuizTag>();
    public DbSet<QuizReaction> QuizReactions => Set<QuizReaction>();
    public DbSet<QuizComment> QuizComments => Set<QuizComment>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<PlaySession> PlaySessions => Set<PlaySession>();
    public DbSet<Choosr.Domain.Entities.Draft> Drafts => Set<Choosr.Domain.Entities.Draft>();
    public DbSet<Choosr.Domain.Entities.DraftChoice> DraftChoices => Set<Choosr.Domain.Entities.DraftChoice>();
    public DbSet<Choosr.Domain.Entities.DraftRevision> DraftRevisions => Set<Choosr.Domain.Entities.DraftRevision>();
    public DbSet<TagSelectionStat> TagSelectionStats => Set<TagSelectionStat>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Quiz>(e=>{
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Title)
                .HasConversion(
                    v => v.Value,
                    v => QuizTitle.Create(v)
                )
                .HasMaxLength(200)
                .IsRequired();
            e.Property(x=>x.Category).HasMaxLength(100).IsRequired();
            e.Property(x=>x.Plays).HasDefaultValue(0);
            // Çoklu dil alanları (opsiyonel)
            e.Property(x=>x.TitleTr).HasMaxLength(200);
            e.Property(x=>x.TitleEn).HasMaxLength(200);
            e.Property(x=>x.DescriptionTr).HasColumnType("nvarchar(max)");
            e.Property(x=>x.DescriptionEn).HasColumnType("nvarchar(max)");
            e.Property(x=>x.Moderation)
                .HasConversion<int>();
            e.Property(x=>x.ModerationNotes).HasMaxLength(1000);
            e.HasIndex(x=>x.Moderation);
            e.HasMany(x=>x.Choices).WithOne(x=>x.Quiz).HasForeignKey(x=>x.QuizId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<QuizChoice>(e=>{
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Caption).HasMaxLength(200);
        });
        b.Entity<Choosr.Domain.Entities.QuizChoiceStat>(e=>{
            e.HasKey(x=>x.Id);
            e.HasIndex(x=> new { x.QuizId, x.ChoiceId }).IsUnique();
            e.Property(x=>x.Picks).HasDefaultValue(0);
            e.Property(x=>x.Matches).HasDefaultValue(0);
            e.Property(x=>x.Wins).HasDefaultValue(0);
            e.Property(x=>x.Champions).HasDefaultValue(0);
            e.HasOne(x=>x.Quiz).WithMany().HasForeignKey(x=>x.QuizId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x=>x.Choice).WithMany().HasForeignKey(x=>x.ChoiceId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Tag>(e=>{
            e.Property(x=>x.Name)
                .HasConversion(
                    v => v.Value,
                    v => TagName.Create(v)
                )
                .HasMaxLength(100)
                .IsRequired();
            e.HasIndex(x=>x.Name).IsUnique();
        });
        b.Entity<QuizTag>(e=>{
            e.HasKey(x=> new { x.QuizId, x.TagId });
            e.HasOne(x=>x.Quiz).WithMany(x=>x.QuizTags).HasForeignKey(x=>x.QuizId);
            e.HasOne(x=>x.Tag).WithMany(x=>x.QuizTags).HasForeignKey(x=>x.TagId);
        });
        b.Entity<QuizReaction>(e=>{
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Type).HasMaxLength(20).IsRequired();
            e.Property(x=>x.UserId).HasMaxLength(64).IsRequired();
            e.HasIndex(x=> new { x.QuizId, x.UserId }).IsUnique(); // her kullanıcı başına tek reaksiyon
            e.HasOne(x=>x.Quiz).WithMany().HasForeignKey(x=>x.QuizId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<QuizComment>(e=>{
            e.HasKey(x=>x.Id);
            e.Property(x=>x.UserId).HasMaxLength(64).IsRequired();
            e.Property(x=>x.UserName).HasMaxLength(64).IsRequired();
            e.Property(x=>x.Text).HasMaxLength(5000).IsRequired();
            e.Property(x=>x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x=> new { x.QuizId, x.CreatedAt });
            e.HasOne(x=>x.Quiz).WithMany().HasForeignKey(x=>x.QuizId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ContentReport>(e=>{
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Reason).HasMaxLength(200).IsRequired();
            e.Property(x=>x.ReporterUserId).HasMaxLength(64);
            e.Property(x=>x.ReporterUserName).HasMaxLength(64);
            e.Property(x=>x.ReporterIp).HasMaxLength(64);
            e.Property(x=>x.Details).HasMaxLength(4000);
            e.Property(x=>x.ModeratorNotes).HasMaxLength(1000);
            e.Property(x=>x.InReviewBy).HasMaxLength(64);
            e.HasIndex(x=> new { x.TargetType, x.TargetId, x.Status });
            e.HasIndex(x=> x.CreatedAt);
        });
        b.Entity<PlaySession>(e=>{
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Mode).HasMaxLength(32).HasDefaultValue("unknown");
            e.Property(x=>x.UserName).HasMaxLength(64);
            e.Property(x=>x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x=>x.CreatedAt);
            e.HasOne(x=>x.Quiz).WithMany().HasForeignKey(x=>x.QuizId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Choosr.Domain.Entities.Draft>(e=>{
            e.HasKey(x=>x.Id);
            e.Property(x=>x.UserName).HasMaxLength(64).IsRequired();
            e.Property(x=>x.Title).HasMaxLength(200).IsRequired();
            e.Property(x=>x.Category).HasMaxLength(100).IsRequired();
            e.Property(x=>x.Visibility).HasMaxLength(20).HasDefaultValue("public");
            e.Property(x=>x.Tags)
                .HasConversion(
                    v => string.Join('\u001F', v ?? Array.Empty<string>()),
                    v => string.IsNullOrEmpty(v) ? Array.Empty<string>() : v.Split('\u001F', StringSplitOptions.RemoveEmptyEntries)
                )
                .Metadata.SetValueComparer(new ValueComparer<string[]>(
                    (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
                    v => v == null ? 0 : v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s != null ? s.GetHashCode() : 0)),
                    v => v == null ? Array.Empty<string>() : v.ToArray()
                ));
            e.HasMany(x=>x.Choices).WithOne(x=>x.Draft).HasForeignKey(x=>x.DraftId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x=> new { x.UserName, x.UpdatedAt });
        });
        b.Entity<Choosr.Domain.Entities.DraftChoice>(e=>{
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Caption).HasMaxLength(200);
            e.HasIndex(x=> new { x.DraftId, x.Order });
        });
        b.Entity<Choosr.Domain.Entities.DraftRevision>(e=>{
            e.HasKey(x=>x.Id);
            e.Property(x=>x.UserName).HasMaxLength(64).IsRequired();
            e.Property(x=>x.Title).HasMaxLength(200).IsRequired();
            e.Property(x=>x.Category).HasMaxLength(100).IsRequired();
            e.Property(x=>x.Visibility).HasMaxLength(20).HasDefaultValue("public");
            e.Property(x=>x.Tags)
                .HasConversion(
                    v => string.Join('\u001F', v ?? Array.Empty<string>()),
                    v => string.IsNullOrEmpty(v) ? Array.Empty<string>() : v.Split('\u001F', StringSplitOptions.RemoveEmptyEntries)
                )
                .Metadata.SetValueComparer(new ValueComparer<string[]>(
                    (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
                    v => v == null ? 0 : v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s != null ? s.GetHashCode() : 0)),
                    v => v == null ? Array.Empty<string>() : v.ToArray()
                ));
            e.Property(x=>x.ChoicesJson).HasColumnType("nvarchar(max)").IsRequired();
            e.HasIndex(x=> new { x.DraftId, x.CreatedAt });
        });
        b.Entity<Notification>(e=>{
            e.HasKey(x=>x.Id);
            e.Property(x=>x.UserName).HasMaxLength(64).IsRequired();
            e.Property(x=>x.Title).HasMaxLength(200).IsRequired();
            e.Property(x=>x.LinkUrl).HasMaxLength(500);
            e.HasIndex(x=> new { x.UserName, x.CreatedAt });
        });
        b.Entity<TagSelectionStat>(e=>{
            e.HasKey(x=>x.Tag);
            e.Property(x=>x.Tag).HasMaxLength(100).IsRequired();
            e.Property(x=>x.Count).HasDefaultValue(0);
            e.Property(x=>x.LastSelectedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x=>x.LastSelectedAt);
        });
    }
}
