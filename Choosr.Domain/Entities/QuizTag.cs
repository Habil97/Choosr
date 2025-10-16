namespace Choosr.Domain.Entities;

public class QuizTag
{
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
