namespace Choosr.Domain.Entities;

public class PlaySession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuizId { get; set; }
    public Guid? ChampionId { get; set; }
    public string Mode { get; set; } = "unknown"; // vs | bracket | rank | other
    public string? UserName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Quiz? Quiz { get; set; }
}
