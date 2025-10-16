using System.Text.RegularExpressions;

namespace Choosr.Web.Services;

public interface IBadWordsFilter
{
    bool ContainsBadWords(string text);
}

public class FileBadWordsFilter : IBadWordsFilter
{
    private readonly HashSet<string> _words;
    private readonly Regex _normalize;

    public FileBadWordsFilter(IHostEnvironment env)
    {
        _normalize = new Regex("\\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        var file = Path.Combine(dataDir, "bad-words.txt");
        try
        {
            if(File.Exists(file))
            {
                var lines = File.ReadAllLines(file)
                    .Select(l => (l ?? string.Empty).Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
                    .Select(l => l.ToLowerInvariant())
                    .ToList();
                _words = new HashSet<string>(lines, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            _words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool ContainsBadWords(string text)
    {
        if(string.IsNullOrWhiteSpace(text)) return false;
        var clean = text.ToLowerInvariant();
        // Tokenize on non-letters to avoid partial matches in URLs
        var tokens = Regex.Split(clean, "[^a-zA-ZğüşöçıİĞÜŞÖÇ0-9]+").Where(t => !string.IsNullOrWhiteSpace(t));
        foreach(var t in tokens)
        {
            if(_words.Contains(t)) return true;
        }
        return false;
    }
}
