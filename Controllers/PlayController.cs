using Choosr.Web.Services;
using Choosr.Infrastructure.Data;
using Choosr.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Choosr.Web.Controllers;

public class PlayController(IQuizService quizService, AppDbContext db) : Controller
{
    public IActionResult Mode(Guid id)
    {
        var quiz = quizService.GetById(id);
        if(quiz == null) return NotFound();
        return View(quiz);
    }

    public IActionResult Vs(Guid id, int round = 1)
    {
        var quiz = quizService.GetById(id);
        if(quiz == null) return NotFound();
        ViewBag.Round = round;
        // Optional: if there is a challenger query for challenge link, pass through
        ViewBag.Challenge = Request.Query["seed"].FirstOrDefault();
        return View(quiz);
    }

    public IActionResult Bracket(Guid id)
    {
        var quiz = quizService.GetById(id);
        if(quiz == null) return NotFound();
        return View(quiz);
    }

    public IActionResult Rank(Guid id, int? slots)
    {
        var quiz = quizService.GetById(id);
        if(quiz == null) return NotFound();
        ViewBag.Slots = (slots==5 || slots==10) ? slots : 5;
        return View(quiz);
    }

    // Surprise: pick a random public quiz and redirect to Mode
    [HttpGet]
    public IActionResult Surprise()
    {
        var any = quizService.GetLatest(100).ToList();
        if(any.Count == 0) return RedirectToAction("Index","Home");
        var rnd = new Random();
        var pick = any[rnd.Next(any.Count)];
        return RedirectToAction("Mode", new { id = pick.Id });
    }

    [HttpGet]
    public IActionResult RankResult(Guid id, [FromQuery] List<Guid> order)
    {
        var quiz = quizService.GetById(id);
        if(quiz == null) return NotFound();
        ViewBag.Order = order;
        return View("RankResult", quiz);
    }

    [HttpPost]
    public IActionResult RecordRank(Guid id, [FromBody] List<Guid> order)
    {
        // Kör sıralama da tamamlandığında oynanma sayısını artır ve profil played ekle
        quizService.IncreasePlays(id);
        // PlaySession kaydı (rank)
        try
        {
            var champion = (order != null && order.Count > 0) ? order[0] : Guid.Empty;
            var session = new PlaySession
            {
                QuizId = id,
                ChampionId = champion == Guid.Empty ? null : champion,
                Mode = "rank",
                UserName = User?.Identity?.IsAuthenticated == true ? User.Identity!.Name : null
            };
            db.PlaySessions.Add(session);
            db.SaveChanges();
        }
        catch { /* ignore session persistence errors */ }
        if(User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity!.Name))
        {
            try
            {
                var prof = HttpContext.RequestServices.GetService<Choosr.Infrastructure.Services.IUserProfileService>();
                prof?.AddPlayed(User.Identity!.Name!, id);
            }
            catch { }
        }
        return Ok(new { ok = true });
    }

    public IActionResult Finish(Guid id, Guid? w)
    {
        var quiz = quizService.GetById(id);
        if (quiz == null) return NotFound();
        ViewBag.WinnerId = w;
        return View(quiz);
    }

    [HttpPost]
    public IActionResult Record(Guid id, [FromBody] BracketResultDto dto)
    {
        // increase plays and record detailed stats (robust champion fallback)
        quizService.IncreasePlays(id);
        var matchList = (dto?.Matches ?? new List<MatchDto>()).ToList();
        var champion = dto?.Champion ?? Guid.Empty;
        if(champion == Guid.Empty && matchList.Count > 0)
        {
            // Son maçın kazananını şampiyon olarak kabul et
            champion = matchList[^1].Winner;
        }
        Console.WriteLine($"[Record] Quiz:{id} Champion:{champion} Matches:{matchList.Count}");
        if(champion != Guid.Empty)
        {
            var matches = matchList.Select(m => (m.Winner, m.Loser));
            quizService.RecordPlay(id, champion, matches);
        }
        // PlaySession kaydı (vs/bracket)
        try
        {
            var mode = (Request.Query["mode"].FirstOrDefault() ?? "unknown").ToLowerInvariant();
            if(mode != "vs" && mode != "bracket") mode = "unknown";
            var session = new PlaySession
            {
                QuizId = id,
                ChampionId = champion == Guid.Empty ? null : champion,
                Mode = mode,
                UserName = User?.Identity?.IsAuthenticated == true ? User.Identity!.Name : null
            };
            db.PlaySessions.Add(session);
            db.SaveChanges();
        }
        catch { /* ignore session persistence errors */ }
        // Kullanıcı giriş yaptıysa profile 'played' ekle
        if(User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity!.Name))
        {
            try
            {
                var prof = HttpContext.RequestServices.GetService<Choosr.Infrastructure.Services.IUserProfileService>();
                prof?.AddPlayed(User.Identity!.Name!, id);
            }
            catch { }
        }
        return Ok(new { ok = true });
    }

    public class BracketResultDto 
    { 
        public Guid Champion { get; set; }
        public List<MatchDto> Matches { get; set; } = new();
    }
    public record MatchDto(Guid Winner, Guid Loser);
}