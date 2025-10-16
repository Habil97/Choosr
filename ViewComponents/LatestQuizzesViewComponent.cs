using Choosr.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Choosr.Web.ViewComponents;

public class LatestQuizzesViewComponent(IQuizService quizService) : ViewComponent
{
    public IViewComponentResult Invoke(int take = 6)
    {
        var data = quizService.GetLatest(take);
        return View(data);
    }
}