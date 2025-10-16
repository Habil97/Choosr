using System.ComponentModel.DataAnnotations;

namespace Choosr.Domain.Entities;

public class QuizComment
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    [MaxLength(64)]
    public string UserId { get; set; } = string.Empty;
    [MaxLength(64)]
    public string UserName { get; set; } = string.Empty;
    [MaxLength(5000)]
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Quiz? Quiz { get; set; }
}