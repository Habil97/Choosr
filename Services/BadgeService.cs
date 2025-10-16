using Choosr.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Choosr.Web.Services;

public record UserBadge(string Key, string Label, string Icon, int Level);

public interface IBadgeService
{
    Task<IReadOnlyList<UserBadge>> GetBadgesAsync(string userName, CancellationToken ct = default);
}

public class EfBadgeService : IBadgeService
{
    private readonly AppDbContext _db;
    public EfBadgeService(AppDbContext db){ _db=db; }

    // Thresholds can be tuned later
    private static readonly int[] CreatedThresholds = new[]{1,5,10,25,50,100};
    private static readonly int[] PlaysThresholds = new[]{100,1000,5000,10000,50000,100000};
    private static readonly int[] LikesThresholds = new[]{10,50,100,250,500,1000};

    public async Task<IReadOnlyList<UserBadge>> GetBadgesAsync(string userName, CancellationToken ct = default)
    {
        if(string.IsNullOrWhiteSpace(userName)) return Array.Empty<UserBadge>();
        userName = userName.Trim();
        // Created quizzes (public only)
        var createdCount = await _db.Quizzes.AsNoTracking().CountAsync(q=>q.AuthorUserName==userName && q.IsPublic, ct);
        // Total plays on user's public quizzes
        var totalPlays = await _db.Quizzes.AsNoTracking().Where(q=>q.AuthorUserName==userName && q.IsPublic).SumAsync(q=> (int?)q.Plays) ?? 0;
        // Total likes on user's quizzes
        var likeCount = await _db.QuizReactions.AsNoTracking()
            .Where(r=> r.Type=="like")
            .Join(_db.Quizzes.AsNoTracking().Where(q=>q.AuthorUserName==userName && q.IsPublic), r=>r.QuizId, q=>q.Id, (r,q)=>r)
            .CountAsync(ct);

        var list = new List<UserBadge>();
        int CreatedLevel(int v){ int lvl=0; foreach(var t in CreatedThresholds){ if(v>=t) lvl++; } return lvl; }
        int PlaysLevel(int v){ int lvl=0; foreach(var t in PlaysThresholds){ if(v>=t) lvl++; } return lvl; }
        int LikesLevel(int v){ int lvl=0; foreach(var t in LikesThresholds){ if(v>=t) lvl++; } return lvl; }

        var cLvl = CreatedLevel(createdCount);
        if(cLvl>0) list.Add(new UserBadge("creator", $"Quiz Ustası Lv{cLvl}", "🧩", cLvl));
        var pLvl = PlaysLevel(totalPlays);
        if(pLvl>0) list.Add(new UserBadge("popular", $"Popüler Lv{pLvl}", "🔥", pLvl));
        var lLvl = LikesLevel(likeCount);
        if(lLvl>0) list.Add(new UserBadge("liked", $"Beğeni Lv{lLvl}", "❤", lLvl));
        return list;
    }
}
