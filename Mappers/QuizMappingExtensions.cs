using Choosr.Domain.Entities;
using Choosr.Web.ViewModels;

namespace Choosr.Web.Mappers;

public static class QuizMappingExtensions
{
    public static QuizCardViewModel ToCardViewModel(this Quiz q)
    {
        // Basit dil seçim mantığı: Thread.CurrentUICulture'ye göre TR/EN alanları tercih et, yoksa ana Title/Description
        var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        string title = (string)q.Title;
        string? desc = q.Description;
        if(culture == "tr")
        {
            title = !string.IsNullOrWhiteSpace(q.TitleTr) ? q.TitleTr! : title;
            desc = !string.IsNullOrWhiteSpace(q.DescriptionTr) ? q.DescriptionTr : desc;
        }
        else if(culture == "en")
        {
            title = !string.IsNullOrWhiteSpace(q.TitleEn) ? q.TitleEn! : title;
            desc = !string.IsNullOrWhiteSpace(q.DescriptionEn) ? q.DescriptionEn : desc;
        }
        return new QuizCardViewModel
        {
            Id = q.Id,
            Title = title,
            Description = desc ?? string.Empty,
            Category = q.Category ?? string.Empty,
            CoverImageUrl = q.CoverImageUrl ?? string.Empty,
            CoverImageWidth = q.CoverImageWidth,
            CoverImageHeight = q.CoverImageHeight,
            Plays = q.Plays,
            Comments = 0,
            Reactions = 0,
            IsEditorPick = false,
            IsTrending = false,
            AuthorUserName = q.AuthorUserName ?? string.Empty,
            AuthorAvatarUrl = null,
            CreatedAt = q.CreatedAt,
            ItemsCount = q.Choices?.Count ?? 0,
            Tags = (q.QuizTags ?? new List<QuizTag>())
                .Where(qt => qt.Tag != null)
                .Select(qt => (string)qt.Tag!.Name)
                .ToArray()
        };
    }

    public static QuizDetailViewModel ToDetailViewModelBasic(this Quiz q)
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        string title = (string)q.Title;
        string? desc = q.Description;
        if(culture == "tr")
        {
            title = !string.IsNullOrWhiteSpace(q.TitleTr) ? q.TitleTr! : title;
            desc = !string.IsNullOrWhiteSpace(q.DescriptionTr) ? q.DescriptionTr : desc;
        }
        else if(culture == "en")
        {
            title = !string.IsNullOrWhiteSpace(q.TitleEn) ? q.TitleEn! : title;
            desc = !string.IsNullOrWhiteSpace(q.DescriptionEn) ? q.DescriptionEn : desc;
        }
        return new QuizDetailViewModel
        {
            Id = q.Id,
            Title = title,
            Description = desc ?? string.Empty,
            Category = q.Category ?? string.Empty,
            CoverImageUrl = q.CoverImageUrl ?? string.Empty,
            Plays = q.Plays,
            IsEditorPick = false,
            IsTrending = false,
            AuthorUserName = q.AuthorUserName ?? string.Empty,
            AuthorAvatarUrl = null,
            CreatedAt = q.CreatedAt,
            Tags = (q.QuizTags ?? new List<QuizTag>())
                .Where(qt => qt.Tag != null)
                .Select(qt => (string)qt.Tag!.Name)
                .ToArray(),
            Choices = (q.Choices ?? new List<QuizChoice>())
                .OrderBy(c => c.Order)
                .Select(c => new QuizChoiceViewModel
                {
                    Id = c.Id,
                    ImageUrl = c.ImageUrl,
                    ImageWidth = c.ImageWidth,
                    ImageHeight = c.ImageHeight,
                    YoutubeUrl = c.YoutubeUrl,
                    Caption = c.Caption,
                    Order = c.Order,
                    Picks = 0,
                    Matches = 0,
                    Wins = 0,
                    Champions = 0,
                    Percent = 0,
                    ChampionRate = 0,
                    WinRate = 0
                })
                .ToList()
        };
    }

    public static void ApplyScalarsFromViewModel(this Quiz q, QuizDetailViewModel vm)
    {
        q.Title = vm.Title ?? string.Empty;
        q.Description = vm.Description;
        q.Category = vm.Category ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(vm.CoverImageUrl)) q.CoverImageUrl = vm.CoverImageUrl;
        if (vm.CoverImageWidth.HasValue) q.CoverImageWidth = vm.CoverImageWidth;
        if (vm.CoverImageHeight.HasValue) q.CoverImageHeight = vm.CoverImageHeight;
        q.IsPublic = vm.IsPublic;
    }

    public static List<QuizChoice> ToEntityChoices(this IEnumerable<QuizChoiceViewModel> vms, Guid quizId)
    {
        return (vms ?? Enumerable.Empty<QuizChoiceViewModel>())
            .Select(c => new QuizChoice
            {
                Id = c.Id == Guid.Empty ? Guid.NewGuid() : c.Id,
                QuizId = quizId,
                ImageUrl = string.IsNullOrWhiteSpace(c.ImageUrl) ? null : c.ImageUrl,
                ImageWidth = c.ImageWidth,
                ImageHeight = c.ImageHeight,
                YoutubeUrl = string.IsNullOrWhiteSpace(c.YoutubeUrl) ? null : c.YoutubeUrl,
                Caption = c.Caption,
                Order = c.Order
            })
            .OrderBy(x => x.Order)
            .ToList();
    }

    public static List<string> NormalizeTags(this IEnumerable<string>? tags)
    {
        return (tags ?? Enumerable.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
    }
}
