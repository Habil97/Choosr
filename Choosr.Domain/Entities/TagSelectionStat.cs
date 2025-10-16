namespace Choosr.Domain.Entities;

public class TagSelectionStat
{
    public string Tag { get; set; } = string.Empty; // normalized lower-case tag acts as PK
    public int Count { get; set; }
    public DateTime LastSelectedAt { get; set; }
}
