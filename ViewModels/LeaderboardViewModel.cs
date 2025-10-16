namespace Choosr.Web.ViewModels;

public class LeaderboardViewModel
{
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public string Period { get; set; } = "Haftalık"; // Haftalık | Aylık
    public string Mode { get; set; } = "Oluşturucu"; // Oluşturucu | Oyuncu
    public string Metric { get; set; } = "Toplam Oyun"; // Toplam Oyun | Tekil Oyuncu
    // Global Creators filters
    public string? Category { get; set; }
    public IEnumerable<string> Categories { get; set; } = Array.Empty<string>();
    public int Top { get; set; } = 20;
    public List<LeaderboardEntry> Entries { get; set; } = new();
}

public class LeaderboardEntry
{
    public string UserName { get; set; } = string.Empty;
    public int Plays { get; set; }
    // Optional presentation fields
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
