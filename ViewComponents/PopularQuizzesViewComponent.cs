using Choosr.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Choosr.Web.ViewComponents;

public class PopularQuizzesViewComponent(IQuizService quizService) : ViewComponent
{
    public IViewComponentResult Invoke(int take = 6)
    {
        var data = quizService.GetPopular(take);
        return View(data);
    }
}