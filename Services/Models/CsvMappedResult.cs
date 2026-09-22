namespace LexiLoop.Services.Models;

public record CsvMappedResult(IReadOnlyList<VocabularyInput> Inputs, int TotalRows, int InvalidRows, int DuplicateRows, IReadOnlyList<string> Errors);
