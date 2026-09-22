using LexiLoop.Models.Enums;

namespace LexiLoop.Models.Entities;

public class LearningSessionCard
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public LearningSession Session { get; set; } = null!;
    public long VocabularyItemId { get; set; }
    public VocabularyItem VocabularyItem { get; set; } = null!;
    public ReviewDirection Direction { get; set; }
    public int Position { get; set; }
    public bool Completed { get; set; }
    public ReviewResult? Result { get; set; }
    public bool? TypedAnswerCorrect { get; set; }
    public string? EnteredAnswer { get; set; }
    public string? ExpectedAnswer { get; set; }
}
