using Choosr.Domain.Entities;
using Choosr.Infrastructure.Data;
using Choosr.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Choosr.Web.Mappers;

namespace Choosr.Web.Services;

public class EfQuizService : IQuizService
{
    private readonly AppDbContext _db;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
    private readonly Choosr.Web.Services.ITagStatsInvalidator _tagStatsInvalidator;
    private readonly IHttpContextAccessor _http;
    public EfQuizService(AppDbContext db, Microsoft.Extensions.Caching.Memory.IMemoryCache cache, Choosr.Web.Services.ITagStatsInvalidator tagStatsInvalidator, IHttpContextAccessor http){ _db = db; _cache = cache; _tagStatsInvalidator = tagStatsInvalidator; _http = http; }

    private static QuizCardViewModel MapCard(Quiz q) => q.ToCardViewModel();

    private QuizDetailViewModel MapDetailWithStats(Quiz q)
    {
        var vm = q.ToDetailViewModelBasic();
        vm.Comments = _db.QuizComments.Count(c=>c.QuizId==q.Id);
        vm.Reactions = _db.QuizReactions.Count(r=>r.QuizId==q.Id);
        var choices = (q.Choices ?? new List<QuizChoice>()).OrderBy(c => c.Order).ToList();
        var statRows = _db.QuizChoiceStats.AsNoTracking().Where(s => s.QuizId == q.Id).ToList();
        var picksMap = statRows.ToDictionary(s => s.ChoiceId, s => s.Picks);
        var matchesMap = statRows.ToDictionary(s => s.ChoiceId, s => s.Matches);
        var winsMap = statRows.ToDictionary(s => s.ChoiceId, s => s.Wins);
        var champsMap = statRows.ToDictionary(s => s.ChoiceId, s => s.Champions);
        var totalChampions = statRows.Sum(s => s.Champions);
        vm.Choices = choices.Select(c => new QuizChoiceViewModel
        {
            Id = c.Id,
            ImageUrl = c.ImageUrl,
            ImageWidth = c.ImageWidth,
            ImageHeight = c.ImageHeight,
            YoutubeUrl = c.YoutubeUrl,
            Caption = c.Caption,
            Order = c.Order,
            Picks = picksMap.TryGetValue(c.Id, out var p) ? p : 0,
            Matches = matchesMap.TryGetValue(c.Id, out var m) ? m : 0,
            Wins = winsMap.TryGetValue(c.Id, out var w) ? w : 0,
            Champions = champsMap.TryGetValue(c.Id, out var ch) ? ch : 0,
            // Legacy percentage remains (over picks) but not used in UI
            Percent = 0,
            ChampionRate = totalChampions > 0 ? Math.Round(((champsMap.TryGetValue(c.Id, out var ch2) ? ch2 : 0) * 100.0) / totalChampions, 0) : 0,
            WinRate = (matchesMap.TryGetValue(c.Id, out var m2) ? m2 : 0) > 0 ? Math.Round(((winsMap.TryGetValue(c.Id, out var w2) ? w2 : 0) * 100.0) / (m2), 0) : 0
        }).ToList();

        // Reaksiyon sayıları (type bazında)
        var reactCounts = _db.QuizReactions.AsNoTracking().Where(r=>r.QuizId==q.Id).GroupBy(r=>r.Type).Select(g=> new { Type=g.Key, C=g.Count() }).ToList();
        // Toplamı basitçe toplayalım (ayrıntı UI'da gösterilecek)
        vm.Reactions = reactCounts.Sum(x=>x.C);
        return vm;
    }

    public int GetQuizCommentsCount(Guid quizId)
    {
        return _db.QuizComments.Count(c=>c.QuizId==quizId);
    }
    public int GetUserCommentsCount(string userId)
    {
        return _db.QuizComments.Count(c=>c.UserId==userId);
    }

    public int GetUserReactionsCount(string userId)
    {
        return _db.QuizReactions.Count(r=>r.UserId==userId);
    }

    public IEnumerable<Guid> GetReactedQuizIdsByUser(string userId)
    {
        return _db.QuizReactions.AsNoTracking().Where(r=>r.UserId==userId).Select(r=>r.QuizId).Distinct().ToList();
    }

