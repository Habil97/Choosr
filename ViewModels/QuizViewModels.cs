namespace Choosr.Web.ViewModels;

public class QuizCardViewModel
{
	public Guid Id { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string CoverImageUrl { get; set; } = string.Empty;
	// CLS azaltımı için opsiyonel kapak ölçüleri
	public int? CoverImageWidth { get; set; }
	public int? CoverImageHeight { get; set; }
	public int Plays { get; set; }
	public int Comments { get; set; }
	public int Reactions { get; set; }
	public bool IsEditorPick { get; set; }
	public bool IsTrending { get; set; }
	public string AuthorUserName { get; set; } = string.Empty;
	public string? AuthorAvatarUrl { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public int ItemsCount { get; set; }
	public IEnumerable<string> Tags { get; set; } = new List<string>();
}

public class QuizDetailViewModel : QuizCardViewModel
{
	public IEnumerable<QuizChoiceViewModel> Choices { get; set; } = new List<QuizChoiceViewModel>();
	public bool IsPublic { get; set; } = true;
	public IEnumerable<string>? LastWinners { get; set; } = new List<string>();
}

public class CommentViewModel
{
	public Guid Id { get; set; }
	public Guid QuizId { get; set; }
	public string UserId { get; set; } = string.Empty;
	public string UserName { get; set; } = string.Empty;
	public string Text { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class QuizChoiceViewModel
{
	public Guid Id { get; set; }
	public string? ImageUrl { get; set; }
	// CLS azaltımı için opsiyonel görsel ölçüleri
	public int? ImageWidth { get; set; }
	public int? ImageHeight { get; set; }
	public string? YoutubeUrl { get; set; }
	public string? Caption { get; set; }
	public int Order { get; set; }
	// İstatistikler
	public int Picks { get; set; }
	public double Percent { get; set; }
	public int Matches { get; set; }
	public int Wins { get; set; }
	public int Champions { get; set; }
	public double ChampionRate { get; set; }
	public double WinRate { get; set; }
}

public class CreateQuizDetailsStepViewModel
{
	public string PrimaryLanguage { get; set; } = "tr";
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public bool IsAnonymous { get; set; }
	public string Visibility { get; set; } = "public"; // public | unlisted
	public List<string> Tags { get; set; } = new();
}

public class CreateQuizChoicesStepViewModel
{
	public string Mode { get; set; } = "images"; // images | videos
	public List<QuizChoiceViewModel> Choices { get; set; } = new();
	public bool HideCaptions { get; set; }
}

public class CreateQuizResultStepViewModel
{
	public Guid QuizId { get; set; }
	public bool Success { get; set; }
	public string? Message { get; set; }
}

