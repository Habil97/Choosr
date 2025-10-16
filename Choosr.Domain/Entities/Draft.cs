namespace Choosr.Domain.Entities;

public class Draft
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Genel";
    public string Visibility { get; set; } = "public"; // public | unlisted
    public bool IsAnonymous { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string? CoverImageUrl { get; set; }
    public int? CoverImageWidth { get; set; }
    public int? CoverImageHeight { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<DraftChoice> Choices { get; set; } = new();
}

public class DraftChoice
{
    public Guid Id { get; set; }
    public Guid DraftId { get; set; }
    public string? ImageUrl { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? YoutubeUrl { get; set; }
    public string? Caption { get; set; }
    public int Order { get; set; }
    public Draft? Draft { get; set; }
}

public class DraftRevision
{
    public Guid Id { get; set; }
    public Guid DraftId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Snapshot of draft fields at that time
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Genel";
    public string Visibility { get; set; } = "public"; // public | unlisted
    public bool IsAnonymous { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string? CoverImageUrl { get; set; }
    public int? CoverImageWidth { get; set; }
    public int? CoverImageHeight { get; set; }

    // Store choices snapshot as JSON for simplicity
    public string ChoicesJson { get; set; } = "[]";
}
