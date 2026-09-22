namespace LexiLoop.Services.Interfaces;

public interface IAnswerComparer
{
    bool AreEquivalent(string actual, string expected);
}
