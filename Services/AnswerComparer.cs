using System.Text;
using LexiLoop.Services.Interfaces;

namespace LexiLoop.Services;

public class AnswerComparer : IAnswerComparer
{
    public bool AreEquivalent(string actual, string expected)
    {
        return string.Equals(Normalize(actual), Normalize(expected), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
