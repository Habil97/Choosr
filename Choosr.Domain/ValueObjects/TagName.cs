namespace Choosr.Domain.ValueObjects;

// Lightweight value object for Tag names with centralized validation
public readonly record struct TagName
{
    public string Value { get; }

    private TagName(string value)
    {
        Value = value;
    }

    public static TagName Create(string? input)
    {
        var normalized = Normalize(input);
        Validate(normalized);
        return new TagName(normalized);
    }

    private static string Normalize(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        // Collapse internal whitespace to single space
    t = System.Text.RegularExpressions.Regex.Replace(t, @"\s{2,}", " ");
        return t;
    }

    private static void Validate(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            throw new ArgumentException("TagName cannot be empty.");
        if (s.Length > 100)
            throw new ArgumentException("TagName is too long (max 100).");
        // Disallow control characters
        if (s.Any(char.IsControl))
            throw new ArgumentException("TagName contains invalid characters.");
    }

    public override string ToString() => Value;

    public static implicit operator string(TagName name) => name.Value;
    public static implicit operator TagName(string input) => Create(input);
}
