using Choosr.Infrastructure.Data;
using Choosr.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.OutputCaching;
var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllersWithViews();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.AddDbContext<AppDbContext>(options=>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"))
);

// Output caching (page-level caching for popular lists and category pages)
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("home-index", b => b
        .Expire(TimeSpan.FromSeconds(60))
        .SetVaryByQuery("*") // future-proof; home currently no query
        // Vary by auth cookie so header (Giriş/Profil) renders correctly per user
        .SetVaryByHeader("Cookie")
    );
    options.AddPolicy("quiz-list", b => b
        .Expire(TimeSpan.FromSeconds(30))
        .SetVaryByQuery("category","q","tag","sort","page","pageSize")
        // Vary by auth cookie so header (Giriş/Profil) renders correctly per user
        .SetVaryByHeader("Cookie")
    );
    options.AddPolicy("partials-short", b => b
        .Expire(TimeSpan.FromSeconds(20))
        .SetVaryByQuery("*")
        .SetVaryByRouteValue("*")
    );
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
});
// Persist profiles and counters to a JSON file under App_Data
builder.Services.AddSingleton<Choosr.Infrastructure.Services.IUserProfileService>(sp =>
{
    var env = sp.GetRequiredService<IHostEnvironment>();
    var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
    var filePath = Path.Combine(dataDir, "profiles.json");
    return new Choosr.Infrastructure.Services.FileUserProfileService(filePath);
});

// Creator experience helpers
builder.Services.AddScoped<Choosr.Web.Services.IDraftService, Choosr.Web.Services.EfDraftService>();
builder.Services.AddScoped<Choosr.Web.Services.ITagSelectionService, Choosr.Web.Services.EfTagSelectionService>();
builder.Services.AddScoped<Choosr.Web.Services.ITagSuggestService, Choosr.Web.Services.BlendedTagSuggestService>();
builder.Services.AddSingleton<Choosr.Web.Services.IFileScanner, Choosr.Web.Services.NoopFileScanner>();
builder.Services.AddScoped<Choosr.Web.Services.IQuizService, Choosr.Web.Services.EfQuizService>();
builder.Services.AddScoped<Choosr.Web.Services.IBadgeService, Choosr.Web.Services.EfBadgeService>();
builder.Services.AddSingleton<Choosr.Web.Services.ICategoryService, Choosr.Web.Services.InMemoryCategoryService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<Choosr.Web.Services.ICaptchaVerifier, Choosr.Web.Services.TurnstileCaptchaVerifier>();
builder.Services.AddSingleton<Choosr.Web.Services.IFileScanner, Choosr.Web.Services.NoopFileScanner>();
builder.Services.AddSingleton<Choosr.Web.Services.IBadWordsFilter, Choosr.Web.Services.FileBadWordsFilter>();
builder.Services.AddMemoryCache();
// Idempotency store for create-quiz flow
builder.Services.AddSingleton<Choosr.Web.Services.IIdempotencyStore, Choosr.Web.Services.InMemoryIdempotencyStore>();
// TagStatsService needs DbContext (scoped) so must also be scoped
builder.Services.AddScoped<Choosr.Web.Services.ITagStatsService, Choosr.Web.Services.TagStatsService>();
// Central invalidator for tag stats cache (version bump on changes)
builder.Services.AddSingleton<Choosr.Web.Services.ITagStatsInvalidator, Choosr.Web.Services.TagStatsInvalidator>();
// Image processing queue (background service + API)
builder.Services.AddSingleton<Choosr.Web.Services.BoundedImageProcessingQueue>();
builder.Services.AddSingleton<Choosr.Web.Services.IImageProcessingQueue>(sp => sp.GetRequiredService<Choosr.Web.Services.BoundedImageProcessingQueue>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<Choosr.Web.Services.BoundedImageProcessingQueue>());
// Notifications
builder.Services.AddSingleton<Choosr.Web.Services.IReportNotificationService, Choosr.Web.Services.EmailReportNotificationService>();
builder.Services.AddScoped<Choosr.Web.Services.INotificationService, Choosr.Web.Services.EfNotificationService>();

