using Choosr.Domain.Entities;
using Choosr.Infrastructure.Data;
using Choosr.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Choosr.Web.Controllers;

[ApiController]
[Route("report")]
public class ReportController(AppDbContext db, ICaptchaVerifier captcha) : ControllerBase
{
    [HttpPost("quiz/{id:guid}")]
    [EnableRateLimiting("reports")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportQuiz(Guid id, [FromForm] string reason, [FromForm] string? details, [FromForm(Name="cf-turnstile-response")] string? token)
    {
        var quizExists = await db.Quizzes.AsNoTracking().AnyAsync(q=>q.Id==id);
        if(!quizExists) return NotFound();
        reason = (reason ?? string.Empty).Trim(); if(reason.Length == 0) return BadRequest(new{ message = "Neden belirtilmelidir."});
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var ok = await captcha.VerifyAsync(token ?? string.Empty, ip);
        if(!ok) return BadRequest(new{ message = "Doğrulama başarısız."});
        var rep = new ContentReport{
            TargetType = ReportTargetType.Quiz,
            TargetId = id,
            ReporterUserId = User?.Identity?.IsAuthenticated == true ? (User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value) : null,
            ReporterUserName = User?.Identity?.IsAuthenticated == true ? (User.Identity?.Name ?? null) : null,
            ReporterIp = ip,
            Reason = reason,
            Details = string.IsNullOrWhiteSpace(details)? null : details!.Trim(),
            Status = ReportStatus.New,
            CreatedAt = DateTime.UtcNow
        };
        db.ContentReports.Add(rep);
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("comment/{id:guid}")]
    [EnableRateLimiting("reports")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportComment(Guid id, [FromForm] string reason, [FromForm] string? details, [FromForm(Name="cf-turnstile-response")] string? token)
    {
        var c = await db.QuizComments.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id);
        if(c == null) return NotFound();
        reason = (reason ?? string.Empty).Trim(); if(reason.Length == 0) return BadRequest(new{ message = "Neden belirtilmelidir."});
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var ok = await captcha.VerifyAsync(token ?? string.Empty, ip);
        if(!ok) return BadRequest(new{ message = "Doğrulama başarısız."});
        var rep = new ContentReport{
            TargetType = ReportTargetType.Comment,
            TargetId = id,
            ReporterUserId = User?.Identity?.IsAuthenticated == true ? (User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value) : null,
            ReporterUserName = User?.Identity?.IsAuthenticated == true ? (User.Identity?.Name ?? null) : null,
            ReporterIp = ip,
            Reason = reason,
            Details = string.IsNullOrWhiteSpace(details)? null : details!.Trim(),
            Status = ReportStatus.New,
            CreatedAt = DateTime.UtcNow
        };
        db.ContentReports.Add(rep);
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
