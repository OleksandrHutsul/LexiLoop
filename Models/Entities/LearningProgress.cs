using LexiLoop.Models.Enums;

namespace LexiLoop.Models.Entities;

public class LearningProgress
{
    public long Id { get; set; }
    public long VocabularyItemId { get; set; }
    public VocabularyItem VocabularyItem { get; set; } = null!;
    public int ForeignToTranslationLevel { get; set; }
    public int TranslationToForeignLevel { get; set; }
    public DateTimeOffset? ForeignToTranslationNextReviewAt { get; set; }
    public DateTimeOffset? TranslationToForeignNextReviewAt { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }
    public int ReviewCount { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public SrsCardState ForeignToTranslationState { get; set; }
    public double ForeignToTranslationStability { get; set; }
    public double ForeignToTranslationDifficulty { get; set; } = 5;
    public int ForeignToTranslationIntervalDays { get; set; }
    public int ForeignToTranslationReviewCount { get; set; }
    public int ForeignToTranslationLapseCount { get; set; }
    public DateTimeOffset? ForeignToTranslationLastReviewedAt { get; set; }
    public SrsCardState TranslationToForeignState { get; set; }
    public double TranslationToForeignStability { get; set; }
    public double TranslationToForeignDifficulty { get; set; } = 5;
    public int TranslationToForeignIntervalDays { get; set; }
    public int TranslationToForeignReviewCount { get; set; }
    public int TranslationToForeignLapseCount { get; set; }
    public DateTimeOffset? TranslationToForeignLastReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
