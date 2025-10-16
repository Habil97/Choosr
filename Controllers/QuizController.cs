using Choosr.Web.Services;
using Choosr.Infrastructure.Services;
using Choosr.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace Choosr.Web.Controllers;

public class QuizController(IQuizService quizService, IUserProfileService profiles, ICaptchaVerifier captcha) : Controller
{
    [OutputCache(PolicyName = "quiz-list")]
    public IActionResult Index(string? category, string? q, string? tag, string? sort, int page = 1, int pageSize = 12)
    {
        var (items,total) = quizService.Search(category, q, tag, sort, page, pageSize);
        var vm = new PagedResultViewModel<QuizCardViewModel>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Category = category,
            Q = q
        };
        ViewBag.Categories = quizService.GetCategories();
        ViewBag.Tags = quizService.GetAllTags();
        ViewBag.SelectedTag = tag;
        ViewBag.Category = category;
        ViewBag.Q = q;
        ViewBag.Sort = sort;
        return View(vm);
    }

    [HttpGet]
    [OutputCache(PolicyName = "partials-short")]
    public IActionResult ListJson(string? category, string? q, string? tag, string? sort, int page = 1, int size = 12)
    {
        if (page < 1) page = 1;
        if (size <= 0) size = 12;
        if (size > 48) size = 48; // hard cap to avoid excessive payloads

        var (items, total) = quizService.Search(category, q, tag, sort, page, size);
        var hasNext = (page * size) < total;
        var nextUrl = hasNext ? Url.Action("ListJson", "Quiz", new { category, q, tag, sort, page = page + 1, size }) : null;
        var prevUrl = page > 1 ? Url.Action("ListJson", "Quiz", new { category, q, tag, sort, page = page - 1, size }) : null;

        return Ok(new
        {
            items,
            page,
            size,
            total,
            hasNext,
            nextUrl,
            prevUrl
        });
    }

    [HttpGet]
    [OutputCache(PolicyName = "partials-short")]
    public IActionResult SearchCards(string? category, string? q, string? tag, string? sort, int skip = 0, int take = 12)
    {
        if (take <= 0) take = 12;
        if (skip < 0) skip = 0;
        // Use page-based search; assume skip is multiple of take
        var page = (skip / take) + 1;
        var (items, total) = quizService.Search(category, q, tag, sort, page, take);
        return PartialView("~/Views/Shared/Partials/_QuizCardHomeList.cshtml", items);
    }

    [HttpGet]
    [OutputCache(PolicyName = "partials-short")]
    public IActionResult LatestItems(int skip = 0, int take = 6)
    {
        if (take <= 0) take = 6;
        if (skip < 0) skip = 0;
        // Use GetLatest for simplicity, then skip client-side portion
        var list = quizService.GetLatest(skip + take).Skip(skip).Take(take).ToList();
        var items = list.Select(q => new { id = q.Id, title = q.Title, coverImageUrl = q.CoverImageUrl });
        return Ok(new { items });
    }

    [HttpGet]
    [OutputCache(PolicyName = "partials-short")]
    public IActionResult LatestCards(int skip = 0, int take = 6)
    {
        if (take <= 0) take = 6;
        if (skip < 0) skip = 0;
        var list = quizService.GetLatest(skip + take).Skip(skip).Take(take).ToList();
        return PartialView("~/Views/Shared/Partials/_QuizCardHomeList.cshtml", list);
    }

    [HttpGet]
    [OutputCache(PolicyName = "partials-short")]
    public IActionResult SimilarCards(Guid id, int skip = 0, int take = 6)
    {
        if (take <= 0) take = 6;
        if (skip < 0) skip = 0;
        var list = quizService.GetSimilar(id, skip + take).Skip(skip).Take(take).ToList();
        return PartialView("~/Views/Shared/Partials/_QuizCardHomeList.cshtml", list);
    }

    [HttpGet]
    [OutputCache(PolicyName = "partials-short")]
    public IActionResult RelatedTags([FromQuery] string? tag, int take = 14)
    {
        if(string.IsNullOrWhiteSpace(tag)) return Ok(new { items = Array.Empty<object>() });
        try
        {
            var svc = HttpContext.RequestServices.GetService<Choosr.Web.Services.ITagStatsService>();
            var co = svc?.GetCoOccurrences(tag!, take);
            var items = (co?.Others ?? new List<(string Other,int Count)>())
                .Select(o => new { name = o.Other, count = o.Count })
                .ToList();
            return Ok(new { items });
        }
        catch
        {
            return Ok(new { items = Array.Empty<object>() });
        }
    }

    public IActionResult Detail(Guid id)
    {
        var quiz = quizService.GetById(id);
        if(quiz == null) return NotFound();
        // If moderation not approved, block public view unless admin
        try
        {
            var db = HttpContext.RequestServices.GetRequiredService<Choosr.Infrastructure.Data.AppDbContext>();
            var row = db.Quizzes.AsNoTracking().FirstOrDefault(q=>q.Id==id);
            if(row != null && row.Moderation != Choosr.Domain.Entities.ModerationStatus.Approved)
            {
                if(!(User?.IsInRole("Admin") ?? false)) return NotFound();
            }
        }catch{}
        // ETag hesapla
        try
        {
            // Temel alanları birleştir
            int choiceCount = 0;
            try { if (quiz.Choices != null) choiceCount = System.Linq.Enumerable.Count(quiz.Choices); } catch { }
            var raw = $"{quiz.Id}|{quiz.Title}|{quiz.CoverImageUrl}|{quiz.Plays}|{quiz.Reactions}|{quiz.Comments}|{choiceCount}";
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
            var etag = Convert.ToBase64String(hashBytes).TrimEnd('=');
            var strongTag = '"' + "quiz-" + etag + '"';
            var ifNoneMatch = Request.Headers["If-None-Match"].FirstOrDefault();
            if(!string.IsNullOrEmpty(ifNoneMatch) && string.Equals(ifNoneMatch, strongTag, StringComparison.Ordinal))
            {
                return StatusCode(304);
            }
            Response.Headers["ETag"] = strongTag;
            // İsteğe bağlı kısa süreli cache kontrolü (değişirse tarayıcı tekrar çeker)
            Response.Headers["Cache-Control"] = "public,max-age=30"; // plays/reaksiyon değişimlerine çok duyarlı olduğu için kısa
            // Last-Modified (CreatedAt alanı yoksa şimdi) - ileride UpdatedAt eklenirse değiştirilir
            try
            {
                var createdAtProp = quiz.GetType().GetProperty("CreatedAt");
                if(createdAtProp != null)
                {
                    var val = createdAtProp.GetValue(quiz) as DateTime?;
                    if(val.HasValue)
                    {
                        Response.Headers["Last-Modified"] = val.Value.ToUniversalTime().ToString("R");
                    }
                }
            } catch { }
        }
        catch { }
        ViewBag.Similar = quizService.GetSimilar(id,24);
        return View(quiz);
    }

    [HttpGet]
    public IActionResult Reactions(Guid id)
    {
        var (like,love,haha,wow,sad,angry) = quizService.GetReactionBreakdown(id);
        string? my = null;
        if(User.Identity?.IsAuthenticated ?? false)
        {
            var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            if(!string.IsNullOrWhiteSpace(userId)) my = quizService.GetUserReaction(id, userId!);
        }
        return Ok(new { like,love,haha,wow,sad,angry, my });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("reactions")]
    public IActionResult React(Guid id, [FromForm] string type)
    {
        if(!User.Identity?.IsAuthenticated ?? true) return Unauthorized();
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ?? string.Empty;
        if(string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        quizService.SetReaction(id, userId, type);
        // Profilde reaksiyon kaydet
        if(User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity!.Name))
        {
            try{ var prof=HttpContext.RequestServices.GetService<Choosr.Infrastructure.Services.IUserProfileService>(); prof?.AddReaction(User.Identity!.Name!, id);}catch{}
        }
        var (like,love,haha,wow,sad,angry) = quizService.GetReactionBreakdown(id);
        return Ok(new { like,love,haha,wow,sad,angry });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("reactions")]
    public IActionResult Unreact(Guid id)
    {
        if(!User.Identity?.IsAuthenticated ?? true) return Unauthorized();
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ?? string.Empty;
        if(string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        quizService.ClearReaction(id, userId);
        if(User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity!.Name))
        {
            try{ var prof=HttpContext.RequestServices.GetService<Choosr.Infrastructure.Services.IUserProfileService>(); prof?.RemoveReaction(User.Identity!.Name!, id);}catch{}
        }
        var (like,love,haha,wow,sad,angry) = quizService.GetReactionBreakdown(id);
        return Ok(new { like,love,haha,wow,sad,angry });
    }

    [HttpGet]
    public IActionResult Comments(Guid id, int skip = 0, int take = 50)
    {
        var list = quizService.GetComments(id, take, skip);
        return Ok(list);
    }

    [HttpPost]
    [EnableRateLimiting("comments")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Comment(Guid id, [FromForm] string text, [FromForm(Name="cf-turnstile-response")] string? captchaToken)
    {
        if(!User.Identity?.IsAuthenticated ?? true) return Unauthorized();
        text = (text ?? string.Empty).Trim();
        if(string.IsNullOrWhiteSpace(text)) return BadRequest(new{ message = "Boş yorum gönderilemez."});
        // Basic profanity filter (server-side)
        try
        {
            var filter = HttpContext.RequestServices.GetService<Choosr.Web.Services.IBadWordsFilter>();
            if(filter != null && filter.ContainsBadWords(text))
            {
                return BadRequest(new { message = "Uygunsuz içerik tespit edildi. Lütfen yorumunuzu düzenleyin." });
            }
        }
        catch{}
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var ok = await captcha.VerifyAsync(captchaToken ?? string.Empty, remoteIp);
        if(!ok) return BadRequest(new { message = "Doğrulama başarısız. Lütfen tekrar deneyin." });
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ?? string.Empty;
        var userName = User.Identity?.Name ?? (User.FindFirst("preferred_username")?.Value ?? "kullanici");
        if(string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var c = quizService.AddComment(id, userId, userName!, text);
        // Kullanıcının profilindeki yorum sayacını artır
        var p = profiles.GetByUserName(userName!);
        if(p == null)
        {
            p = new Choosr.Domain.Models.UserProfile{ UserName = userName!, DisplayName = userName!, AvatarUrl = "/img/demo-avatar.png" };
            try { profiles.Create(p); } catch {}
        }
    // Profil sayaçlarını DB verisine senkronla
    var qCount = quizService.GetQuizCommentsCount(id);
    var uCount = quizService.GetUserCommentsCount(userId);
    p.CommentCount = uCount; profiles.Update(p);
        return Ok(new { comment = c, quizComments = qCount, userComments = uCount });
    }
}