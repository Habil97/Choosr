using Choosr.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Choosr.Web.ViewComponents;

public class EditorPicksViewComponent(IQuizService quizService) : ViewComponent
{
    public IViewComponentResult Invoke(int take = 6)
    {
        var data = quizService.GetEditorPicks(take);
        return View(data);
    }
}