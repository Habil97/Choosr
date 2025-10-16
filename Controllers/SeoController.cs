using System.Text;
using Microsoft.AspNetCore.Mvc;
using Choosr.Web.Services;
using Choosr.Infrastructure.Services;

namespace Choosr.Web.Controllers;

[ApiController]
public class SeoController(IQuizService quizzes) : Controller
{
    private const int SitemapPartitionSize = 500;

    [HttpGet("sitemap-index.xml")]
    [ResponseCache(Duration = 3600)]
    public IActionResult SitemapIndex()
    {
        // If total quizzes small just redirect to single sitemap for simplicity
        var total = quizzes.GetLatest(1_000_000).Count(); // rough count via service; optimize with dedicated count method later
        if(total <= SitemapPartitionSize)
        {
            return RedirectPermanent("/sitemap.xml");
        }
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        int partitions = (int)Math.Ceiling(total / (double)SitemapPartitionSize);
        for(int i=1;i<=partitions;i++)
        {
            sb.Append("  <sitemap><loc>")
              .Append(System.Net.WebUtility.HtmlEncode($"{baseUrl}/sitemap-part-{i}.xml"))
              .AppendLine("</loc></sitemap>");
        }
        sb.AppendLine("</sitemapindex>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    [HttpGet("sitemap-part-{part:int}.xml")]
    [ResponseCache(Duration = 3600)]
    public IActionResult SitemapPart(int part)
    {
        if(part <= 0) return NotFound();
        // Materialize all (could be optimized with real paging at DB layer later)
        var all = quizzes.GetLatest(part * SitemapPartitionSize).ToList();
        if(!all.Any()) return NotFound();
        int skip = (part-1) * SitemapPartitionSize;
        var page = all.Skip(skip).Take(SitemapPartitionSize).ToList();
        if(page.Count == 0) return NotFound();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        void UrlTag(string loc, DateTime? lastMod=null){
            sb.Append("  <url><loc>").Append(System.Net.WebUtility.HtmlEncode(baseUrl+loc)).Append("</loc>");
            if(lastMod.HasValue) sb.Append("<lastmod>").Append(lastMod.Value.ToString("yyyy-MM-dd")).Append("</lastmod>");
            sb.AppendLine("</url>");
        }
        foreach(var q in page){ UrlTag($"/Quiz/Detail/{q.Id}", q.CreatedAt); }
        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }
    [HttpGet("sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public IActionResult Sitemap()
    {
        // Conditional GET (simple time threshold: newest quiz timestamp)
        var latestQuiz = quizzes.GetLatest(1).FirstOrDefault();
        var lastMod = latestQuiz?.CreatedAt.ToUniversalTime() ?? DateTime.UtcNow.AddMinutes(-10);
        if(Request.Headers.TryGetValue("If-Modified-Since", out var ims))
        {
            if(DateTime.TryParse(ims, out var since))
            {
                if(lastMod <= since.ToUniversalTime()) return StatusCode(304);
            }
        }
        Response.Headers["Last-Modified"] = lastMod.ToString("R");
        var sb = new StringBuilder();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        void UrlTag(string loc, DateTime? lastMod=null){
            sb.Append("  <url><loc>").Append(System.Net.WebUtility.HtmlEncode(baseUrl+loc)).Append("</loc>");
            if(lastMod.HasValue) sb.Append("<lastmod>").Append(lastMod.Value.ToString("yyyy-MM-dd")) .Append("</lastmod>");
            sb.AppendLine("</url>");
        }
        UrlTag("/");
        UrlTag("/Quiz");
        // Last 200 public quizzes
        var latest = quizzes.GetLatest(200).ToList();
        foreach(var q in latest){ UrlTag($"/Quiz/Detail/{q.Id}", q.CreatedAt); }
        // Tag listing (top tags)
        try {
            var tagStats = HttpContext.RequestServices.GetService<Choosr.Web.Services.ITagStatsService>();
            var tags = tagStats?.GetTopTags(100) ?? new List<Choosr.Web.Services.TagStatDto>();
            foreach(var t in tags){ UrlTag($"/Quiz?tag={Uri.EscapeDataString(t.Name)}"); }
        } catch {}
        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    [HttpGet("feed.xml")]
    [ResponseCache(Duration = 600)]
    public IActionResult Feed()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var latest = quizzes.GetLatest(30).ToList();
        var lastMod = latest.FirstOrDefault()?.CreatedAt.ToUniversalTime() ?? DateTime.UtcNow.AddMinutes(-10);
        if(Request.Headers.TryGetValue("If-Modified-Since", out var ims))
        {
            if(DateTime.TryParse(ims, out var since))
            {
                if(lastMod <= since.ToUniversalTime()) return StatusCode(304);
            }
        }
        Response.Headers["Last-Modified"] = lastMod.ToString("R");
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<rss version=\"2.0\"><channel>");
        sb.AppendLine("<title>Choosr - Son Quizler</title>");
        sb.AppendLine($"<link>{baseUrl}</link>");
        sb.AppendLine("<description>Yeni eklenen quizleri takip edin.</description>");
        sb.AppendLine("<language>tr</language>");
        foreach(var q in latest){
            var link = baseUrl+"/Quiz/Detail/"+q.Id;
            var title = System.Net.WebUtility.HtmlEncode(q.Title);
            var desc = System.Net.WebUtility.HtmlEncode(q.Description ?? q.Title);
            sb.AppendLine("<item>");
            sb.AppendLine($"<title>{title}</title>");
            sb.AppendLine($"<link>{link}</link>");
            sb.AppendLine($"<guid isPermaLink=\"true\">{link}</guid>");
            sb.AppendLine($"<pubDate>{q.CreatedAt:R}</pubDate>");
            sb.AppendLine($"<description><![CDATA[{desc}]]></description>");
            sb.AppendLine("</item>");
        }
        sb.AppendLine("</channel></rss>");
        return Content(sb.ToString(), "application/rss+xml", Encoding.UTF8);
    }

    [HttpGet("robots.txt")]
    [ResponseCache(Duration = 86400)]
    public IActionResult Robots()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var content = $"User-agent: *\nAllow: /\nSitemap: {baseUrl}/sitemap.xml\n";
        return Content(content, "text/plain", Encoding.UTF8);
    }

    [HttpGet("healthz")]
    public IActionResult Health([FromServices] Choosr.Infrastructure.Data.AppDbContext db)
    {
        try {
            db.Database.CanConnect();
            return Ok(new { status = "ok", db = true, time = DateTime.UtcNow });
        } catch { return StatusCode(503, new { status = "degraded" }); }
    }
}
