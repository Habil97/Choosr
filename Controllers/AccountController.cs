using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Choosr.Infrastructure.Identity;
using Choosr.Web.Services;

namespace Choosr.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly ICaptchaVerifier _captcha;
    private readonly IConfiguration _config;
    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, ICaptchaVerifier captcha, IConfiguration config)
    {
        _users = users; _signIn = signIn; _captcha = captcha; _config = config;
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(string? UserOrEmail, string? Password, bool Remember, [FromForm(Name="cf-turnstile-response")] string? captchaToken)
    {
        var require = string.Equals(_config["Captcha:RequireOn:Login"], "true", StringComparison.OrdinalIgnoreCase);
        if(require)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var okCap = await _captcha.VerifyAsync(captchaToken ?? string.Empty, ip);
            if(!okCap)
            {
                ModelState.AddModelError(string.Empty, "Doğrulama başarısız, lütfen tekrar deneyin.");
                return View();
            }
        }
        if(string.IsNullOrWhiteSpace(UserOrEmail) || string.IsNullOrWhiteSpace(Password))
        {
            ModelState.AddModelError("","Geçersiz giriş denemesi.");
            return View();
        }
        // Try by username first, then by email
        ApplicationUser? user = await _users.FindByNameAsync(UserOrEmail);
        if(user == null)
        {
            user = await _users.FindByEmailAsync(UserOrEmail);
        }
        if(user == null)
        {
            ModelState.AddModelError("","Kullanıcı bulunamadı.");
            return View();
        }
        var result = await _signIn.PasswordSignInAsync(user, Password, Remember, lockoutOnFailure: true);
        if(result.Succeeded)
            return RedirectToAction("Index","Profile");
        if(result.IsLockedOut)
        {
            ModelState.AddModelError("","Hesap kilitlendi. Lütfen daha sonra tekrar deneyin.");
            return View();
        }
        ModelState.AddModelError("","Şifre hatalı.");
        return View();
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(string DisplayName,string Email,string UserName,string Password,string PasswordConfirm, [FromForm(Name="cf-turnstile-response")] string? captchaToken)
    {
        var require = string.Equals(_config["Captcha:RequireOn:Register"], "true", StringComparison.OrdinalIgnoreCase);
        if(require)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var okCap = await _captcha.VerifyAsync(captchaToken ?? string.Empty, ip);
            if(!okCap)
            {
                ModelState.AddModelError(string.Empty, "Doğrulama başarısız, lütfen tekrar deneyin.");
                return View();
            }
        }
        if(string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ModelState.AddModelError("","Lütfen gerekli alanları doldurunuz.");
            return View();
        }
        if(Password != PasswordConfirm)
        {
            ModelState.AddModelError("","Şifreler eşleşmiyor.");
            return View();
        }

        var existingUser = await _users.FindByNameAsync(UserName);
        if(existingUser != null)
        {
            ModelState.AddModelError("","Bu kullanıcı adı zaten alınmış.");
            return View();
        }
        var user = new ApplicationUser{ UserName = UserName, Email = Email, DisplayName = DisplayName, AvatarUrl = "/img/demo-avatar.png" };
        var createResult = await _users.CreateAsync(user, Password);
        if(createResult.Succeeded)
        {
            await _signIn.SignInAsync(user, isPersistent:false);
            return RedirectToAction("Index","Profile");
        }
        foreach(var e in createResult.Errors) ModelState.AddModelError("", e.Description);
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Logout(){ await _signIn.SignOutAsync(); return RedirectToAction("Index","Home"); }
}
