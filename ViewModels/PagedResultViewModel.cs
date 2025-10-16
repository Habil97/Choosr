namespace Choosr.Web.ViewModels;

public class PagedResultViewModel<T>
{
    public required IEnumerable<T> Items { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public string? Category { get; set; }
    public string? Q { get; set; }
}
