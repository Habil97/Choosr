namespace Choosr.Web.Services;

public interface IFileScanner
{
    Task<bool> IsSafeAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}

public class NoopFileScanner : IFileScanner
{
    public Task<bool> IsSafeAsync(Stream fileStream, string fileName, CancellationToken ct = default) => Task.FromResult(true);
}
