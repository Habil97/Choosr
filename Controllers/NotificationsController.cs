using Choosr.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choosr.Web.Controllers;

[Authorize]
public class NotificationsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var user = User?.Identity?.Name;
        if(string.IsNullOrWhiteSpace(user)) return RedirectToAction("Login","Account");
        var list = await db.Notifications.AsNoTracking()
            .Where(n => n.UserName == user)
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)
            .ToListAsync();
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var user = User?.Identity?.Name; if(string.IsNullOrWhiteSpace(user)) return RedirectToAction("Login","Account");
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserName == user);
        if(n!=null && n.ReadAt==null)
        {
            n.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var user = User?.Identity?.Name; if(string.IsNullOrWhiteSpace(user)) return RedirectToAction("Login","Account");
        var items = await db.Notifications.Where(n => n.UserName == user && n.ReadAt == null).ToListAsync();
        var now = DateTime.UtcNow;
        foreach(var i in items) i.ReadAt = now;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
