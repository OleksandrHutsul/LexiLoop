using LexiLoop.Services.Models;

namespace LexiLoop.Services.Interfaces;

public interface IVocabularyParser
{
    ParseResult ParseLines(string text);
    ParseResult ParseJson(string json);
}
