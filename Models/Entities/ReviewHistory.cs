using LexiLoop.Models.Enums;

namespace LexiLoop.Models.Entities;

public class ReviewHistory
{
    public long Id { get; set; }
    public long VocabularyItemId { get; set; }
    public VocabularyItem VocabularyItem { get; set; } = null!;
    public ReviewDirection Direction { get; set; }
    public ReviewResult Result { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }
}
