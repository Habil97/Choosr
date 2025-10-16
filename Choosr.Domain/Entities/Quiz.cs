using Choosr.Domain.ValueObjects;

namespace Choosr.Domain.Entities;

public class Quiz
{
    public Guid Id { get; set; }
    public QuizTitle Title { get; set; } = new QuizTitle();
    public string? Description { get; set; }
    // Çoklu dil alanları (opsiyonel): mevcut Title/Description ile birlikte çalışır
    public string? TitleTr { get; set; }
    public string? TitleEn { get; set; }
    public string? DescriptionTr { get; set; }
    public string? DescriptionEn { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public int? CoverImageWidth { get; set; }
    public int? CoverImageHeight { get; set; }
    public string AuthorUserName { get; set; } = string.Empty; // later: UserId
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsPublic { get; set; } = true;
    // Metrics
    public int Plays { get; set; } = 0;
    // Derived metrics (computed from related tables but kept for convenience queries in projections)
    // Not required in schema; existing queries compute from relations. Keep here if needed later.
    // public int Reactions { get; set; }
    // public int Comments { get; set; }

    // Moderation
    public ModerationStatus Moderation { get; set; } = ModerationStatus.Approved;
    public string? ModerationNotes { get; set; }

    public ICollection<QuizChoice> Choices { get; set; } = new List<QuizChoice>();
    public ICollection<QuizTag> QuizTags { get; set; } = new List<QuizTag>();
}
