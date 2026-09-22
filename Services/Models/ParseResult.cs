namespace LexiLoop.Services.Models;

public record ParseResult(IReadOnlyList<VocabularyInput> Valid, IReadOnlyList<string> Errors);
