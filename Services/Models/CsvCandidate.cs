namespace LexiLoop.Services.Models;

public sealed record CsvCandidate(IReadOnlyList<string[]> Rows, string Name, int Score);
