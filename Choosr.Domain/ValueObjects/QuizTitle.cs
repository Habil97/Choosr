namespace Choosr.Domain.ValueObjects;

// Value object for Quiz titles with centralized validation and normalization
public readonly record struct QuizTitle
{
    public string Value { get; }

    private QuizTitle(string value)
    {
        Value = value;
    }

    public static QuizTitle Create(string? input)
    {
        var normalized = Normalize(input);
        Validate(normalized);
        return new QuizTitle(normalized);
    }

    private static string Normalize(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        // Collapse consecutive whitespace to a single space
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\s{2,}", " ");
        return t;
    }

    private static void Validate(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            throw new ArgumentException("QuizTitle cannot be empty.");
        if (s.Length < 3)
            throw new ArgumentException("QuizTitle is too short (min 3).");
        if (s.Length > 200)
            throw new ArgumentException("QuizTitle is too long (max 200).");
        if (s.Any(char.IsControl))
            throw new ArgumentException("QuizTitle contains invalid characters.");
    }

    public override string ToString() => Value;

    public static implicit operator string(QuizTitle title) => title.Value;
    public static implicit operator QuizTitle(string input) => Create(input);
}
