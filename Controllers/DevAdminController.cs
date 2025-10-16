using Choosr.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;

namespace Choosr.Web.Controllers;

// Simple dev-only utilities; secure/disable in production
[ApiController]
[Route("dev-admin")]
public class DevAdminController(AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost("backfill-image-dimensions")]
    public async Task<IActionResult> BackfillImageDimensions()
    {
        if(!env.IsDevelopment()) return NotFound();

        var root = Directory.GetCurrentDirectory();
        var wwwroot = Path.Combine(root, "wwwroot");
        int updated = 0, skipped = 0;

        // Backfill for cover images
        var quizzes = await db.Quizzes.ToListAsync();
        foreach(var q in quizzes)
        {
            if(q.CoverImageWidth.HasValue && q.CoverImageHeight.HasValue) { skipped++; continue; }
            var (w,h) = ProbeDimensions(q.CoverImageUrl, wwwroot);
            if(w.HasValue && h.HasValue){ q.CoverImageWidth = w; q.CoverImageHeight = h; updated++; }
        }

        // Backfill for choice images
        var choices = await db.QuizChoices.ToListAsync();
        foreach(var c in choices)
        {
            if(c.ImageWidth.HasValue && c.ImageHeight.HasValue) { skipped++; continue; }
            var (w,h) = ProbeDimensions(c.ImageUrl, wwwroot);
            if(w.HasValue && h.HasValue){ c.ImageWidth = w; c.ImageHeight = h; updated++; }
        }

        await db.SaveChangesAsync();
        return Ok(new { updated, skipped });
    }

    private static (int?, int?) ProbeDimensions(string? url, string wwwroot)
    {
        if(string.IsNullOrWhiteSpace(url)) return (null,null);
        try
        {
            if(url.StartsWith("/"))
            {
                var path = Path.Combine(wwwroot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if(System.IO.File.Exists(path))
                {
                    using var img = Image.Load(path);
                    return (img.Width, img.Height);
                }
            }
            else if(url.Contains("img.youtube.com/vi"))
            {
                // hqdefault is 480x360 (best effort without network call)
                return (480, 360);
            }
        }
        catch { }
        return (null,null);
    }
}
