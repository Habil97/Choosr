using Choosr.Web.Services;
using Choosr.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Gif;
using Microsoft.AspNetCore.RateLimiting;

namespace Choosr.Web.Controllers;

public class CreateQuizController(IQuizService quizService, ICategoryService categories, ICaptchaVerifier captcha, IFileScanner scanner, Choosr.Web.Services.IImageProcessingQueue imgQueue, Choosr.Web.Services.IIdempotencyStore idem) : Controller
{
    private const string SessionKey = "_WizardState"; // NOTE: backend persist later
    private const string Purpose = "create-quiz";

    public IActionResult Details()
    {
        if(User?.Identity?.IsAuthenticated != true)
        {
            TempData["AuthMsg"] = "Quiz oluşturmak için giriş yapmalısınız.";
            return RedirectToAction("Login","Account");
        }
        var vm = new CreateQuizDetailsStepViewModel();
        ViewBag.Categories = categories.GetAll();
        // Issue an idempotency token and surface to the view
        var userKey0 = User?.Identity?.Name ?? (HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anon");
        ViewBag.IdemToken = idem.CreateToken(userKey0, Purpose, TimeSpan.FromMinutes(15));
        return View(vm);
    }

    [HttpPost]
    [EnableRateLimiting("create-quiz")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Details(CreateQuizDetailsStepViewModel model, IFormFile? CoverFile, [FromForm(Name="cf-turnstile-response")] string? captchaToken, [FromForm(Name="IdemToken")] string? idemToken)
    {
        if(User?.Identity?.IsAuthenticated != true)
        {
            TempData["AuthMsg"] = "Quiz oluşturmak için giriş yapmalısınız.";
            return RedirectToAction("Login","Account");
        }
        if(!string.IsNullOrWhiteSpace(idemToken))
        {
            TempData[SessionKey+"IdemToken"] = idemToken;
        }
        // Captcha
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var okCaptcha = await captcha.VerifyAsync(captchaToken ?? string.Empty, remoteIp);
        if(!okCaptcha){ ModelState.AddModelError("Captcha", "Doğrulama başarısız, lütfen tekrar deneyin."); }
        if(string.IsNullOrWhiteSpace(model.Title)) ModelState.AddModelError("Title","Başlık zorunlu");
        if(string.IsNullOrWhiteSpace(model.Category)) ModelState.AddModelError("Category","Kategori zorunlu");
        if(!ModelState.IsValid){
            ViewBag.Categories = categories.GetAll();
            ViewBag.IdemToken = idemToken ?? (string?)TempData[SessionKey+"IdemToken"] ?? NewToken();
            return View(model);
        }
        TempData[SessionKey+"Title"] = model.Title;
        TempData[SessionKey+"Category"] = model.Category;
        TempData[SessionKey+"Description"] = model.Description;
        TempData[SessionKey+"IsAnonymous"] = model.IsAnonymous;
        TempData[SessionKey+"Visibility"] = model.Visibility;
        // Parse tags coming from hidden TagsCsv or model.Tags (if later bound)
        var tagsCsv = Request?.Form["TagsCsv"].ToString();
        if(!string.IsNullOrWhiteSpace(tagsCsv))
        {
            var tags = tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                               .Select(t => t.ToLowerInvariant())
                               .Distinct()
                               .ToArray();
            TempData[SessionKey+"Tags"] = string.Join(',', tags);
        }
        // Save cover file if provided
        if(CoverFile != null && CoverFile.Length > 0)
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsDir);
            var ext = Path.GetExtension(CoverFile.FileName).ToLowerInvariant();
            var allowed = new[]{".png",".jpg",".jpeg",".webp",".gif"};
            if(!allowed.Contains(ext)) ext = ".jpg";
            if(CoverFile.Length > 10_000_000) { ModelState.AddModelError("CoverFile","Dosya çok büyük (max 10MB)"); ViewBag.Categories = categories.GetAll(); return View(model); }
            var name = $"{Guid.NewGuid()}{ext}";
            var path = Path.Combine(uploadsDir, name);
            await using var ms = new MemoryStream();
            await CoverFile.CopyToAsync(ms);
            ms.Position = 0;
            var safe2 = await scanner.IsSafeAsync(ms, CoverFile.FileName);
            if(!safe2){ ModelState.AddModelError("CoverFile","Dosya güvenli değil."); ViewBag.Categories = categories.GetAll(); return View(model); }
            ms.Position = 0;
            var tcs = new TaskCompletionSource<(bool ok, int? w, int? h)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var req = new Choosr.Web.Services.ImageProcessRequest(ms, CoverFile.FileName, path, (img) => {
                var targetRatio = 16f/9f;
                var w = img.Width; var h = img.Height; var currentRatio = (float)w/h;
                Rectangle cropRect;
                if(currentRatio > targetRatio){ var newW = (int)(h * targetRatio); var x = (w - newW)/2; cropRect = new Rectangle(x, 0, newW, h); }
                else { var newH = (int)(w / targetRatio); var y = (h - newH)/2; cropRect = new Rectangle(0, y, w, newH); }
                IImageEncoder encoder = ext switch { 
                    ".png" => new PngEncoder(), 
                    ".webp" => new WebpEncoder(), 
                    ".gif" => new GifEncoder(), 
                    _ => new JpegEncoder { Quality = 85 } 
                };
                return (encoder, (IImageProcessingContext ctx) => ctx.Crop(cropRect).Resize(1280, 720));
            }, tcs);
            var _ = await imgQueue.EnqueueAsync(req);
            TempData[SessionKey+"Cover"] = "/uploads/" + name;
            TempData[SessionKey+"CoverW"] = 1280;
            TempData[SessionKey+"CoverH"] = 720;
        }
        TempData.Keep();
        return RedirectToAction(nameof(Choices));
    }