// Rate limiting: Separate policies for comments and quiz creation
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        var retryAfter = 0d;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra))
        {
            retryAfter = ra.TotalSeconds;
            context.HttpContext.Response.Headers["Retry-After"] = Math.Ceiling(retryAfter).ToString();
        }
        var body = System.Text.Json.JsonSerializer.Serialize(new { message = "Çok fazla istek. Lütfen biraz sonra tekrar deneyin.", retryAfterSeconds = retryAfter });
        await context.HttpContext.Response.WriteAsync(body, token);
    };
    // Load limits from configuration (with sane defaults)
    var commentsLimit = builder.Configuration.GetValue<int?>("RateLimits:Comments:TokenLimit") ?? 10;
    var commentsWindowSeconds = builder.Configuration.GetValue<int?>("RateLimits:Comments:WindowSeconds") ?? 30;
    var reportsLimit = builder.Configuration.GetValue<int?>("RateLimits:Reports:TokenLimit") ?? 6;
    var reportsWindowSeconds = builder.Configuration.GetValue<int?>("RateLimits:Reports:WindowSeconds") ?? 60;
    options.AddPolicy("comments", context =>
    {
        var key = context.User?.Identity?.IsAuthenticated == true
            ? (context.User?.FindFirst("sub")?.Value ?? context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ?? "anon")
            : context.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = commentsLimit,
            QueueLimit = 0,
            ReplenishmentPeriod = TimeSpan.FromSeconds(commentsWindowSeconds),
            TokensPerPeriod = commentsLimit,
            AutoReplenishment = true
        });
    });
    options.AddPolicy("create-quiz", context =>
    {
        var key = context.User?.Identity?.IsAuthenticated == true
            ? (context.User?.FindFirst("sub")?.Value ?? context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ?? "anon")
            : context.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(10),
            AutoReplenishment = true
        });
    });
    options.AddPolicy("reports", context =>
    {
        var key = context.User?.Identity?.IsAuthenticated == true
            ? (context.User?.FindFirst("sub")?.Value ?? context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ?? "anon")
            : context.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = reportsLimit,
            QueueLimit = 0,
            ReplenishmentPeriod = TimeSpan.FromSeconds(reportsWindowSeconds),
            TokensPerPeriod = reportsLimit,
            AutoReplenishment = true
        });
    });
    options.AddPolicy("reactions", context =>
    {
        var key = context.User?.Identity?.IsAuthenticated == true
            ? (context.User?.FindFirst("sub")?.Value ?? context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ?? "anon")
            : context.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 20,
            QueueLimit = 0,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = 20,
            AutoReplenishment = true
        });
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.PhysicalPath ?? string.Empty;
        // Simple heuristic: images, css, js -> cache long; others default
        if(path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=2592000,immutable"; // 30 gün
        }
    }
});
app.UseRouting();
app.UseResponseCompression();
// Security headers middleware (lightweight hardening)
// Nonce generation middleware (executes early so later middlewares/views can read the nonce)
app.Use(async (ctx, next) =>
{
    var nonceBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
    var nonce = Convert.ToBase64String(nonceBytes);
    ctx.Items["CspNonce"] = nonce;
    await next();
});

// Security headers + CSP (will be updated again after external script migration)
app.Use(async (ctx, next) =>
{
    var headers = ctx.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), interest-cohort=()";
    if(!headers.ContainsKey("Content-Security-Policy"))
    {
        var nonce = ctx.Items["CspNonce"] as string ?? string.Empty;
        // Tightened CSP: no unsafe-inline for scripts; allow Turnstile domain
        // TODO: Move inline styles to external or add hashes, then drop 'unsafe-inline' from style-src
    headers["Content-Security-Policy"] = $"default-src 'self'; img-src 'self' data: blob: https:; script-src 'self' 'nonce-{nonce}' https://challenges.cloudflare.com; style-src 'self' 'unsafe-inline'; frame-src https://challenges.cloudflare.com; object-src 'none'; frame-ancestors 'self'; base-uri 'self'; form-action 'self'";
    }
    await next();
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
// Place OutputCache after authentication so vary-by-user works correctly
app.UseOutputCache();

// Optional HTML response minification (safe whitespace trim between tags)
var minifyEnabled = builder.Configuration.GetValue<bool>("ResponseMinify:Enabled");
if(minifyEnabled)
{
    app.UseMiddleware<Choosr.Web.Middleware.HtmlMinifyMiddleware>();
}

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Apply pending migrations (dev convenience)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    // Seed Admin role and elevate users from configuration
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    const string adminRole = "Admin";
    if(!await roleMgr.RoleExistsAsync(adminRole))
    {
        await roleMgr.CreateAsync(new IdentityRole(adminRole));
    }
    var emails = builder.Configuration.GetSection("Admin:ElevateEmails").Get<string[]>() ?? Array.Empty<string>();
    foreach(var email in emails)
    {
        if(string.IsNullOrWhiteSpace(email)) continue;
        var user = await userMgr.FindByEmailAsync(email);
        if(user != null && !await userMgr.IsInRoleAsync(user, adminRole))
        {
            await userMgr.AddToRoleAsync(user, adminRole);
        }
    }
}

app.Run();
