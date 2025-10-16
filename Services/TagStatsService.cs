using Choosr.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Choosr.Web.Services;

public record TagStatDto(string Name, int Count);
public record TagCoOccurrenceDto(string Tag, List<(string Other,int Count)> Others);

public interface ITagStatsService
{
    IReadOnlyList<TagStatDto> GetTopTags(int take = 30);
    TagCoOccurrenceDto GetCoOccurrences(string tag, int take = 10);
}

public class TagStatsService : ITagStatsService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private static readonly string TAG_CACHE_KEY = "tagstats:top";
    private static readonly TimeSpan TopTagsTtl = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan CoOccurTtl = TimeSpan.FromMinutes(20);
    public TagStatsService(AppDbContext db, IMemoryCache cache){ _db=db; _cache=cache; }

    public IReadOnlyList<TagStatDto> GetTopTags(int take = 30)
    {
        var ver = TagStatsInvalidator.GetVersion(_cache);
        var key = $"{TAG_CACHE_KEY}:{ver}:{take}";
        return _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TopTagsTtl;
            var rows = _db.QuizTags
                .AsNoTracking()
                .Where(qt => qt.Quiz != null && qt.Quiz.IsPublic)
                .GroupBy(qt => qt.Tag!.Name)
                .Select(g => new TagStatDto((string)g.Key, g.Count()))
                .OrderByDescending(x=>x.Count)
                .ThenBy(x=>x.Name)
                .Take(take)
                .ToList();
            return (IReadOnlyList<TagStatDto>)rows;
        })!;
    }

    public TagCoOccurrenceDto GetCoOccurrences(string tag, int take = 10)
    {
        if(string.IsNullOrWhiteSpace(tag)) return new TagCoOccurrenceDto(tag, new());
        tag = tag.Trim().ToLowerInvariant();
        // Versioned cache per tag
        var ver = TagStatsInvalidator.GetVersion(_cache);
        var key = $"tagstats:co:{ver}:{tag}:{take}";
        return _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CoOccurTtl;
            // Quiz bazında aynı quiz içindeki diğer tagları bul
            var quizIds = _db.QuizTags.AsNoTracking()
                .Where(qt=> EF.Property<string>(qt.Tag!, "Name") == tag && qt.Quiz!.IsPublic)
                .Select(qt=>qt.QuizId).Distinct().ToList();
            if(quizIds.Count==0) return new TagCoOccurrenceDto(tag, new());
            var others = _db.QuizTags.AsNoTracking()
                .Where(qt => quizIds.Contains(qt.QuizId) && EF.Property<string>(qt.Tag!, "Name") != tag && qt.Quiz!.IsPublic)
                .GroupBy(qt => qt.Tag!.Name)
                .Select(g => new { Name = (string)g.Key, C = g.Count() })
                .OrderByDescending(x=>x.C)
                .ThenBy(x=>x.Name)
                .Take(take)
                .ToList();
            return new TagCoOccurrenceDto(tag, others.Select(o => (o.Name, o.C)).ToList());
        })!;
    }
}