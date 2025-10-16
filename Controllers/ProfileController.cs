using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Choosr.Infrastructure.Services;
using Choosr.Web.Services;
using Choosr.Infrastructure.Identity;
using Choosr.Domain.Models;

namespace Choosr.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IUserProfileService _profiles;
    private readonly Services.IQuizService _quizzes;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ICaptchaVerifier _captcha;
    private readonly IConfiguration _config;
    public ProfileController(IUserProfileService profiles, Services.IQuizService quizzes, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ICaptchaVerifier captcha, IConfiguration config){_profiles=profiles;_quizzes=quizzes;_userManager=userManager;_signInManager=signInManager;_captcha=captcha;_config=config;}

    private async Task<UserProfile?> GetOrCreateProfileAsync(){
        if(User?.Identity?.IsAuthenticated!=true || string.IsNullOrWhiteSpace(User.Identity!.Name)) return null;
        var existing = _profiles.GetByUserName(User.Identity!.Name!);
        if(existing!=null) return existing;
        // Create an in-memory profile record based on Identity user
        var u = await _userManager.FindByNameAsync(User.Identity!.Name!);
        if(u==null) return null;
    var uname = u.UserName ?? string.Empty;
    var dname = string.IsNullOrWhiteSpace(u.DisplayName) ? uname : u.DisplayName!;
    var newProf = new UserProfile{ UserName = uname, DisplayName = dname, AvatarUrl = u.AvatarUrl ?? "/img/demo-avatar.png" };
        try{ _profiles.Create(newProf); } catch {}
        return newProf;
    }

    public async Task<IActionResult> Index(){
        var p = await GetOrCreateProfileAsync(); if(p==null) return RedirectToAction("Login","Account");
        // Kullanıcının oluşturduğu quizleri (özel olanlar dahil) çek
        var created = _quizzes.GetByAuthor(p.UserName, includeNonPublic: true).ToList();
        ViewBag.Created = created;
        // Avatar altındaki sayacı güncelle
        p.CreatedCount = created.Count;
        // Played & Reactions listeleri
    var prof = _profiles.GetByUserName(p.UserName);
    var playedIds = _profiles.GetPlayed(p.UserName).ToHashSet();
    // Derleme: reaksiyonları DB'den hesapla ki veritabanı temizlenince profil de sıfırlansın
    var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ?? string.Empty;
    var reactedIds = string.IsNullOrWhiteSpace(userId) ? new HashSet<Guid>() : _quizzes.GetReactedQuizIdsByUser(userId).ToHashSet();
    var all = _quizzes.Search(null, null, null, null, 1, 2000).Items.ToList();
    ViewBag.Played = all.Where(q=> playedIds.Contains(q.Id)).ToList();
        ViewBag.Reactions = all.Where(q=> reactedIds.Contains(q.Id)).ToList();
        p.PlayedCount = playedIds.Count;
        p.ReactionCount = reactedIds.Count;
        // Yorum sayısını DB'den hesapla (persisted profildeki eski değerleri ez)
        if(!string.IsNullOrWhiteSpace(userId))
        {
            p.CommentCount = _quizzes.GetUserCommentsCount(userId);
        }
        // Taslakları getir
        var draftsSvc = HttpContext.RequestServices.GetService<Choosr.Web.Services.IDraftService>();
        if(draftsSvc!=null){ ViewBag.Drafts = draftsSvc.List(p.UserName); }
        return View(p);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(){ var p=await GetOrCreateProfileAsync(); if(p==null) return RedirectToAction("Login","Account"); return View(p); }

    [HttpPost]
    public async Task<IActionResult> Edit(UserProfile form, IFormFile? Avatar, bool RemoveAvatar=false){
        var p=await GetOrCreateProfileAsync(); if(p==null) return RedirectToAction("Login","Account");
        p.DisplayName=form.DisplayName; p.Bio=form.Bio; p.Twitter=form.Twitter; p.Instagram=form.Instagram; p.Youtube=form.Youtube; p.Twitch=form.Twitch; p.Kick=form.Kick; _profiles.Update(p);
        // Identity kullanıcısını da senkronize et
        var user = await _userManager.GetUserAsync(User);
        if(user!=null){
            user.DisplayName = form.DisplayName;
        }
        if(RemoveAvatar){ p.AvatarUrl=null; _profiles.Update(p); if(user!=null){ user.AvatarUrl = null; } }
        else if(Avatar!=null && Avatar.Length>0){
            var ext=Path.GetExtension(Avatar.FileName).ToLowerInvariant();
            var allowed=new[]{".png",".jpg",".jpeg",".webp"};
            if(allowed.Contains(ext)){
                var fileName=$"{p.Id}{ext}";
                var dir=Path.Combine(Directory.GetCurrentDirectory(),"wwwroot","avatars");
                Directory.CreateDirectory(dir);
                var path=Path.Combine(dir,fileName);
                using var fs=new FileStream(path,FileMode.Create); Avatar.CopyTo(fs);
                p.AvatarUrl=$"/avatars/{fileName}"; _profiles.Update(p);
                if(user!=null){ user.AvatarUrl = p.AvatarUrl; }
            }
        }
        if(user!=null){ await _userManager.UpdateAsync(user); }
        TempData["Msg"]="Profil güncellendi";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> ChangePassword(){ var p=await GetOrCreateProfileAsync(); if(p==null) return RedirectToAction("Login","Account"); return View(p); }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(string CurrentPassword, string Password, string PasswordConfirm, [FromForm(Name="cf-turnstile-response")] string? captchaToken)
    {
        var require = string.Equals(_config["Captcha:RequireOn:ChangePassword"], "true", StringComparison.OrdinalIgnoreCase);
        if(require)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var ok = await _captcha.VerifyAsync(captchaToken ?? string.Empty, ip);
            if(!ok)
            {
                var p0 = await GetOrCreateProfileAsync();
                if(p0==null) return RedirectToAction("Login","Account");
                ModelState.AddModelError(string.Empty, "Doğrulama başarısız, lütfen tekrar deneyin.");
                return View(p0);
            }
        }
        var p = await GetOrCreateProfileAsync(); if(p==null) return RedirectToAction("Login","Account");
        if(string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(PasswordConfirm))
        {
            ModelState.AddModelError(string.Empty, "Lütfen yeni şifre alanlarını doldurun.");
            return View(p);
        }
        if(Password != PasswordConfirm)
        {
            ModelState.AddModelError(string.Empty, "Şifreler eşleşmiyor.");
            return View(p);
        }
        var user = await _userManager.GetUserAsync(User);
        if(user == null)
        {
            ModelState.AddModelError(string.Empty, "Kullanıcı bulunamadı.");
            return View(p);
        }
        // Eğer CurrentPassword boşsa ve kullanıcının parolası yoksa AddPassword yapılabilir.
        var hasPwd = await _userManager.HasPasswordAsync(user);
        IdentityResult result;
        if(hasPwd)
        {
            if(string.IsNullOrWhiteSpace(CurrentPassword))
            {
                ModelState.AddModelError(string.Empty, "Mevcut şifre gerekli.");
                return View(p);
            }
            result = await _userManager.ChangePasswordAsync(user, CurrentPassword, Password);
        }
        else
        {
            result = await _userManager.AddPasswordAsync(user, Password);
        }
        if(result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["Msg"] = "Şifre güncellendi.";
            return RedirectToAction("Index");
        }
    foreach(var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
    return View(p);
    }
}
