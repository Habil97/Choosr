namespace Choosr.Web.ViewModels;

public class DraftChoiceViewModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? ImageUrl { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? YoutubeUrl { get; set; }
    public string? Caption { get; set; }
    public int Order { get; set; }
}

public class DraftViewModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Genel";
    public string Visibility { get; set; } = "public"; // public | unlisted
    public bool IsAnonymous { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string? CoverImageUrl { get; set; }
    public int? CoverImageWidth { get; set; }
    public int? CoverImageHeight { get; set; }
    public List<DraftChoiceViewModel> Choices { get; set; } = new();
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
