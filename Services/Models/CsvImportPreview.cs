namespace LexiLoop.Services.Models;

public record CsvImportPreview(int TotalRows, int ValidRows, int InvalidRows, int FileDuplicateRows, int ExistingRows, IReadOnlyList<VocabularyInput> NewItems, 
    IReadOnlyList<VocabularyInput> SampleItems, IReadOnlyList<string> Errors);