    public QuizDetailViewModel Add(QuizDetailViewModel quiz)
    {
        var entity = new Quiz
        {
            Id = quiz.Id == Guid.Empty ? Guid.NewGuid() : quiz.Id,
            Title = quiz.Title ?? string.Empty,
            Description = quiz.Description,
            // Basit atama: eğer kültür TR ise TitleTr/DescriptionTr doldur, EN ise TitleEn/DescriptionEn; diğer durumlarda boş bırak
            TitleTr = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase) ? (quiz.Title ?? string.Empty) : null,
            TitleEn = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase) ? (quiz.Title ?? string.Empty) : null,
            DescriptionTr = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase) ? (quiz.Description ?? string.Empty) : null,
            DescriptionEn = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase) ? (quiz.Description ?? string.Empty) : null,
            Category = quiz.Category,
            CoverImageUrl = quiz.CoverImageUrl,
            CoverImageWidth = quiz.CoverImageWidth,
            CoverImageHeight = quiz.CoverImageHeight,
            AuthorUserName = string.IsNullOrWhiteSpace(quiz.AuthorUserName) ? "anon" : quiz.AuthorUserName,
            CreatedAt = quiz.CreatedAt == default ? DateTime.UtcNow : quiz.CreatedAt,
            IsPublic = quiz.IsPublic,
            Choices = quiz.Choices.ToEntityChoices(quiz.Id == Guid.Empty ? Guid.NewGuid() : quiz.Id)
        };
        // Moderation default: Approved; if controller flagged, set Pending and store a note
        var flag = _http?.HttpContext?.Items?["ModerationFlag"] as string ?? (_http?.HttpContext?.Request?.Headers?["X-Moderation-Flag"].ToString());
        // Since TempData isn't directly accessible here without controller context, use Items as fallback; controller didn't set it though. Default to Approved.
        entity.Moderation = Choosr.Domain.Entities.ModerationStatus.Approved;
        if(string.Equals(flag, "pending", StringComparison.OrdinalIgnoreCase))
        {
            entity.Moderation = Choosr.Domain.Entities.ModerationStatus.Pending;
            entity.ModerationNotes = "Auto-flagged by bad-words filter";
        }
        // Kapak güvenliği: boş ise ilk seçimin görseli ya da youtube küçük resmi
        if(string.IsNullOrWhiteSpace(entity.CoverImageUrl))
        {
            var first = entity.Choices.OrderBy(c=>c.Order).FirstOrDefault();
            if(first!=null)
            {
                if(!string.IsNullOrWhiteSpace(first.ImageUrl)) entity.CoverImageUrl = first.ImageUrl;
                else if(!string.IsNullOrWhiteSpace(first.YoutubeUrl))
                {
                    var vid = TryExtractYouTubeId(first.YoutubeUrl!);
                    if(!string.IsNullOrWhiteSpace(vid)) entity.CoverImageUrl = $"https://img.youtube.com/vi/{vid}/hqdefault.jpg";
                }
            }
            if(string.IsNullOrWhiteSpace(entity.CoverImageUrl)) entity.CoverImageUrl = "/img/sample1.jpg";
        }
        // Tags: ensure unique Tag entities
        var tagNames = (quiz.Tags ?? Array.Empty<string>()).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t=> t.Trim().ToLowerInvariant()).Distinct().ToList();
        if(tagNames.Count > 0)
        {
            var existingTags = _db.Tags.Where(t => tagNames.Contains(t.Name)).ToList();
            var toAdd = tagNames.Except(existingTags.Select(t=>(string)t.Name)).Select(n => new Tag{ Id = Guid.NewGuid(), Name = n }).ToList();
            if(toAdd.Count>0) _db.Tags.AddRange(toAdd);
            var allTags = existingTags.Concat(toAdd).ToList();
            foreach(var t in allTags)
                entity.QuizTags.Add(new QuizTag{ QuizId = entity.Id, TagId = t.Id, Tag = t, Quiz = entity });
        }
        _db.Quizzes.Add(entity);
        _db.SaveChanges();
        // Invalidate cached categories (new category possible)
        _cache.Remove("quiz-categories");
    // Tags might have changed; invalidate tag stats
    _tagStatsInvalidator.InvalidateAll();
        // Return mapped detail
        var saved = _db.Quizzes.Include(q=>q.Choices).Include(q=>q.QuizTags).ThenInclude(qt=>qt.Tag).First(x=>x.Id==entity.Id);
    return MapDetailWithStats(saved);
    }

    public QuizDetailViewModel? Update(QuizDetailViewModel quiz)
    {
        var q = _db.Quizzes.Include(x=>x.Choices).Include(x=>x.QuizTags).FirstOrDefault(x=>x.Id==quiz.Id);
        if(q==null) return null;
        q.ApplyScalarsFromViewModel(quiz);
    // Güncellemede kültüre göre lokalize alanları da set et (varsayılan basit mantık)
    var lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
    if(lang == "tr") { q.TitleTr = quiz.Title; q.DescriptionTr = quiz.Description; }
    else if(lang == "en") { q.TitleEn = quiz.Title; q.DescriptionEn = quiz.Description; }
        // Basit yaklaşım: mevcut choice’ları silip geleni ekle
        _db.QuizChoices.RemoveRange(q.Choices);
    q.Choices = quiz.Choices.Select(c=> new QuizChoice{ Id = c.Id==Guid.Empty? Guid.NewGuid():c.Id, QuizId=q.Id, ImageUrl=c.ImageUrl, ImageWidth=c.ImageWidth, ImageHeight=c.ImageHeight, YoutubeUrl=c.YoutubeUrl, Caption=c.Caption, Order=c.Order }).ToList();
        // Etiketleri güncelle (mevcut junctionları sil, sonra ekle)
        var oldTags = _db.QuizTags.Where(x=>x.QuizId==q.Id).ToList();
        _db.QuizTags.RemoveRange(oldTags);
        var tagNames = (quiz.Tags ?? Array.Empty<string>()).Where(t=>!string.IsNullOrWhiteSpace(t)).Select(t=>t.Trim().ToLowerInvariant()).Distinct().ToList();
        if(tagNames.Count>0){
            var existingTags = _db.Tags.Where(t=>tagNames.Contains(t.Name)).ToList();
            var toAdd = tagNames.Except(existingTags.Select(t=>(string)t.Name)).Select(n=> new Tag{ Id=Guid.NewGuid(), Name=n }).ToList();
            if(toAdd.Count>0) _db.Tags.AddRange(toAdd);
            var allTags = existingTags.Concat(toAdd).ToList();
            foreach(var t in allTags) _db.QuizTags.Add(new QuizTag{ QuizId=q.Id, TagId=t.Id });
        }
        _db.SaveChanges();
        _cache.Remove("quiz-categories");
    // Tags might have changed for this quiz; invalidate tag stats
    _tagStatsInvalidator.InvalidateAll();
    var saved = _db.Quizzes.Include(x=>x.Choices).Include(x=>x.QuizTags).ThenInclude(x=>x.Tag).First(x=>x.Id==q.Id);
    return MapDetailWithStats(saved);
    }

    public bool Delete(Guid id, string requesterUserName)
    {
        var q = _db.Quizzes.Include(x=>x.Choices).FirstOrDefault(x=>x.Id==id);
        if(q==null) return false;
        if(!string.Equals(q.AuthorUserName, requesterUserName, StringComparison.OrdinalIgnoreCase)) return false;
        // Remove related stats first due to Restrict FK
        var stats = _db.QuizChoiceStats.Where(s=>s.QuizId==id).ToList();
        if(stats.Count>0) _db.QuizChoiceStats.RemoveRange(stats);
        _db.Quizzes.Remove(q);
        _db.SaveChanges();
        _cache.Remove("quiz-categories");
        // Removing a quiz affects tag counts; invalidate tag stats
        _tagStatsInvalidator.InvalidateAll();
        return true;
    }

    private static string? TryExtractYouTubeId(string url)
    {
        try
        {
            var u = new UriBuilder(url).Uri;
            // 1) watch?v=ID
            var q = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(u.Query);
            if(q.TryGetValue("v", out var vv))
            {
                var id = vv.ToString();
                if(!string.IsNullOrWhiteSpace(id)) return id;
            }
            // 2) youtu.be/ID
            if(u.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            {
                var seg = u.AbsolutePath.Trim('/');
                if(!string.IsNullOrWhiteSpace(seg)) return seg;
            }
            // 3) embed/ID
            var parts = u.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var idx = Array.IndexOf(parts, "embed");
            if(idx >= 0 && idx+1 < parts.Length) return parts[idx+1];
        }
        catch { }
        return null;
    }

    public IEnumerable<QuizCardViewModel> GetEditorPicks(int take = 6)
    {
        var cacheKey = $"editor-picks:{take}";
        if(_cache.TryGetValue(cacheKey, out List<QuizCardViewModel>? cached) && cached!=null) return cached;
        // Temporary: latest as editor picks
        var baseItems = _db.Quizzes.AsNoTracking()
            .Where(q=>q.IsPublic && q.Moderation == Choosr.Domain.Entities.ModerationStatus.Approved)
            .OrderByDescending(q=>q.CreatedAt)
            .Take(take)
            .Select(q => new {
                q.Id,
                Title = (string)q.Title,
                q.Description,
                q.Category,
                q.CoverImageUrl,
                q.CoverImageWidth,
                q.CoverImageHeight,
                q.AuthorUserName,
                q.CreatedAt,
                q.Plays,
                ItemsCount = q.Choices.Count,
                Comments = _db.QuizComments.Count(c=>c.QuizId==q.Id),
                Reactions = _db.QuizReactions.Count(r=>r.QuizId==q.Id)
            })
            .ToList();
        var items = baseItems.Select(x => new QuizCardViewModel{
            Id = x.Id,
            Title = x.Title,
            Description = x.Description ?? string.Empty,
            Category = x.Category ?? string.Empty,
            CoverImageUrl = x.CoverImageUrl ?? string.Empty,
            CoverImageWidth = x.CoverImageWidth,
            CoverImageHeight = x.CoverImageHeight,
            Plays = x.Plays,
            Comments = x.Comments,
            Reactions = x.Reactions,
            IsEditorPick = true,
            IsTrending = false,
            AuthorUserName = x.AuthorUserName ?? string.Empty,
            AuthorAvatarUrl = null,
            CreatedAt = x.CreatedAt,
            ItemsCount = x.ItemsCount,
            Tags = Array.Empty<string>()
        }).ToList();
        AttachAvatars(items);
        _cache.Set(cacheKey, items, new MemoryCacheEntryOptions{ AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) });
        return items;
    }

    public QuizDetailViewModel? GetById(Guid id)
    {
        var q = _db.Quizzes.AsNoTracking()
            .Include(q=>q.Choices)
            .Include(q=>q.QuizTags).ThenInclude(qt=>qt.Tag)
            .FirstOrDefault(q=>q.Id==id);
        if(q==null) return null;
    var vm = MapDetailWithStats(q);
        var author = _db.Users.AsNoTracking().FirstOrDefault(u => u.UserName == q.AuthorUserName);
    vm.AuthorAvatarUrl = string.IsNullOrWhiteSpace(author?.AvatarUrl) ? "/img/anon-avatar.svg" : author!.AvatarUrl;
        // Load top winners (captions) from stats if any
        var topStats = _db.QuizChoiceStats.AsNoTracking()
            .Where(s=>s.QuizId==id)
            .OrderByDescending(s=>s.Picks)
            .Take(5)
            .ToList();
        if(topStats.Count > 0)
        {
            var capById = vm.Choices.ToDictionary(c=>c.Id, c=> c.Caption ?? "");
            vm.LastWinners = topStats.Select(s => capById.TryGetValue(s.ChoiceId, out var cap) ? cap : "").Where(c=>!string.IsNullOrWhiteSpace(c)).ToList();
        }
        return vm;
    }

    public IEnumerable<string> GetCategories()
    {
        const string key = "quiz-categories";
        if(!_cache.TryGetValue(key, out object? catsObj) || catsObj is not List<string> cats)
        {
            cats = _db.Quizzes.AsNoTracking().Select(q=>q.Category).Distinct().OrderBy(x=>x).ToList();
            _cache.Set(key, cats, new MemoryCacheEntryOptions{ AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(120) });
        }
        return cats;
    }

    public IEnumerable<string> GetAllTags()
    {
        // Only return tags that are attached to at least one public quiz
        return _db.QuizTags
            .AsNoTracking()
            .Where(qt => qt.Quiz != null && qt.Quiz.IsPublic && qt.Quiz.Moderation == Choosr.Domain.Entities.ModerationStatus.Approved)
            .Select(qt => (string)qt.Tag!.Name)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    // Reaction API-like helpers
    public (int like,int love,int haha,int wow,int sad,int angry) GetReactionBreakdown(Guid quizId)
    {
        var map = _db.QuizReactions.AsNoTracking().Where(r=>r.QuizId==quizId)
            .GroupBy(r=>r.Type)
            .ToDictionary(g=>g.Key, g=>g.Count());
        int G(string k)=> map.TryGetValue(k, out var v)? v:0;
        return (G("like"), G("love"), G("haha"), G("wow"), G("sad"), G("angry"));
    }

    public string? GetUserReaction(Guid quizId, string userId)
    {
        return _db.QuizReactions.AsNoTracking().FirstOrDefault(r=>r.QuizId==quizId && r.UserId==userId)?.Type;
    }

    public void SetReaction(Guid quizId, string userId, string type)
    {
        // tek reaksiyon politikası: aynı kullanıcı yeni tip seçerse var olanı güncelle
        var r = _db.QuizReactions.FirstOrDefault(x=>x.QuizId==quizId && x.UserId==userId);
        if(r==null){ r = new QuizReaction{ Id=Guid.NewGuid(), QuizId=quizId, UserId=userId, Type=type }; _db.QuizReactions.Add(r); }
        else { r.Type = type; _db.QuizReactions.Update(r); }
        _db.SaveChanges();
    }

    public void ClearReaction(Guid quizId, string userId)
    {
        var r = _db.QuizReactions.FirstOrDefault(x=>x.QuizId==quizId && x.UserId==userId);
        if(r!=null){ _db.QuizReactions.Remove(r); _db.SaveChanges(); }
    }

    // Comments
    public IEnumerable<CommentViewModel> GetComments(Guid quizId, int take = 50, int skip = 0)
    {
        return _db.QuizComments.AsNoTracking()
            .Where(c=>c.QuizId==quizId)
            .OrderByDescending(c=>c.CreatedAt)
            .Skip(skip).Take(take)
            .Select(c=> new CommentViewModel{ Id = c.Id, QuizId = c.QuizId, UserId = c.UserId, UserName = c.UserName, Text = c.Text, CreatedAt = c.CreatedAt })
            .ToList();
    }
    public CommentViewModel AddComment(Guid quizId, string userId, string userName, string text)
    {
        var c = new QuizComment{ Id = Guid.NewGuid(), QuizId = quizId, UserId = userId, UserName = userName, Text = text, CreatedAt = DateTime.UtcNow };
        _db.QuizComments.Add(c);
        _db.SaveChanges();
        // Optionally update cached stats on quiz (Comments count) later
        return new CommentViewModel{ Id = c.Id, QuizId = c.QuizId, UserId = c.UserId, UserName = c.UserName, Text = c.Text, CreatedAt = c.CreatedAt };
    }

    public IEnumerable<QuizCardViewModel> GetLatest(int take = 6)
    {
        var key = $"latest:{take}";
        if(_cache.TryGetValue(key, out List<QuizCardViewModel>? cached) && cached!=null) return cached;
        var baseItems = _db.Quizzes.AsNoTracking()
            .Where(q=>q.IsPublic && q.Moderation == Choosr.Domain.Entities.ModerationStatus.Approved)
            .OrderByDescending(q=>q.CreatedAt)
            .Take(take)
            .Select(q=> new {
                q.Id,
                q.Title,
                q.Description,
                q.Category,
                q.CoverImageUrl,
                q.CoverImageWidth,
                q.CoverImageHeight,
                q.AuthorUserName,
                q.CreatedAt,
                q.Plays,
                ItemsCount = q.Choices.Count,
                Comments = _db.QuizComments.Count(c=>c.QuizId==q.Id),
                Reactions = _db.QuizReactions.Count(r=>r.QuizId==q.Id)
            })
            .ToList();
        var latest = baseItems.Select(x=> new QuizCardViewModel{
            Id = x.Id,
            Title = x.Title,
            Description = x.Description ?? string.Empty,
            Category = x.Category ?? string.Empty,
            CoverImageUrl = x.CoverImageUrl ?? string.Empty,
            CoverImageWidth = x.CoverImageWidth,
            CoverImageHeight = x.CoverImageHeight,
            Plays = x.Plays,
            Comments = x.Comments,
            Reactions = x.Reactions,
            IsEditorPick = false,
            IsTrending = false,
            AuthorUserName = x.AuthorUserName ?? string.Empty,
            AuthorAvatarUrl = null,
            CreatedAt = x.CreatedAt,
            ItemsCount = x.ItemsCount,
            Tags = Array.Empty<string>()
        }).ToList();
        AttachAvatars(latest);
        _cache.Set(key, latest, new MemoryCacheEntryOptions{ AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(45) });
        return latest;
    }

    public IEnumerable<QuizCardViewModel> GetPopular(int take = 6)
    {
        var key = $"popular-{take}";
        if(_cache.TryGetValue(key, out List<QuizCardViewModel>? cached) && cached!=null) return cached;
        var baseItems = _db.Quizzes.AsNoTracking()
            .Where(q=>q.IsPublic)
            .OrderByDescending(q=>q.Plays)
            .ThenByDescending(q=>q.CreatedAt)
            .Take(take)
            .Select(q=> new {
                q.Id,
                Title = (string)q.Title,
                q.Description,
                q.Category,
                q.CoverImageUrl,
                q.CoverImageWidth,
                q.CoverImageHeight,
                q.AuthorUserName,
                q.CreatedAt,
                q.Plays,
                ItemsCount = q.Choices.Count,
                Comments = _db.QuizComments.Count(c=>c.QuizId==q.Id),
                Reactions = _db.QuizReactions.Count(r=>r.QuizId==q.Id)
            })
            .ToList();
        var popular = baseItems.Select(x=> new QuizCardViewModel{
            Id = x.Id,
            Title = x.Title,
            Description = x.Description ?? string.Empty,
            Category = x.Category ?? string.Empty,
            CoverImageUrl = x.CoverImageUrl ?? string.Empty,
            CoverImageWidth = x.CoverImageWidth,
            CoverImageHeight = x.CoverImageHeight,
            Plays = x.Plays,
            Comments = x.Comments,
            Reactions = x.Reactions,
            IsEditorPick = false,
            IsTrending = false,
            AuthorUserName = x.AuthorUserName ?? string.Empty,
            AuthorAvatarUrl = null,
            CreatedAt = x.CreatedAt,
            ItemsCount = x.ItemsCount,
            Tags = Array.Empty<string>()
        }).ToList();
        AttachAvatars(popular);
        _cache.Set(key, popular, new MemoryCacheEntryOptions{ AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) });
        return popular;
    }

    public IEnumerable<QuizCardViewModel> GetByAuthor(string authorUserName, bool includeNonPublic = false)
    {
        var query = _db.Quizzes.AsNoTracking()
            .Where(q=> q.AuthorUserName == authorUserName);
        if(!includeNonPublic)
        {
            query = query.Where(q=>q.IsPublic && q.Moderation == Choosr.Domain.Entities.ModerationStatus.Approved);
        }
        var baseItems = query
            .OrderByDescending(q=>q.CreatedAt)
            .Select(q=> new {
                q.Id,
                Title = (string)q.Title,
                q.Description,
                q.Category,
                q.CoverImageUrl,
                q.CoverImageWidth,
                q.CoverImageHeight,
                q.AuthorUserName,
                q.CreatedAt,
                q.Plays,
                ItemsCount = q.Choices.Count,
                Comments = _db.QuizComments.Count(c=>c.QuizId==q.Id),
                Reactions = _db.QuizReactions.Count(r=>r.QuizId==q.Id)
            })
            .ToList();
        var items = baseItems.Select(x=> new QuizCardViewModel{
            Id = x.Id,
            Title = x.Title,
            Description = x.Description ?? string.Empty,
            Category = x.Category ?? string.Empty,
            CoverImageUrl = x.CoverImageUrl ?? string.Empty,
            CoverImageWidth = x.CoverImageWidth,
            CoverImageHeight = x.CoverImageHeight,
            Plays = x.Plays,
            Comments = x.Comments,
            Reactions = x.Reactions,
            IsEditorPick = false,
            IsTrending = false,
            AuthorUserName = x.AuthorUserName ?? string.Empty,
            AuthorAvatarUrl = null,
            CreatedAt = x.CreatedAt,
            ItemsCount = x.ItemsCount,
            Tags = Array.Empty<string>()
        }).ToList();
        AttachAvatars(items);
        return items;
    }

    public IEnumerable<QuizCardViewModel> GetTrending(int take = 6)
    {
        var key = $"trending-{take}";
        if(_cache.TryGetValue(key, out List<QuizCardViewModel>? cached) && cached!=null) return cached;
        var baseItems = _db.Quizzes.AsNoTracking()
            .Where(q=>q.IsPublic && q.Moderation == Choosr.Domain.Entities.ModerationStatus.Approved)
            .OrderByDescending(q=>q.CreatedAt)
            .Take(take)
            .Select(q=> new {
                q.Id,
                Title = (string)q.Title,
                q.Description,
                q.Category,
                q.CoverImageUrl,
                q.CoverImageWidth,
                q.CoverImageHeight,
                q.AuthorUserName,
                q.CreatedAt,
                q.Plays,
                ItemsCount = q.Choices.Count,
                Comments = _db.QuizComments.Count(c=>c.QuizId==q.Id),
                Reactions = _db.QuizReactions.Count(r=>r.QuizId==q.Id)
            })
            .ToList();
        var items = baseItems.Select(x=> new QuizCardViewModel{
            Id = x.Id,
            Title = x.Title ?? string.Empty,
            Description = x.Description ?? string.Empty,
            Category = x.Category ?? string.Empty,
            CoverImageUrl = x.CoverImageUrl ?? string.Empty,
            CoverImageWidth = x.CoverImageWidth,
            CoverImageHeight = x.CoverImageHeight,
            Plays = x.Plays,
            Comments = x.Comments,
            Reactions = x.Reactions,
            IsEditorPick = false,
            IsTrending = true,
            AuthorUserName = x.AuthorUserName ?? string.Empty,
            AuthorAvatarUrl = null,
            CreatedAt = x.CreatedAt,
            ItemsCount = x.ItemsCount,
            Tags = Array.Empty<string>()
        }).ToList();
        AttachAvatars(items);
        _cache.Set(key, items, new MemoryCacheEntryOptions{ AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) });
        return items;
    }
    public (IEnumerable<QuizCardViewModel> Items, int TotalCount) Search(string? category, string? q, string? tag, string? sort, int page, int pageSize)
    {
        var query = _db.Quizzes.AsNoTracking()
            .Include(x=>x.Choices)
            .Include(x=>x.QuizTags).ThenInclude(qt=>qt.Tag)
            .Where(x=>x.IsPublic && x.Moderation == Choosr.Domain.Entities.ModerationStatus.Approved)
            .AsQueryable();
        if(!string.IsNullOrWhiteSpace(category)) query = query.Where(x=>x.Category == category);
    if(!string.IsNullOrWhiteSpace(q)) query = query.Where(x=> ((string)x.Title).Contains(q));
        if(!string.IsNullOrWhiteSpace(tag)) query = query.Where(x=> x.QuizTags.Any(qt=> qt.Tag!=null && qt.Tag.Name == tag));
        sort = string.IsNullOrWhiteSpace(sort) ? "latest" : sort.Trim().ToLowerInvariant();
        if(sort == "comments" || sort == "reactions")
        {
            var q2 = query.Select(q => new {
                q.Id,
                q.Title,
                q.Description,
                q.Category,
                q.CoverImageUrl,
                q.CoverImageWidth,
                q.CoverImageHeight,
                q.AuthorUserName,
                q.CreatedAt,
                q.Plays,
                ItemsCount = q.Choices.Count,
                Comments = _db.QuizComments.Count(c=>c.QuizId==q.Id),
                Reactions = _db.QuizReactions.Count(r=>r.QuizId==q.Id)
            });
            q2 = sort == "comments"
                ? q2.OrderByDescending(x=>x.Comments).ThenByDescending(x=>x.CreatedAt)
                : q2.OrderByDescending(x=>x.Reactions).ThenByDescending(x=>x.CreatedAt);
            var total = q2.Count();
            var pageItems = q2.Skip((page-1)*pageSize).Take(pageSize).ToList();
            var items = pageItems.Select(x=> new QuizCardViewModel{
                Id = x.Id,
                Title = x.Title,
                Description = x.Description ?? string.Empty,
                Category = x.Category ?? string.Empty,
                CoverImageUrl = x.CoverImageUrl ?? string.Empty,
                CoverImageWidth = x.CoverImageWidth,
                CoverImageHeight = x.CoverImageHeight,
                Plays = x.Plays,
                Comments = x.Comments,
                Reactions = x.Reactions,
                IsEditorPick = false,
                IsTrending = false,
                AuthorUserName = x.AuthorUserName ?? string.Empty,
                AuthorAvatarUrl = null,
                CreatedAt = x.CreatedAt,
                ItemsCount = x.ItemsCount,
                Tags = Array.Empty<string>()
            }).ToList();
            AttachAvatars(items);
            return (items, total);
        }
        query = sort switch
        {
            "popular" => query.OrderByDescending(x=>x.Plays).ThenByDescending(x=>x.CreatedAt),
            "title" => query.OrderBy(x=>(string)x.Title),
            "title-desc" => query.OrderByDescending(x=>(string)x.Title),
            _ => query.OrderByDescending(x=>x.CreatedAt)
        };
        var total2 = query.Count();
        var pageBase = query.Skip((page-1)*pageSize).Take(pageSize)
            .Select(q=> new {
                q.Id,
                Title = (string)q.Title,
                q.Description,
                q.Category,
                q.CoverImageUrl,
                q.CoverImageWidth,
                q.CoverImageHeight,
                q.AuthorUserName,
                q.CreatedAt,
                q.Plays,
                ItemsCount = q.Choices.Count,
                Comments = _db.QuizComments.Count(c=>c.QuizId==q.Id),
                Reactions = _db.QuizReactions.Count(r=>r.QuizId==q.Id)
            })
            .ToList();
        var items2 = pageBase.Select(x=> new QuizCardViewModel{
            Id = x.Id,
            Title = x.Title,
            Description = x.Description ?? string.Empty,
            Category = x.Category ?? string.Empty,
            CoverImageUrl = x.CoverImageUrl ?? string.Empty,
            CoverImageWidth = x.CoverImageWidth,
            CoverImageHeight = x.CoverImageHeight,
            Plays = x.Plays,
            Comments = x.Comments,
            Reactions = x.Reactions,
            IsEditorPick = false,
            IsTrending = false,
            AuthorUserName = x.AuthorUserName ?? string.Empty,
            AuthorAvatarUrl = null,
            CreatedAt = x.CreatedAt,
            ItemsCount = x.ItemsCount,
            Tags = Array.Empty<string>()
        }).ToList();
        AttachAvatars(items2);
        return (items2, total2);
    }

    public IEnumerable<QuizCardViewModel> GetSimilar(Guid id, int take = 6)
    {
        // cache per quiz id and take
        var cacheKey = $"sim:{id}:{take}";
        if(_cache.TryGetValue(cacheKey, out List<QuizCardViewModel>? cached) && cached != null)
        {
            return cached;
        }

        var main = _db.Quizzes.AsNoTracking().Include(q=>q.QuizTags).ThenInclude(qt=>qt.Tag).FirstOrDefault(x=>x.Id==id);
        if(main==null) return Enumerable.Empty<QuizCardViewModel>();

        var mainTags = (main.QuizTags ?? new List<QuizTag>())
            .Where(qt=>qt.Tag!=null)
            .Select(qt => (string)qt.Tag!.Name)
            .Distinct()
            .ToList();
        var mainCategory = main.Category;

        // total public quizzes for IDF baseline
    var totalPublic = _db.Quizzes.AsNoTracking().Count(q=>q.IsPublic && q.Moderation == Choosr.Domain.Entities.ModerationStatus.Approved);

        // Only need DF for tags of the main quiz
        var tagDfs = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
    if(mainTags.Count > 0)
        {
            tagDfs = _db.QuizTags.AsNoTracking()
        .Where(qt => qt.Tag != null && mainTags.Contains(qt.Tag!.Name) && qt.Quiz!.IsPublic && qt.Quiz!.Moderation == Choosr.Domain.Entities.ModerationStatus.Approved)
                .GroupBy(qt => qt.Tag!.Name)
                .Select(g => new { Tag = (string)g.Key, C = g.Select(x=>x.QuizId).Distinct().Count() })
                .ToDictionary(x=>x.Tag, x=>x.C, StringComparer.OrdinalIgnoreCase);
        }

        // Build candidate set: same category or shares any main tag
        const int candidateCap = 200;
        var candidatesQuery = _db.Quizzes.AsNoTracking()
            .Where(q => q.IsPublic && q.Moderation == Choosr.Domain.Entities.ModerationStatus.Approved && q.Id != id && (
                (!string.IsNullOrWhiteSpace(mainCategory) && q.Category == mainCategory) ||
                (mainTags.Count>0 && q.QuizTags.Any(qt => qt.Tag != null && mainTags.Contains(qt.Tag!.Name)))
            ))
            .OrderByDescending(q=>q.Plays)
            .ThenByDescending(q=>q.CreatedAt)
            .Take(candidateCap)
            .Select(q => new {
                q.Id,
                Title = (string)q.Title,
                q.Description,
                q.Category,
                q.CoverImageUrl,
                q.CoverImageWidth,
                q.CoverImageHeight,
                q.AuthorUserName,
                q.CreatedAt,
                q.Plays,
                ItemsCount = q.Choices.Count,
                Comments = _db.QuizComments.Count(c=>c.QuizId==q.Id),
                Reactions = _db.QuizReactions.Count(r=>r.QuizId==q.Id)
            })
            .ToList();

        // Fetch tags for candidates in a single query
        var candIds = candidatesQuery.Select(c=>c.Id).ToList();
        var tagsByQuiz = new Dictionary<Guid, HashSet<string>>();
        if(candIds.Count>0)
        {
            var tagRows = _db.QuizTags.AsNoTracking()
                .Where(qt => candIds.Contains(qt.QuizId) && qt.Tag!=null)
                .Select(qt => new { qt.QuizId, Name = (string)qt.Tag!.Name })
                .ToList();
            foreach(var row in tagRows)
            {
                if(!tagsByQuiz.TryGetValue(row.QuizId, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    tagsByQuiz[row.QuizId] = set;
                }
                set.Add(row.Name);
            }
        }

        // Popularity normalize: compute raw popularity then scale 0..1
        double Pop(int plays, int reactions, int comments)
        {
            return Math.Log(1 + Math.Max(0, plays)) + 0.7 * Math.Log(1 + Math.Max(0, reactions)) + 0.4 * Math.Log(1 + Math.Max(0, comments));
        }
        var pops = candidatesQuery.Select(c => Pop(c.Plays, c.Reactions, c.Comments)).ToList();
        var popMax = pops.Count>0 ? pops.Max() : 1.0;

        // weights
        const double wTags = 1.0;
        const double wCat = 0.35; // small boost
        const double wPop = 0.25; // modest influence

        // IDF for a tag: log(1 + N/df)
        double Idf(string tag)
        {
            if(!tagDfs.TryGetValue(tag, out var df) || df <= 0) return 0.0;
            var n = Math.Max(1, totalPublic);
            return Math.Log(1.0 + (double)n / df);
        }

        // helper to compute tag score
        double TagScore(Guid quizId)
        {
            if(mainTags.Count==0) return 0.0;
            if(!tagsByQuiz.TryGetValue(quizId, out var tset) || tset.Count==0) return 0.0;
            double s = 0.0;
            foreach(var t in mainTags)
            {
                if(tset.Contains(t)) s += Idf(t);
            }
            return s;
        }

        var ranked = candidatesQuery
            .Select(c => new {
                C = c,
                tagScore = TagScore(c.Id),
                catScore = (!string.IsNullOrWhiteSpace(mainCategory) && string.Equals(c.Category, mainCategory, StringComparison.OrdinalIgnoreCase)) ? 1.0 : 0.0,
                popScore = popMax > 0 ? (Pop(c.Plays, c.Reactions, c.Comments) / popMax) : 0.0
            })
            .Select(x => new { x.C, score = (wTags * x.tagScore) + (wCat * x.catScore) + (wPop * x.popScore) })
            .OrderByDescending(r => r.score)
            .ThenByDescending(r => r.C.CreatedAt)
            .Take(Math.Max(1, take))
            .ToList();

        // Fallback: if no candidates found, return latest popular public quizzes excluding main
        if(ranked.Count == 0)
        {
            var fb = _db.Quizzes.AsNoTracking()
                .Where(q=>q.IsPublic && q.Moderation == Choosr.Domain.Entities.ModerationStatus.Approved && q.Id!=id)
                .OrderByDescending(q=>q.Plays).ThenByDescending(q=>q.CreatedAt)
                .Take(Math.Max(1, take))
                .Select(q=> new {
                    q.Id,
                    Title = (string)q.Title,
                    q.Description,
                    q.Category,
                    q.CoverImageUrl,
                    q.CoverImageWidth,
                    q.CoverImageHeight,
                    q.AuthorUserName,
                    q.CreatedAt,
                    q.Plays,
                    ItemsCount = q.Choices.Count,
                    Comments = _db.QuizComments.Count(c=>c.QuizId==q.Id),
                    Reactions = _db.QuizReactions.Count(r=>r.QuizId==q.Id)
                })
                .ToList();
            var fbItems = fb.Select(x=> new QuizCardViewModel{
                Id = x.Id,
                Title = x.Title,
                Description = x.Description ?? string.Empty,
                Category = x.Category ?? string.Empty,
                CoverImageUrl = x.CoverImageUrl ?? string.Empty,
                CoverImageWidth = x.CoverImageWidth,
                CoverImageHeight = x.CoverImageHeight,
                Plays = x.Plays,
                Comments = x.Comments,
                Reactions = x.Reactions,
                IsEditorPick = false,
                IsTrending = false,
                AuthorUserName = x.AuthorUserName ?? string.Empty,
                AuthorAvatarUrl = null,
                CreatedAt = x.CreatedAt,
                ItemsCount = x.ItemsCount,
                Tags = Array.Empty<string>()
            }).ToList();
            AttachAvatars(fbItems);
            _cache.Set(cacheKey, fbItems, TimeSpan.FromMinutes(10));
            return fbItems;
        }

        var items2 = ranked.Select(r => new QuizCardViewModel{
            Id = r.C.Id,
            Title = r.C.Title,
            Description = r.C.Description ?? string.Empty,
            Category = r.C.Category ?? string.Empty,
            CoverImageUrl = r.C.CoverImageUrl ?? string.Empty,
            CoverImageWidth = r.C.CoverImageWidth,
            CoverImageHeight = r.C.CoverImageHeight,
            Plays = r.C.Plays,
            Comments = r.C.Comments,
            Reactions = r.C.Reactions,
            IsEditorPick = false,
            IsTrending = false,
            AuthorUserName = r.C.AuthorUserName ?? string.Empty,
            AuthorAvatarUrl = null,
            CreatedAt = r.C.CreatedAt,
            ItemsCount = r.C.ItemsCount,
            Tags = Array.Empty<string>()
        }).ToList();
        AttachAvatars(items2);
        _cache.Set(cacheKey, items2, TimeSpan.FromMinutes(10));
        return items2;
    }

    private void AttachAvatars(IEnumerable<QuizCardViewModel> items)
    {
        var names = items.Select(i=>i.AuthorUserName).Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        if(names.Count==0) return;
        var users = _db.Users.AsNoTracking().Where(u=> names.Contains(u.UserName!)).Select(u=> new { u.UserName, u.AvatarUrl }).ToList();
    foreach(var i in items){ i.AuthorAvatarUrl = users.FirstOrDefault(u=>u.UserName==i.AuthorUserName)?.AvatarUrl ?? "/img/anon-avatar.svg"; }
    }

    // Basic counters and winners recording persisted
    public void IncreasePlays(Guid id)
    {
        var q = _db.Quizzes.FirstOrDefault(x=>x.Id==id);
        if(q==null) return;
        q.Plays += 1;
        _db.SaveChanges();
    }

    public void RecordPlay(Guid id, Guid championId, IEnumerable<(Guid winnerId, Guid loserId)> matches)
    {
        Console.WriteLine($"[RecordPlay] Quiz:{id} Champion:{championId}");
        // Helper: get or create stat safely (checks local tracker first to avoid duplicate adds before SaveChanges)
        QuizChoiceStat GetOrCreate(Guid choiceId)
        {
            var local = _db.QuizChoiceStats.Local.FirstOrDefault(s => s.QuizId==id && s.ChoiceId==choiceId);
            if(local != null) return local;
            var dbRow = _db.QuizChoiceStats.FirstOrDefault(s => s.QuizId==id && s.ChoiceId==choiceId);
            if(dbRow != null) return dbRow;
            var created = new QuizChoiceStat{ Id = Guid.NewGuid(), QuizId = id, ChoiceId = choiceId, Matches=0, Wins=0, Champions=0, Picks=0 };
            _db.QuizChoiceStats.Add(created);
            return created;
        }

        // Champion: increment Champions
        if(championId != Guid.Empty)
        {
            var champ = GetOrCreate(championId);
            champ.Champions += 1;
        }

        // Matches: for each pair, increment Matches for both, and Wins for winner. Keep legacy Picks ++ for winner.
        foreach(var m in matches)
        {
            Console.WriteLine($"[RecordPlay] Match: W={m.winnerId} L={m.loserId}");
            var w = GetOrCreate(m.winnerId);
            var l = GetOrCreate(m.loserId);
            w.Matches += 1; l.Matches += 1;
            w.Wins += 1; w.Picks += 1;
        }
        try{ _db.SaveChanges(); }
        catch(Exception ex)
        {
            Console.WriteLine("[RecordPlay][ERROR] " + ex);
            throw;
        }
    }
}
