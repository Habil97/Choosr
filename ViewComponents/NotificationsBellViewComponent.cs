using Choosr.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Choosr.Web.ViewComponents;

public class NotificationsBellViewComponent(AppDbContext db, IHttpContextAccessor http, IMemoryCache cache) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Content(string.Empty);
        }

        var userName = user!.Identity!.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Content(string.Empty);
        }

        var cacheKey = $"notif-unread:{userName}";
        if (!cache.TryGetValue(cacheKey, out int unread))
        {
            unread = await db.Notifications.AsNoTracking()
                .Where(n => n.UserName == userName && n.ReadAt == null)
                .CountAsync();
            cache.Set(cacheKey, unread, TimeSpan.FromSeconds(15));
        }

        return View(unread);
    }
}
