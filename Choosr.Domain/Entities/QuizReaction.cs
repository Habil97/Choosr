namespace Choosr.Domain.Entities;

public class QuizReaction
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public string UserId { get; set; } = string.Empty; // AspNetUsers.Id
    public string Type { get; set; } = "like"; // like | love | haha | wow | sad | angry (genişletilebilir)

    public Quiz Quiz { get; set; } = null!;
}
