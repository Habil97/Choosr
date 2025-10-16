using Choosr.Web.ViewModels;

namespace Choosr.Web.Services;

public interface IQuizService
{
    IEnumerable<QuizCardViewModel> GetEditorPicks(int take = 6);
    IEnumerable<QuizCardViewModel> GetTrending(int take = 6);
    IEnumerable<QuizCardViewModel> GetLatest(int take = 6);
    IEnumerable<QuizCardViewModel> GetPopular(int take = 6);
    // Fetch quizzes by author; when includeNonPublic=true, return all authored quizzes including non-public ones
    IEnumerable<QuizCardViewModel> GetByAuthor(string authorUserName, bool includeNonPublic = false);
    QuizDetailViewModel? GetById(Guid id);
    IEnumerable<string> GetCategories();
    IEnumerable<string> GetAllTags();
    (IEnumerable<QuizCardViewModel> Items, int TotalCount) Search(string? category, string? q, string? tag, string? sort, int page, int pageSize);
    IEnumerable<QuizCardViewModel> GetSimilar(Guid id, int take = 6);
    QuizDetailViewModel Add(QuizDetailViewModel quiz);
    QuizDetailViewModel? Update(QuizDetailViewModel quiz);
    bool Delete(Guid id, string requesterUserName);
    void IncreasePlays(Guid id);
    // Record a finished play: champion and match wins; matches are pairwise encounters encountered during play
    void RecordPlay(Guid id, Guid championId, IEnumerable<(Guid winnerId, Guid loserId)> matches);
    // Reactions
    (int like,int love,int haha,int wow,int sad,int angry) GetReactionBreakdown(Guid quizId);
    string? GetUserReaction(Guid quizId, string userId);
    void SetReaction(Guid quizId, string userId, string type);
    void ClearReaction(Guid quizId, string userId);
    // Comments
    IEnumerable<CommentViewModel> GetComments(Guid quizId, int take = 50, int skip = 0);
    CommentViewModel AddComment(Guid quizId, string userId, string userName, string text);
    int GetQuizCommentsCount(Guid quizId);
    int GetUserCommentsCount(string userId);
    // User reactions helpers (for profile consistency with DB)
    int GetUserReactionsCount(string userId);
    IEnumerable<Guid> GetReactedQuizIdsByUser(string userId);
}

public class InMemoryQuizService : IQuizService
{
    private readonly List<QuizDetailViewModel> _quizzes;
    private readonly Dictionary<Guid,List<Guid>> _winnersByQuiz = new();
    private readonly Dictionary<(Guid quizId,string userId), string> _reactions = new();
    private readonly Dictionary<Guid, List<CommentViewModel>> _comments = new();

    public InMemoryQuizService()
    {
        _quizzes = Seed();
    }

    private static QuizCardViewModel Map(QuizDetailViewModel q) => new()
    {
        Id = q.Id,
        Title = q.Title,
        Description = q.Description,
        Category = q.Category,
        CoverImageUrl = q.CoverImageUrl,
        Plays = q.Plays,
        Comments = q.Comments,
        Reactions = q.Reactions,
        IsEditorPick = q.IsEditorPick,
        IsTrending = q.IsTrending,
        AuthorUserName = q.AuthorUserName,
        CreatedAt = q.CreatedAt,
        ItemsCount = q.Choices.Count()
    };

