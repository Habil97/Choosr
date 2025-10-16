namespace Choosr.Domain.Entities;

public class QuizChoiceStat
{
	public Guid Id { get; set; }
	public Guid QuizId { get; set; }
	public Guid ChoiceId { get; set; }
	// Toplam maçlara çıkma sayısı
	public int Matches { get; set; }
	// Toplam maç kazanma sayısı
	public int Wins { get; set; }
	// Toplam şampiyon olma sayısı
	public int Champions { get; set; }
	// Eski alan (geri uyumluluk için tutuluyor)
	public int Picks { get; set; }

	public Quiz Quiz { get; set; } = null!;
	public QuizChoice Choice { get; set; } = null!;
}

