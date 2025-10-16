using Choosr.Domain.Entities;
using Choosr.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Choosr.Web.Services;

public class EfNotificationService(AppDbContext db) : INotificationService
{
    public async Task CreateAsync(string userName, string title, string? body = null, string? linkUrl = null, CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(title)) return;
        var n = new Notification{ UserName = userName, Title = title, Body = body, LinkUrl = linkUrl, CreatedAt = DateTime.UtcNow };
        await db.Notifications.AddAsync(n, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
