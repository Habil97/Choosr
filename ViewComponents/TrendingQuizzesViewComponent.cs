using Choosr.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Choosr.Web.ViewComponents;

public class TrendingQuizzesViewComponent(IQuizService quizService) : ViewComponent
{
    public IViewComponentResult Invoke(int take = 6)
    {
        var data = quizService.GetTrending(take);
        return View(data);
    }
}