using Microsoft.AspNetCore.Mvc;
using Choosr.Infrastructure.Services;
using Choosr.Web.Services;

namespace Choosr.Web.Controllers;

public class PublicProfileController(IUserProfileService profiles, IQuizService quizzes, IBadgeService badges) : Controller
{
    [HttpGet("u/{userName}")]
    public async Task<IActionResult> Show(string userName)
    {
        if(string.IsNullOrWhiteSpace(userName)) return NotFound();
        var p = profiles.GetByUserName(userName);
        if(p==null) return NotFound();
        ViewBag.Created = quizzes.GetByAuthor(userName, includeNonPublic: false).ToList();
        ViewBag.Badges = await badges.GetBadgesAsync(userName);
        return View(p);
    }
}
