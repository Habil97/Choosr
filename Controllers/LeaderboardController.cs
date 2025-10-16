using Choosr.Infrastructure.Data;
using Choosr.Web.Services;
using Choosr.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Choosr.Infrastructure.Services;

namespace Choosr.Web.Controllers;

public class LeaderboardController(AppDbContext db, IQuizService quizzes) : Controller
{
    [HttpGet("Leaderboard/{id}")]
    public IActionResult Index(Guid id, string period = "week", int top = 20, string mode = "creator", string metric = "plays")
    {
        var quiz = quizzes.GetById(id);
        if (quiz == null) return NotFound();
        top = Math.Clamp(top, 5, 100);
        var now = DateTime.UtcNow;
        var cutoff = period?.ToLowerInvariant() == "month" ? now.AddDays(-30) : now.AddDays(-7);
        mode = (mode ?? "creator").ToLowerInvariant();
        metric = (metric ?? "plays").ToLowerInvariant(); // plays | unique

        // Varsayılan: Oluşturucu bazlı sıralama (kullanıcının oluşturduğu quiz’ler başkaları tarafından kaç kez oynandı)
        List<LeaderboardEntry> entries;
        if(mode == "creator")
        {
            // Sadece giriş yapmış kullanıcıların oynadığı oturumlar sayılır (anon hariç). Ayrıca yazarın kendi oynadıkları hariç.
            var baseQuery = from ps in db.PlaySessions
                        join qz in db.Quizzes on ps.QuizId equals qz.Id
                        where ps.QuizId == id
                              && ps.CreatedAt >= cutoff
                              && !string.IsNullOrEmpty(ps.UserName)
                              && ps.UserName != qz.AuthorUserName
                        select new { ps, qz };

            List<(string Author,int Value)> rows;
            if(metric=="unique")
            {
                rows = baseQuery
                    .GroupBy(x => x.qz.AuthorUserName)
                    .Select(g => new { Author = g.Key, Value = g.Select(x=>x.ps.UserName!).Distinct().Count() })
                    .OrderByDescending(x=>x.Value).ThenBy(x=>x.Author)
                    .Take(top)
                    .AsEnumerable()
                    .Select(x=>(x.Author,x.Value)).ToList();
            }
            else
            {
                rows = baseQuery
                    .GroupBy(x => x.qz.AuthorUserName)
                    .Select(g => new { Author = g.Key, Value = g.Count() })
                    .OrderByDescending(x=>x.Value).ThenBy(x=>x.Author)
                    .Take(top)
                    .AsEnumerable()
                    .Select(x=>(x.Author,x.Value)).ToList();
            }

            // Map to entries and attach avatar/display info if available
            var profileSvc = HttpContext.RequestServices.GetService<IUserProfileService>();
            entries = rows.Select(r => {
                var p = profileSvc?.GetByUserName(r.Author);
                return new LeaderboardEntry{
                    UserName = r.Author,
                    DisplayName = string.IsNullOrWhiteSpace(p?.DisplayName) ? r.Author : p!.DisplayName!,
                    AvatarUrl = p?.AvatarUrl ?? "/img/demo-avatar.png",
                    Plays = r.Value
                };
            }).ToList();
        }
        else // mode == "player" (eski davranışa geri dönüş opsiyonu)
        {
            var query = db.PlaySessions
                .Where(p => p.QuizId == id && p.CreatedAt >= cutoff && p.UserName != null && p.UserName != "");

            List<(string UserName,int Value)> rows;
            if(metric=="unique")
            {
                rows = query
                    .GroupBy(p => p.UserName!)
                    .Select(g => new { UserName = g.Key, Value = 1 }) // unique players = 1 per user
                    .OrderByDescending(x => x.Value)
                    .ThenBy(x => x.UserName)
                    .Take(top)
                    .AsEnumerable()
                    .Select(x=>(x.UserName,x.Value)).ToList();
            }
            else
            {
                rows = query
                    .GroupBy(p => p.UserName!)
                    .Select(g => new { UserName = g.Key, Value = g.Count() })
                    .OrderByDescending(x => x.Value)
                    .ThenBy(x => x.UserName)
                    .Take(top)
                    .AsEnumerable()
                    .Select(x=>(x.UserName,x.Value)).ToList();
            }
            var profileSvc = HttpContext.RequestServices.GetService<IUserProfileService>();
            entries = rows.Select(r => {
                var p = profileSvc?.GetByUserName(r.UserName);
                return new LeaderboardEntry{
                    UserName = r.UserName,
                    DisplayName = string.IsNullOrWhiteSpace(p?.DisplayName) ? r.UserName : p!.DisplayName!,
                    AvatarUrl = p?.AvatarUrl ?? "/img/demo-avatar.png",
                    Plays = r.Value
                };
            }).ToList();
        }

        var model = new LeaderboardViewModel
        {
            QuizId = id,
            QuizTitle = quiz.Title,
            Period = period?.ToLowerInvariant() == "month" ? "Aylık" : "Haftalık",
            Mode = mode == "creator" ? "Oluşturucu" : "Oyuncu",
            Metric = metric == "unique" ? "Tekil Oyuncu" : "Toplam Oyun",
            Top = top,
            Entries = entries
        };
        return View(model);
    }

