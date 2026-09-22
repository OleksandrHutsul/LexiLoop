using LexiLoop.Services.Models;

namespace LexiLoop.Bot.Models;

public class PendingCsvImportModel
{
    public string Token { get; }
    public CsvDocument Document { get; }
    public bool? HasHeader { get; set; }
    public int? ForeignIndex { get; set; }
    public int? TranslationIndex { get; set; }
    public CsvImportPreview? Preview { get; set; }

    public PendingCsvImportModel(string token, CsvDocument document)
    {
        Token = token;
        Document = document;
    }
}
