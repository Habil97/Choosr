using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Choosr.Web.Services;
using Choosr.Web.ViewModels;

namespace Choosr.Web.Controllers;

[Authorize]
public class ManageQuizController(IQuizService quizzes) : Controller
{
    [HttpGet]
    public IActionResult Edit(Guid id)
    {
        var q = quizzes.GetById(id);
        if(q==null) return NotFound();
        if(!User.Identity!.IsAuthenticated || !string.Equals(q.AuthorUserName, User.Identity!.Name, StringComparison.OrdinalIgnoreCase)) return Forbid();
        return View(q);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(QuizDetailViewModel form)
    {
        if(!User.Identity!.IsAuthenticated) return Forbid();
        // Yetki kontrolü: mevcut quiz yazarı ile eşleşmeli
        var existing = quizzes.GetById(form.Id);
        if(existing==null) return NotFound();
        if(!string.Equals(existing.AuthorUserName, User.Identity!.Name, StringComparison.OrdinalIgnoreCase)) return Forbid();
        // TagsCsv form alanını parse et (virgülle ayrılmış)
        var tagsCsv = Request?.Form["TagsCsv"].ToString();
        if(!string.IsNullOrWhiteSpace(tagsCsv))
        {
            form.Tags = tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Select(t => t.ToLowerInvariant())
                                .Distinct()
                                .ToArray();
        }
        else
        {
            // Boş ise mevcut etiketleri koru
            form.Tags = existing.Tags;
        }
        // Choices gönderilmediyse (boş/null) mevcut seçimleri koru
        if(form.Choices == null || !form.Choices.Any())
        {
            form.Choices = existing.Choices;
        }
        var updated = quizzes.Update(form);
        if(updated==null){ ModelState.AddModelError(string.Empty, "Güncelleme başarısız"); return View(form); }
        TempData["Msg"] = "Quiz güncellendi";
        return RedirectToAction("Index","Profile");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(Guid id)
    {
        if(!User.Identity!.IsAuthenticated) return Forbid();
        var ok = quizzes.Delete(id, User.Identity!.Name!);
        if(!ok) TempData["Msg"] = "Silme başarısız veya yetkiniz yok"; else TempData["Msg"] = "Quiz silindi";
        return RedirectToAction("Index","Profile");
    }
}