    // Global oluşturucu liderliği: tüm kamuya açık quiz'lerin oynanma sayıları, oynayan kişi giriş yapmış olmalı ve yazarın kendi oyunları hariç
    [HttpGet("Leaderboard/Creators")]
    public IActionResult Creators(string period = "week", int top = 20, string? category = null, string metric = "plays")
    {
        top = Math.Clamp(top, 5, 100);
        var now = DateTime.UtcNow;
        var cutoff = period?.ToLowerInvariant() == "month" ? now.AddDays(-30) : now.AddDays(-7);

        var baseQuery = from ps in db.PlaySessions
                        join qz in db.Quizzes on ps.QuizId equals qz.Id
                        where qz.IsPublic
                              && ps.CreatedAt >= cutoff
                              && !string.IsNullOrEmpty(ps.UserName)
                              && ps.UserName != qz.AuthorUserName
                        select new { ps, qz };
        if(!string.IsNullOrWhiteSpace(category))
        {
            baseQuery = baseQuery.Where(x=>x.qz.Category == category);
        }

        metric = (metric ?? "plays").ToLowerInvariant();
        List<(string Author,int Value)> rows;
        if(metric=="unique")
        {
            rows = baseQuery
                .GroupBy(x => x.qz.AuthorUserName)
                .Select(g => new { Author = g.Key, Value = g.Select(x=>x.ps.UserName!).Distinct().Count() })
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Author)
                .Take(top)
                .AsEnumerable()
                .Select(x=>(x.Author,x.Value)).ToList();
        }
        else
        {
            rows = baseQuery
                .GroupBy(x => x.qz.AuthorUserName)
                .Select(g => new { Author = g.Key, Value = g.Count() })
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Author)
                .Take(top)
                .AsEnumerable()
                .Select(x=>(x.Author,x.Value)).ToList();
        }

        var profileSvc = HttpContext.RequestServices.GetService<IUserProfileService>();
        var entries = rows.Select(r => {
            var p = profileSvc?.GetByUserName(r.Author);
            return new LeaderboardEntry{
                UserName = r.Author,
                DisplayName = string.IsNullOrWhiteSpace(p?.DisplayName) ? r.Author : p!.DisplayName!,
                AvatarUrl = p?.AvatarUrl ?? "/img/demo-avatar.png",
                Plays = r.Value
            };
        }).ToList();

        // QuizId yok; başlık sabit
        var vm = new LeaderboardViewModel{
            QuizId = Guid.Empty,
            QuizTitle = "Global Oluşturucu Liderliği",
            Period = period?.ToLowerInvariant() == "month" ? "Aylık" : "Haftalık",
            Mode = "Oluşturucu",
            Metric = metric == "unique" ? "Tekil Oyuncu" : "Toplam Oyun",
            Category = category,
            Categories = quizzes.GetCategories(),
            Top = top,
            Entries = entries
        };
        return View("Creators", vm);
    }
}