    public IEnumerable<QuizCardViewModel> GetEditorPicks(int take = 6) => _quizzes.Where(q => q.IsEditorPick).Take(take).Select(Map);
    public IEnumerable<QuizCardViewModel> GetTrending(int take = 6) => _quizzes.Where(q => q.IsTrending).Take(take).Select(Map);
    public IEnumerable<QuizCardViewModel> GetLatest(int take = 6) => _quizzes.OrderByDescending(q => q.CreatedAt).Take(take).Select(Map);
    public IEnumerable<QuizCardViewModel> GetPopular(int take = 6) => _quizzes.OrderByDescending(q => q.Plays).Take(take).Select(Map);
    public IEnumerable<QuizCardViewModel> GetByAuthor(string authorUserName, bool includeNonPublic = false)
    {
        var query = _quizzes.Where(q => string.Equals(q.AuthorUserName, authorUserName, StringComparison.OrdinalIgnoreCase));
        if(!includeNonPublic)
        {
            query = query.Where(q => q.IsPublic);
        }
        return query.OrderByDescending(q=>q.CreatedAt).Select(Map).ToList();
    }
    public QuizDetailViewModel? GetById(Guid id)
    {
        var q = _quizzes.FirstOrDefault(q => q.Id == id);
        if(q == null) return null;
        if(_winnersByQuiz.TryGetValue(id, out var winners))
        {
            // map to captions
            var captions = q.Choices.Where(c => winners.Contains(c.Id)).Select(c => c.Caption ?? "").ToList();
            q.LastWinners = captions;
        }
        return q;
    }
    public IEnumerable<string> GetCategories() => _quizzes.Select(q => q.Category).Distinct().OrderBy(x => x);
    public IEnumerable<string> GetAllTags() => _quizzes.SelectMany(q => q.Tags ?? Enumerable.Empty<string>()).Distinct().OrderBy(x=>x);
    public (IEnumerable<QuizCardViewModel> Items, int TotalCount) Search(string? category, string? q, string? tag, string? sort, int page, int pageSize)
    {
        var query = _quizzes.AsQueryable();
        if(!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        if(!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Title.Contains(q, StringComparison.OrdinalIgnoreCase));
        if(!string.IsNullOrWhiteSpace(tag)) query = query.Where(x => (x.Tags ?? Enumerable.Empty<string>()).Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)));
        sort = string.IsNullOrWhiteSpace(sort) ? "latest" : sort.Trim().ToLowerInvariant();
        query = sort switch
        {
            "popular" => query.OrderByDescending(x => x.Plays).ThenByDescending(x => x.CreatedAt),
            "title" => query.OrderBy(x => x.Title),
            "title-desc" => query.OrderByDescending(x => x.Title),
            "comments" => query.OrderByDescending(x => x.Comments).ThenByDescending(x => x.CreatedAt),
            "reactions" => query.OrderByDescending(x => x.Reactions).ThenByDescending(x => x.CreatedAt),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
        var total = query.Count();
        var items = query.Skip((page-1)*pageSize).Take(pageSize).Select(Map).ToList();
        return (items, total);
    }
    public IEnumerable<QuizCardViewModel> GetSimilar(Guid id, int take = 6)
    {
        var main = _quizzes.FirstOrDefault(x => x.Id == id);
        if(main == null) return Enumerable.Empty<QuizCardViewModel>();
        var mainTags = (main.Tags ?? Enumerable.Empty<string>()).Select(t=>t.ToLowerInvariant()).Distinct().ToHashSet();
        string? mainCat = main.Category;
        double Pop(QuizDetailViewModel q) => Math.Log(1 + Math.Max(0, q.Plays)) + 0.7 * Math.Log(1 + Math.Max(0, q.Reactions)) + 0.4 * Math.Log(1 + Math.Max(0, q.Comments));
        var candidates = _quizzes.Where(q => q.Id != id && q.IsPublic && (
            (!string.IsNullOrWhiteSpace(mainCat) && string.Equals(q.Category, mainCat, StringComparison.OrdinalIgnoreCase)) ||
            (mainTags.Count>0 && (q.Tags ?? Enumerable.Empty<string>()).Any(t => mainTags.Contains(t.ToLowerInvariant())))
        ));
        var pops = candidates.Select(Pop).DefaultIfEmpty(0).ToList();
        var popMax = pops.Count>0 ? pops.Max() : 1.0;
        const double wTags = 1.0; const double wCat = 0.35; const double wPop = 0.25;
        double TagScore(QuizDetailViewModel q)
        {
            if(mainTags.Count==0) return 0.0;
            var cTags = (q.Tags ?? Enumerable.Empty<string>()).Select(t=>t.ToLowerInvariant());
            int count = cTags.Count(t => mainTags.Contains(t));
            return count; // simple count for in-memory
        }
        var ranked = candidates
            .Select(q => new { Q = q, tagScore = TagScore(q), catScore = (!string.IsNullOrWhiteSpace(mainCat) && string.Equals(q.Category, mainCat, StringComparison.OrdinalIgnoreCase)) ? 1.0 : 0.0, popScore = popMax>0 ? (Pop(q)/popMax) : 0.0 })
            .OrderByDescending(x => (wTags*x.tagScore) + (wCat*x.catScore) + (wPop*x.popScore))
            .ThenByDescending(x => x.Q.CreatedAt)
            .Take(Math.Max(1, take))
            .Select(x => Map(x.Q))
            .ToList();
        return ranked;
    }

    public QuizDetailViewModel Add(QuizDetailViewModel quiz)
    {
        if(quiz.Id == Guid.Empty) quiz.Id = Guid.NewGuid();
        _quizzes.Insert(0, quiz);
        return quiz;
    }

    public QuizDetailViewModel? Update(QuizDetailViewModel quiz)
    {
        var idx = _quizzes.FindIndex(x => x.Id == quiz.Id);
        if(idx < 0) return null;
        _quizzes[idx] = quiz;
        return quiz;
    }

