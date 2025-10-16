using Choosr.Web.Services;
using Choosr.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Choosr.Web.Controllers;

[Authorize]
public class DraftsController(IDraftService drafts, IQuizService quizzes, ITagSuggestService tagSuggest, Choosr.Web.Services.IImageProcessingQueue imgQueue, Choosr.Web.Services.IFileScanner scanner, ITagSelectionService tagSel) : Controller
{
    [HttpGet]
    public IActionResult Get(Guid id)
    {
        if(User?.Identity?.IsAuthenticated != true) return Unauthorized();
        var user = User.Identity!.Name!;
        var d = drafts.Get(user, id);
        if(d == null) return NotFound();
        return Ok(d);
    }

    [HttpGet]
    public IActionResult Index()
    {
        var user = User.Identity!.Name!;
        var list = drafts.List(user);
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(Guid id)
    {
        var user = User.Identity!.Name!;
        drafts.Delete(user, id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Consumes("application/json")]
    public IActionResult Autosave([FromBody] DraftViewModel draft)
    {
        if(User?.Identity?.IsAuthenticated != true) return Unauthorized();
        var user = User.Identity!.Name!;
        var saved = drafts.Upsert(user, draft);
        return Ok(new { ok=true, id = saved.Id, updatedAt = saved.UpdatedAt });
    }

        [HttpGet]
        public IActionResult Revisions(Guid id)
        {
            var user = User?.Identity?.Name ?? string.Empty; if(string.IsNullOrWhiteSpace(user)) return Unauthorized();
            var items = drafts.ListRevisions(user, id, 100).Select(x=> new { id = x.id, createdAt = x.createdAt, title = x.title });
            return Json(items);
        }

        [HttpGet]
        public IActionResult Revision(Guid id, Guid revId)
        {
            var user = User?.Identity?.Name ?? string.Empty; if(string.IsNullOrWhiteSpace(user)) return Unauthorized();
            var rev = drafts.GetRevision(user, id, revId);
            if(rev==null) return NotFound();
            return Json(new{
                rev.Id,
                rev.DraftId,
                rev.UserName,
                rev.CreatedAt,
                rev.Title,
                rev.Description,
                rev.Category,
                rev.Visibility,
                rev.IsAnonymous,
                rev.Tags,
                rev.CoverImageUrl,
                rev.CoverImageWidth,
                rev.CoverImageHeight,
                rev.ChoicesJson
            });
        }

    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadChoices(List<IFormFile> files)
    {
        if(User?.Identity?.IsAuthenticated != true) return Unauthorized();
        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsDir);
        var results = new List<DraftChoiceViewModel>();
        foreach(var f in files)
        {
            if(f.Length == 0) continue;
            if(f.Length > 10_000_000) continue;
            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
            var allowed = new[]{".png",".jpg",".jpeg",".webp",".gif"};
            if(!allowed.Contains(ext)) ext = ".jpg";
            var name = $"{Guid.NewGuid()}{ext}";
            var path = Path.Combine(uploadsDir, name);
            await using(var ms = new MemoryStream())
            {
                await f.CopyToAsync(ms);
                ms.Position = 0;
                var safe = await scanner.IsSafeAsync(ms, f.FileName);
                if(!safe) continue;
                ms.Position = 0;
                var tcs = new TaskCompletionSource<(bool ok, int? w, int? h)>(TaskCreationOptions.RunContinuationsAsynchronously);
                var req = new Choosr.Web.Services.ImageProcessRequest(ms, f.FileName, path, (img) => {
                    var w0 = img.Width; var h0 = img.Height;
                    var side = Math.Min(w0, h0);
                    var x = (w0 - side) / 2;
                    var y = (h0 - side) / 2;
                    var cropRect = new Rectangle(x, y, side, side);
                    SixLabors.ImageSharp.Formats.IImageEncoder enc = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 85 };
                    return (enc, (SixLabors.ImageSharp.Processing.IImageProcessingContext op) => op.Crop(cropRect).Resize(720, 720));
                }, tcs);
                var (ok, w, h) = await imgQueue.EnqueueAsync(req);
                if(!ok) continue;
                results.Add(new DraftChoiceViewModel{ ImageUrl = "/uploads/" + name, ImageWidth = w, ImageHeight = h });
            }
        }
        return Ok(results);
    }

    [HttpGet]
    public IActionResult SuggestTags([FromQuery] string title, [FromQuery] string? description)
    {
        if(User?.Identity?.IsAuthenticated != true) return Unauthorized();
        var corpus = quizzes.GetAllTags();
        var suggestions = tagSuggest.Suggest(corpus, title, description, 6);
        return Ok(suggestions);
    }

    [HttpPost]
    [Consumes("application/json")]
    public IActionResult RecordSelectedTags([FromBody] string[] tags)
    {
        if(User?.Identity?.IsAuthenticated != true) return Unauthorized();
        tagSel.Increment(tags ?? Array.Empty<string>());
        return Ok(new { ok=true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Publish(Guid id)
    {
        if(User?.Identity?.IsAuthenticated != true) return Unauthorized();
        var user = User.Identity!.Name!;
        var d = drafts.Get(user, id);
        if(d == null) return NotFound();
        // Draft -> QuizDetailViewModel map
        var q = new Choosr.Web.ViewModels.QuizDetailViewModel{
            Id = Guid.NewGuid(),
            Title = d.Title ?? string.Empty,
            Description = d.Description ?? string.Empty,
            Category = string.IsNullOrWhiteSpace(d.Category)? "Genel" : d.Category!,
            CoverImageUrl = d.CoverImageUrl ?? string.Empty,
            CoverImageWidth = d.CoverImageWidth,
            CoverImageHeight = d.CoverImageHeight,
            AuthorUserName = user,
            CreatedAt = DateTime.UtcNow,
            IsPublic = true,
            Tags = d.Tags ?? Array.Empty<string>(),
            Choices = (d.Choices ?? new List<DraftChoiceViewModel>()).Select((c,i)=> new Choosr.Web.ViewModels.QuizChoiceViewModel{
                Id = Guid.NewGuid(),
                ImageUrl = c.ImageUrl,
                ImageWidth = c.ImageWidth,
                ImageHeight = c.ImageHeight,
                YoutubeUrl = c.YoutubeUrl,
                Caption = c.Caption,
                Order = c.Order>0? c.Order : i+1
            }).ToList()
        };
        // Moderation: basic bad-words check for title/description to set initial status
        try
        {
            var filter = HttpContext.RequestServices.GetService<Choosr.Web.Services.IBadWordsFilter>();
            if(filter != null)
            {
                var hasBad = filter.ContainsBadWords(q.Title ?? string.Empty) || filter.ContainsBadWords(q.Description ?? string.Empty);
                if(hasBad)
                {
                    // Pass a signal via HttpContext.Items; the service will persist Moderation when mapping to entity
                    HttpContext.Items["ModerationFlag"] = "pending";
                }
            }
        }catch{}
        var saved = quizzes.Add(q);
        // Record tag selections at publish time as well (signals author intent)
    if(q.Tags != null && q.Tags.Any()){ tagSel.Increment(q.Tags); }
        // Taslağı kaldır
        drafts.Delete(user, id);
        TempData["Msg"] = "Taslak yayınlandı.";
        return RedirectToAction("Index","Profile");
    }
}
