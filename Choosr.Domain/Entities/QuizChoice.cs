namespace Choosr.Domain.Entities;

public class QuizChoice
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? YoutubeUrl { get; set; }
    public string? Caption { get; set; }
    public int Order { get; set; }
}
