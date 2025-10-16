namespace Choosr.Domain.Models;

public class UserProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = string.Empty; // unique handle
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Twitter { get; set; }
    public string? Instagram { get; set; }
    public string? Youtube { get; set; }
    public string? Twitch { get; set; }
    public string? Kick { get; set; }
    public int CreatedCount { get; set; }
    public int PlayedCount { get; set; }
    public int CommentCount { get; set; }
    public int ReactionCount { get; set; }
}