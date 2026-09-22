using LexiLoop.Models.Enums;

namespace LexiLoop.Services.Models;

public record VocabularyInput(string ForeignText, string Translation, string? Pronunciation, VocabularyItemType Type);
