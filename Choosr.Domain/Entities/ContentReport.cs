namespace Choosr.Domain.Entities;

public enum ReportTargetType
{
    Quiz = 1,
    Comment = 2
}

public enum ReportStatus
{
    New = 0,
    InReview = 1,
    Resolved = 2,
    Blocked = 3,
}

public class ContentReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ReportTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string? ReporterUserId { get; set; }
    public string? ReporterUserName { get; set; }
    public string? ReporterIp { get; set; }
    public string Reason { get; set; } = string.Empty; // short reason code or text
    public string? Details { get; set; } // optional free text
    public ReportStatus Status { get; set; } = ReportStatus.New;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    // Moderation workflow additions
    public string? ModeratorNotes { get; set; }
    public DateTime? InReviewAt { get; set; }
    public string? InReviewBy { get; set; }
}
