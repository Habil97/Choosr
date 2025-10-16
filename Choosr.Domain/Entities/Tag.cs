using Choosr.Domain.ValueObjects;

namespace Choosr.Domain.Entities;

public class Tag
{
    public Guid Id { get; set; }
    public TagName Name { get; set; } = new TagName();
    public ICollection<QuizTag> QuizTags { get; set; } = new List<QuizTag>();
}