    public IActionResult Choices()
    {
        if(User?.Identity?.IsAuthenticated != true)
        {
            TempData["AuthMsg"] = "Quiz oluşturmak için giriş yapmalısınız.";
            return RedirectToAction("Login","Account");
        }
        var vm = new CreateQuizChoicesStepViewModel();
        // 1. adımda yüklenen Kapak gibi TempData bilgilerini POST'a kadar koru
        TempData.Keep();
        ViewBag.Cover = TempData[SessionKey+"Cover"] as string;
        // Carry idempotency token to step 2
        ViewBag.IdemToken = (string?)TempData[SessionKey+"IdemToken"] ?? NewToken();
        return View(vm);
    }

    [HttpPost]
    [EnableRateLimiting("create-quiz")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Choices(string actionType, List<IFormFile>? Files, string? Manifest, IFormFile? CoverOverride, [FromForm(Name="cf-turnstile-response")] string? captchaToken, Guid? DraftId, [FromForm(Name="IdemToken")] string? idemToken)
    {
        if(User?.Identity?.IsAuthenticated != true)
        {
            TempData["AuthMsg"] = "Quiz oluşturmak için giriş yapmalısınız.";
            return RedirectToAction("Login","Account");
        }
        // Prepare idempotency token (may be null for drafts)
        var userKey = User?.Identity?.Name ?? (HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anon");
        var token = !string.IsNullOrWhiteSpace(idemToken) ? idemToken : (string?)TempData[SessionKey+"IdemToken"];
        // Captcha (only enforce for publish)
        var remoteIp2 = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var okCaptcha2 = string.Equals(actionType, "save-draft", StringComparison.OrdinalIgnoreCase)
            ? true
            : await captcha.VerifyAsync(captchaToken ?? string.Empty, remoteIp2);
        if(!okCaptcha2) { ModelState.AddModelError("Captcha","Doğrulama başarısız."); ViewBag.IdemToken = token ?? NewToken(); return View(new CreateQuizChoicesStepViewModel()); }
        if(string.Equals(actionType, "finish", StringComparison.OrdinalIgnoreCase))
        {
            if(!string.IsNullOrWhiteSpace(token) && idem.TryGetResult<Guid>(token!, userKey, out var existingId, Purpose))
            {
                return RedirectToAction(nameof(Result), new { id = existingId, success = true});
            }
        }
        // Parse manifest coming from client
        var items = new List<ChoiceItemDto>();
        if(!string.IsNullOrWhiteSpace(Manifest))
        {
            try{
                var opts = new JsonSerializerOptions{ PropertyNameCaseInsensitive = true };
                items = JsonSerializer.Deserialize<List<ChoiceItemDto>>(Manifest!, opts) ?? new();
            }
            catch{ items = new(); }
        }
        if(!string.Equals(actionType, "save-draft", StringComparison.OrdinalIgnoreCase))
        {
            if(items.Count < 8) ModelState.AddModelError("Choices","En az 8 seçim ekleyin");
            if(items.Count > 64) ModelState.AddModelError("Choices","En fazla 64 seçim");
            if(!ModelState.IsValid) { ViewBag.IdemToken = token ?? NewToken(); return View(new CreateQuizChoicesStepViewModel()); }
        }

        // Save uploaded images and capture dimensions
        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsDir);
        (string? url, int? w, int? h) SaveFileWithDims(IFormFile f)
        {
            if(f==null || f.Length==0) return (null, null, null);
            if(f.Length > 8_000_000) return (null, null, null);
            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
            var allowed = new[]{".png",".jpg",".jpeg",".webp",".gif"};
            if(!allowed.Contains(ext)) ext = ".jpg";
            var name = $"{Guid.NewGuid()}{ext}";
            var path = Path.Combine(uploadsDir, name);
            using(var ms = new MemoryStream())
            {
                f.CopyTo(ms);
                ms.Position = 0;
                var safe = scanner.IsSafeAsync(ms, f.FileName).GetAwaiter().GetResult();
                if(!safe) return (null, null, null);
                ms.Position = 0;
                var tcs = new TaskCompletionSource<(bool ok, int? w, int? h)>(TaskCreationOptions.RunContinuationsAsynchronously);
                var req = new Choosr.Web.Services.ImageProcessRequest(ms, f.FileName, path, (img) => {
                    var w0 = img.Width; var h0 = img.Height;
                    var side = Math.Min(w0, h0);
                    var x = (w0 - side) / 2;
                    var y = (h0 - side) / 2;
                    var cropRect = new Rectangle(x, y, side, side);
                    SixLabors.ImageSharp.Formats.IImageEncoder enc = ext switch
                    {
                        ".png" => new PngEncoder(),
                        ".webp" => new WebpEncoder(),
                        ".gif" => new GifEncoder(),
                        _ => new JpegEncoder { Quality = 85 }
                    };
                    return (enc, (IImageProcessingContext op) => op.Crop(cropRect).Resize(720, 720));
                }, tcs);
                var res = imgQueue.EnqueueAsync(req).GetAwaiter().GetResult();
            }
            return ("/uploads/" + name, 720, 720);
        }

    var choiceVms = new List<QuizChoiceViewModel>();
        int? firstImageW = null, firstImageH = null; string? firstImageUrl = null;
        for(int i=0;i<items.Count;i++)
        {
            var it = items[i];
            if(string.Equals(it.Kind, "image", StringComparison.OrdinalIgnoreCase))
            {
                var idx = it.FileIndex ?? -1;
                string? url = null; int? iw = null; int? ih = null;
                // Prefer directly provided ImageUrl (from Drafts.UploadChoices result)
                if(!string.IsNullOrWhiteSpace(it.ImageUrl))
                {
                    url = it.ImageUrl;
                }
                else if(Files!=null && idx>=0 && idx<Files.Count)
                {
                    var fileSaved = SaveFileWithDims(Files[idx]);
                    url = fileSaved.url; iw = fileSaved.w; ih = fileSaved.h;
                }
                choiceVms.Add(new QuizChoiceViewModel{ Id=Guid.NewGuid(), ImageUrl = url, ImageWidth = iw, ImageHeight = ih, Caption = it.Caption ?? string.Empty, Order = i+1 });
                if(firstImageUrl == null && !string.IsNullOrWhiteSpace(url)) { firstImageUrl = url; firstImageW = iw; firstImageH = ih; }
            }
            else if(string.Equals(it.Kind, "video", StringComparison.OrdinalIgnoreCase))
            {
                var y = it.YoutubeUrl ?? string.Empty;
                // Thumbnail from YouTube for preview
                string? thumb = null;
                try{
                    var v = new Uri(y);
                    var qp = QueryHelpers.ParseQuery(v.Query);
                    if(qp.TryGetValue("v", out var vvals))
                    {
                        var vId = vvals.ToString();
                        if(!string.IsNullOrWhiteSpace(vId)) thumb = $"https://img.youtube.com/vi/{vId}/hqdefault.jpg";
                    }
                } catch{}
                // hqdefault is typically 480x360
                int? yW = thumb!=null ? 480 : null; int? yH = thumb!=null ? 360 : null;
                choiceVms.Add(new QuizChoiceViewModel{ Id=Guid.NewGuid(), ImageUrl = thumb, ImageWidth = yW, ImageHeight = yH, YoutubeUrl = string.IsNullOrWhiteSpace(y) ? null : y, Caption = it.Caption ?? string.Empty, Order = i+1 });
            }
        }

        // If user requested save-draft, upsert as draft and redirect
        if(string.Equals(actionType, "save-draft", StringComparison.OrdinalIgnoreCase))
        {
            var draftSvc = HttpContext.RequestServices.GetService<Choosr.Web.Services.IDraftService>();
            var user = User?.Identity?.Name ?? string.Empty;
            var draft = new Choosr.Web.ViewModels.DraftViewModel{
                Id = DraftId.GetValueOrDefault(Guid.NewGuid()),
                Title = (string?)TempData[SessionKey+"Title"] ?? "",
                Description = (string?)TempData[SessionKey+"Description"] ?? "",
                Category = (string?)TempData[SessionKey+"Category"] ?? "Genel",
                Visibility = (TempData[SessionKey+"Visibility"] as string) ?? "public",
                IsAnonymous = (TempData[SessionKey+"IsAnonymous"] as bool? == true),
                Tags = ((string?)TempData[SessionKey+"Tags"])?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>(),
                CoverImageUrl = (string?)TempData[SessionKey+"Cover"] ?? null,
                Choices = items.Select((it,i)=> new Choosr.Web.ViewModels.DraftChoiceViewModel{
                    Id = Guid.NewGuid(),
                    ImageUrl = it.ImageUrl,
                    // If we created choices earlier in this action, try to carry dimensions from the constructed list
                    ImageWidth = null,
                    ImageHeight = null,
                    YoutubeUrl = it.YoutubeUrl,
                    Caption = it.Caption,
                    Order = i+1
                }).ToList()
            };
            if(draftSvc!=null && !string.IsNullOrWhiteSpace(user)) draftSvc.Upsert(user, draft);
            TempData["Msg"] = "Taslak kaydedildi.";
            return RedirectToAction("Index","Profile");
        }

        // create quiz (cover + tags mock)
        // Kapak önceliği: Step2 Override > Step1 Cover > ilk seçim thumb
        string? cover = null; int? coverW = null; int? coverH = null;
        if(CoverOverride != null && CoverOverride.Length > 0)
        {
            var uploadsDir2 = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsDir2);
            var ext2 = Path.GetExtension(CoverOverride.FileName).ToLowerInvariant();
            var allowed2 = new[]{".png",".jpg",".jpeg",".webp",".gif"};
            if(!allowed2.Contains(ext2)) ext2 = ".jpg";
            if(CoverOverride.Length > 10_000_000){ ModelState.AddModelError("CoverOverride","Dosya çok büyük (max 10MB)"); ViewBag.IdemToken = token ?? NewToken(); return View(new CreateQuizChoicesStepViewModel()); }
            var name2 = $"{Guid.NewGuid()}{ext2}";
            var path2 = Path.Combine(uploadsDir2, name2);
            await using var ms2 = new MemoryStream();
            await CoverOverride.CopyToAsync(ms2);
            ms2.Position = 0;
            var safe2 = await scanner.IsSafeAsync(ms2, CoverOverride.FileName);
            if(!safe2){ ModelState.AddModelError("CoverOverride","Dosya güvenli değil."); ViewBag.IdemToken = token ?? NewToken(); return View(new CreateQuizChoicesStepViewModel()); }
            ms2.Position = 0;
            var tcs2 = new TaskCompletionSource<(bool ok, int? w, int? h)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var req2 = new Choosr.Web.Services.ImageProcessRequest(ms2, CoverOverride.FileName, path2, (img) => {
                var targetRatio = 16f/9f;
                var w = img.Width; var h = img.Height; var currentRatio = (float)w/h;
                Rectangle cropRect;
                if(currentRatio > targetRatio){ var newW = (int)(h * targetRatio); var x = (w - newW)/2; cropRect = new Rectangle(x, 0, newW, h); }
                else { var newH = (int)(w / targetRatio); var y = (h - newH)/2; cropRect = new Rectangle(0, y, w, newH); }
                IImageEncoder encoder2 = ext2 switch { 
                    ".png" => new PngEncoder(), 
                    ".webp" => new WebpEncoder(), 
                    ".gif" => new GifEncoder(), 
                    _ => new JpegEncoder { Quality = 85 } 
                };
                return (encoder2, (IImageProcessingContext ctx) => ctx.Crop(cropRect).Resize(1280, 720));
            }, tcs2);
            var _r2 = await imgQueue.EnqueueAsync(req2);
            cover = "/uploads/" + name2;
            coverW = 1280; coverH = 720;
        }
        if(string.IsNullOrWhiteSpace(cover))
        {
            cover = (string?)TempData[SessionKey+"Cover"];
            if(!string.IsNullOrWhiteSpace(cover))
            {
                // Cover from step 1 was processed to 1280x720
                coverW = (TempData[SessionKey+"CoverW"] as int?) ?? 1280;
                coverH = (TempData[SessionKey+"CoverH"] as int?) ?? 720;
            }
        }
        if(string.IsNullOrWhiteSpace(cover))
        {
            cover = choiceVms.FirstOrDefault()?.ImageUrl;
            if(!string.IsNullOrWhiteSpace(cover) && cover == firstImageUrl)
            {
                coverW = firstImageW; coverH = firstImageH;
            }
        }
        if(string.IsNullOrWhiteSpace(cover))
        {
            // Eğer ilk seçim video ise, ImageUrl alanına thumb zaten set edilmişti; yine de boşsa sample'a düş.
            cover = "/img/sample1.jpg";
        }
        var q = new QuizDetailViewModel{
            Id = Guid.NewGuid(),
            Title = (string?)TempData[SessionKey+"Title"] ?? "Yeni Quiz",
            Description = (string?)TempData[SessionKey+"Description"] ?? string.Empty,
            Category = (string?)TempData[SessionKey+"Category"] ?? "Genel",
            CoverImageUrl = cover,
            CoverImageWidth = coverW,
            CoverImageHeight = coverH,
            Plays = 0, Comments = 0, Reactions = 0,
            IsEditorPick = false, IsTrending = false,
            AuthorUserName = (TempData[SessionKey+"IsAnonymous"] as bool? == true) ? "anon" : (User?.Identity?.Name ?? "anon"),
            CreatedAt = DateTime.UtcNow,
            Choices = choiceVms,
            Tags = ((string?)TempData[SessionKey+"Tags"])?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>()
        };
        var visibility = (TempData[SessionKey+"Visibility"] as string) ?? "public";
        q.IsPublic = visibility == "public";
    var savedQuiz = quizService.Add(q);
        // Mark idempotency result so repeated submits with the same token reuse this quiz id
        if(!string.IsNullOrWhiteSpace(token))
        {
            idem.TrySetResult<Guid>(token!, userKey, savedQuiz.Id, Purpose);
        }
    return RedirectToAction(nameof(Result), new { id = savedQuiz.Id, success = true});
    }

    public IActionResult Result(Guid id, bool success=true)
    {
        if(User?.Identity?.IsAuthenticated != true)
        {
            TempData["AuthMsg"] = "Quiz oluşturmak için giriş yapmalısınız.";
            return RedirectToAction("Login","Account");
        }
        var vm = new CreateQuizResultStepViewModel{QuizId=id,Success=success,Message= success?"Quiz başarıyla oluşturuldu":"Hata oluştu"};
        return View(vm);
    }

    private string NewToken()
    {
        var userKey = User?.Identity?.Name ?? (HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anon");
        var t = idem.CreateToken(userKey, Purpose, TimeSpan.FromMinutes(15));
        TempData[SessionKey+"IdemToken"] = t;
        TempData.Keep();
        return t;
    }

    private class ChoiceItemDto
    {
        public string Kind { get; set; } = "image"; // image | video
        public int? FileIndex { get; set; }
        public string? YoutubeUrl { get; set; }
        public string? Caption { get; set; }
        public string? ImageUrl { get; set; }
    }
}