    public bool Delete(Guid id, string requesterUserName)
    {
        var q = _quizzes.FirstOrDefault(x => x.Id == id);
        if(q == null) return false;
        if(!string.Equals(q.AuthorUserName, requesterUserName, StringComparison.OrdinalIgnoreCase)) return false;
        _quizzes.Remove(q);
        return true;
    }

    public void IncreasePlays(Guid id)
    {
        var q = _quizzes.FirstOrDefault(x => x.Id == id);
        if(q != null) q.Plays++;
    }

    public void RecordPlay(Guid id, Guid championId, IEnumerable<(Guid winnerId, Guid loserId)> matches)
    {
        // For demo: store champion as single last-winner list
        _winnersByQuiz[id] = new List<Guid>{ championId };
    }

    public (int like,int love,int haha,int wow,int sad,int angry) GetReactionBreakdown(Guid quizId)
    {
        var items = _reactions.Where(kv => kv.Key.quizId==quizId).Select(kv=>kv.Value).ToList();
        int C(string t) => items.Count(x=>x==t);
        return (C("like"), C("love"), C("haha"), C("wow"), C("sad"), C("angry"));
    }
    public void SetReaction(Guid quizId, string userId, string type)
    {
        _reactions[(quizId,userId)] = type;
    }
    public void ClearReaction(Guid quizId, string userId)
    {
        _reactions.Remove((quizId,userId));
    }
    public string? GetUserReaction(Guid quizId, string userId)
    {
        return _reactions.TryGetValue((quizId,userId), out var t) ? t : null;
    }

    // Comments (in-memory)
    public IEnumerable<CommentViewModel> GetComments(Guid quizId, int take = 50, int skip = 0)
    {
        if(!_comments.TryGetValue(quizId, out var list)) return Enumerable.Empty<CommentViewModel>();
        return list.OrderByDescending(c=>c.CreatedAt).Skip(skip).Take(take).ToList();
    }
    public CommentViewModel AddComment(Guid quizId, string userId, string userName, string text)
    {
        var c = new CommentViewModel{ Id = Guid.NewGuid(), QuizId = quizId, UserId = userId, UserName = userName, Text = text, CreatedAt = DateTime.UtcNow };
        if(!_comments.TryGetValue(quizId, out var list)) { list = new List<CommentViewModel>(); _comments[quizId] = list; }
        list.Add(c);
        var q = _quizzes.FirstOrDefault(x=>x.Id==quizId); if(q!=null) q.Comments = (_comments[quizId]?.Count ?? 0);
        return c;
    }

    public int GetQuizCommentsCount(Guid quizId)
    {
        if(_comments.TryGetValue(quizId, out var list)) return list.Count;
        return _quizzes.FirstOrDefault(q=>q.Id==quizId)?.Comments ?? 0;
    }
    public int GetUserCommentsCount(string userId)
    {
        return _comments.Values.Sum(list => list.Count(c=>c.UserId==userId));
    }

    public int GetUserReactionsCount(string userId)
    {
        return _reactions.Keys.Count(k => k.userId == userId);
    }

    public IEnumerable<Guid> GetReactedQuizIdsByUser(string userId)
    {
        return _reactions.Where(kv => kv.Key.userId == userId).Select(kv => kv.Key.quizId).Distinct().ToList();
    }

    private List<QuizDetailViewModel> Seed()
    {
        var rnd = new Random();
        var list = new List<QuizDetailViewModel>();
        string[] categories = ["Yaşam","Spor","Film","Müzik","Eğlence"];        
        for (int i = 1; i <= 15; i++)
        {
            list.Add(new QuizDetailViewModel
            {
                Id = Guid.NewGuid(),
                Title = $"Örnek Quiz {i}",
                Description = "Açıklama metni",
                Category = categories[rnd.Next(categories.Length)],
                CoverImageUrl = "/img/sample" + ((i % 5)+1) + ".jpg",
                Plays = rnd.Next(10, 1000),
                Comments = rnd.Next(0, 50),
                Reactions = rnd.Next(0, 200),
                IsEditorPick = i % 5 == 0,
                IsTrending = i % 3 == 0,
                AuthorUserName = "kullanici" + i,
                CreatedAt = DateTime.UtcNow.AddDays(-rnd.Next(0,30)),
                Choices = Enumerable.Range(1, 8).Select(x => new QuizChoiceViewModel
                {
                    Id = Guid.NewGuid(),
                    ImageUrl = "/img/sample" + ((x % 5)+1) + ".jpg",
                    Caption = "Seçim " + x,
                    Order = x
                }).ToList(),
                Tags = new []{"etiket","örnek"}
            });
        }
        return list;
    }
}