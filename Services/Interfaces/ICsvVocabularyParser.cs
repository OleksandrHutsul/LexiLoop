using LexiLoop.Services.Models;

namespace LexiLoop.Services.Interfaces;

public interface ICsvVocabularyParser
{
    CsvDocument Parse(byte[] bytes);
    CsvMappedResult Map(CsvDocument document, CsvColumnMapping mapping, bool hasHeader);
}
