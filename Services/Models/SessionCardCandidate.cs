using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;

namespace LexiLoop.Services.Models;

public record SessionCardCandidate(VocabularyItem Item, ReviewDirection Direction, int Priority, DateTimeOffset? DueAt, string ForeignKey, string TranslationKey);
