namespace Choosr.Web.ViewModels;

public class AdminDashboardViewModel
{
    public List<AdminDailyPoint> DailyNewQuizzes { get; set; } = new();
    public List<AdminDailyPoint> DailyActiveUsers { get; set; } = new();
    public List<AdminTopTag> TopTags { get; set; } = new();
}

public class AdminDailyPoint
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public class AdminTopTag
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
