using Choosr.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Choosr.Web.ViewModels;

namespace Choosr.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(AppDbContext db, Choosr.Web.Services.ITagStatsService tagStats) : Controller
{
    public async Task<IActionResult> Index()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var from = todayUtc.AddDays(-13); // last 14 days inclusive

        // Daily new quizzes (public or all? Use all creations for admin insight)
        var dailyQuizzes = await db.Quizzes.AsNoTracking()
            .Where(q => q.CreatedAt >= from)
            .GroupBy(q => new { q.CreatedAt.Year, q.CreatedAt.Month, q.CreatedAt.Day })
            .Select(g => new { Date = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day), Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();

        // Daily active users (based on PlaySessions by day)
        var dailyActiveUsers = await db.PlaySessions.AsNoTracking()
            .Where(p => p.CreatedAt >= from && !string.IsNullOrEmpty(p.UserName))
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month, p.CreatedAt.Day })
            .Select(g => new { Date = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day), Count = g.Select(x => x.UserName!).Distinct().Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();

        // Ensure we have all dates filled (0 for missing days)
        var days = Enumerable.Range(0, 14).Select(d => from.AddDays(d)).ToList();
        var dq = days.Select(d => new AdminDailyPoint { Date = d, Count = dailyQuizzes.FirstOrDefault(x => x.Date == d)?.Count ?? 0 }).ToList();
        var dau = days.Select(d => new AdminDailyPoint { Date = d, Count = dailyActiveUsers.FirstOrDefault(x => x.Date == d)?.Count ?? 0 }).ToList();

        // Top tags (public quizzes)
        var topTags = tagStats.GetTopTags(20).Select(t => new AdminTopTag { Name = t.Name, Count = t.Count }).ToList();

        var vm = new AdminDashboardViewModel
        {
            DailyNewQuizzes = dq,
            DailyActiveUsers = dau,
            TopTags = topTags
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ModerationQueue()
    {
        var pending = await db.Quizzes.AsNoTracking()
            .Where(q => q.Moderation == Choosr.Domain.Entities.ModerationStatus.Pending)
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new { q.Id, Title = (string)q.Title, q.AuthorUserName, q.CreatedAt, q.Category, q.ModerationNotes })
            .Take(200)
            .ToListAsync();
        return View(pending);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id)
    {
        var q = await db.Quizzes.FirstOrDefaultAsync(x => x.Id == id);
        if(q == null) return NotFound();
        q.Moderation = Choosr.Domain.Entities.ModerationStatus.Approved;
        q.ModerationNotes = null;
        await db.SaveChangesAsync();
        TempData["Msg"] = "Onaylandı";
        return RedirectToAction(nameof(ModerationQueue));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, [FromForm] string? reason)
    {
        var q = await db.Quizzes.FirstOrDefaultAsync(x => x.Id == id);
        if(q == null) return NotFound();
        q.Moderation = Choosr.Domain.Entities.ModerationStatus.Rejected;
        q.ModerationNotes = string.IsNullOrWhiteSpace(reason) ? "Admin tarafından reddedildi" : reason;
        await db.SaveChangesAsync();
        TempData["Msg"] = "Reddedildi";
        return RedirectToAction(nameof(ModerationQueue));
    }
}
 
