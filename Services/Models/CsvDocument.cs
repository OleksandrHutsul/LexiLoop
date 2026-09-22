namespace LexiLoop.Services.Models;

public record CsvDocument(IReadOnlyList<string[]> Rows, string DelimiterName, bool HasHeader, bool HeaderDecisionRequired, CsvColumnMapping? DetectedMapping, string? Error)
{
    public int ColumnCount => Rows.Count == 0 ? 0 : Rows.Max(x => x.Length);
}
