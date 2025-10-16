using Choosr.Domain.Entities;
using Choosr.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choosr.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminReportsController(AppDbContext db, Choosr.Web.Services.IReportNotificationService notifier, Choosr.Web.Services.INotificationService notifications) : Controller
{
    public async Task<IActionResult> Index(string? status = null, string? q = null)
    {
        var query = db.ContentReports.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReportStatus>(status, true, out var st))
        {
            query = query.Where(r => r.Status == st);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var qLower = q.ToLower();
            query = query.Where(r =>
                (r.Reason != null && r.Reason.ToLower().Contains(qLower)) ||
                (r.Details != null && r.Details.ToLower().Contains(qLower)) ||
                (r.ReporterUserName != null && r.ReporterUserName.ToLower().Contains(qLower))
            );
        }
        // Status counts for pills (based on the same base set or all?) -> all counts for quick overview
        var all = db.ContentReports.AsNoTracking();
        var counts = await all.GroupBy(r => r.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(ReportStatus.New)] = counts.FirstOrDefault(x => x.Status == ReportStatus.New)?.Count ?? 0,
            [nameof(ReportStatus.InReview)] = counts.FirstOrDefault(x => x.Status == ReportStatus.InReview)?.Count ?? 0,
            [nameof(ReportStatus.Resolved)] = counts.FirstOrDefault(x => x.Status == ReportStatus.Resolved)?.Count ?? 0,
            [nameof(ReportStatus.Blocked)] = counts.FirstOrDefault(x => x.Status == ReportStatus.Blocked)?.Count ?? 0,
        };
        var list = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(200)
            .ToListAsync();

        // Target URL'leri oluştur (yorumlar için parent quiz linki)
        var items = new List<AdminReportItemViewModel>(list.Count);
        var commentIds = list.Where(r => r.TargetType == ReportTargetType.Comment).Select(r => r.TargetId).Distinct().ToList();
        var commentQuizMap = await db.QuizComments.AsNoTracking()
            .Where(c => commentIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.QuizId);
        foreach (var r in list)
        {
            string? url = null;
            if (r.TargetType == ReportTargetType.Quiz)
            {
                url = Url.Action("Detail", "Quiz", new { id = r.TargetId });
            }
            else if (r.TargetType == ReportTargetType.Comment && commentQuizMap.TryGetValue(r.TargetId, out var qid))
            {
                url = Url.Action("Detail", "Quiz", new { id = qid });
            }
            items.Add(new AdminReportItemViewModel { Report = r, TargetUrl = url });
        }

        ViewBag.Filter = status;
        ViewBag.Query = q;
        ViewBag.StatusCounts = dict;
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, ReportStatus status, string? returnStatus, string? notes)
    {
        var r = await db.ContentReports.FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();
        var who = User?.Identity?.Name ?? "admin";
        if (!string.IsNullOrWhiteSpace(notes)) r.ModeratorNotes = notes;
        if (status == ReportStatus.InReview && r.Status != ReportStatus.InReview)
        {
            r.InReviewAt = DateTime.UtcNow;
            r.InReviewBy = who;
        }
        if (status == ReportStatus.Resolved || status == ReportStatus.Blocked)
        {
            r.ResolvedAt = DateTime.UtcNow;
            r.ResolvedBy = who;
        }
        r.Status = status;
        await db.SaveChangesAsync();
        // Fire-and-forget email notification (no blocking failures)
        _ = notifier.NotifyStatusChangedAsync(r, who);
        // In-app notifications to reporter and (if applicable) content owner
        try
        {
            if(!string.IsNullOrWhiteSpace(r.ReporterUserName))
            {
                var link = itemLink(r); // local function to build link
                await notifications.CreateAsync(r.ReporterUserName!, $"Rapor durumunuz güncellendi: {r.Status}", r.ModeratorNotes, link);
            }
            var owner = await resolveContentOwnerAsync(r);
            if(!string.IsNullOrWhiteSpace(owner))
            {
                var link = itemLink(r);
                await notifications.CreateAsync(owner!, $"İçeriğiniz raporlandı / güncellendi: {r.Status}", r.ModeratorNotes, link);
            }
        }
        catch { /* swallow */ }
        TempData["AdminReports.Success"] = $"Rapor güncellendi: {status}";
        return RedirectToAction(nameof(Index), new { status = returnStatus });
    }

    private string? itemLink(ContentReport r)
    {
        if (r.TargetType == ReportTargetType.Quiz)
            return Url.Action("Detail", "Quiz", new { id = r.TargetId });
        if (r.TargetType == ReportTargetType.Comment)
        {
            var qid = db.QuizComments.AsNoTracking().Where(c => c.Id == r.TargetId).Select(c => c.QuizId).FirstOrDefault();
            return qid != Guid.Empty ? Url.Action("Detail", "Quiz", new { id = qid }) : null;
        }
        return null;
    }

    private async Task<string?> resolveContentOwnerAsync(ContentReport r)
    {
        if(r.TargetType == ReportTargetType.Quiz)
        {
            var owner = await db.Quizzes.AsNoTracking().Where(q => q.Id == r.TargetId).Select(q => q.AuthorUserName).FirstOrDefaultAsync();
            return owner;
        }
        if(r.TargetType == ReportTargetType.Comment)
        {
            var owner = await db.QuizComments.AsNoTracking().Where(c => c.Id == r.TargetId).Select(c => c.UserName).FirstOrDefaultAsync();
            return owner;
        }
        return null;
    }
}

public class AdminReportItemViewModel
{
    public required ContentReport Report { get; set; }
    public string? TargetUrl { get; set; }
}
