using Choosr.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Choosr.Web.ViewComponents;

public class CategoriesBarViewComponent(ICategoryService categoryService) : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var cats = categoryService.GetAll();
        return View(cats);
    }
}