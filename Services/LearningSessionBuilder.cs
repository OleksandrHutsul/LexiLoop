using System.Globalization;
using System.Text;
using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;
using LexiLoop.Services.Models;

namespace LexiLoop.Services;

public class LearningSessionBuilder
{
    internal const int SimilarCardSeparationWindow = 4;

    private static readonly HashSet<string> Articles = new(StringComparer.Ordinal)
    {
        "der", "die", "das", "den", "dem", "des", "ein", "eine", "einer", "einem", "einen"
    };

    public IReadOnlyList<SessionCardPlan> Build(IReadOnlyList<VocabularyItem> items, LearningMode mode, LearningSessionKind kind, int newItemLimit, 
        DateTimeOffset now, Random? random = null)
    {
        random ??= Random.Shared;

        var due = new List<SessionCardCandidate>();
        var fresh = new List<SessionCardCandidate>();
        var processedIds = new HashSet<long>();

        foreach (var item in items)
        {
            if (!processedIds.Add(item.Id))
                continue;

            ReviewDirection[] directions = mode switch
            {
                LearningMode.ForeignToTranslation => [ReviewDirection.ForeignToTranslation],
                LearningMode.TranslationToForeign => [ReviewDirection.TranslationToForeign],
                _ => [ReviewDirection.ForeignToTranslation, ReviewDirection.TranslationToForeign]
            };

            foreach (var direction in directions)
            {
                var state = GetState(item.Progress, direction);

                if (state.ReviewCount > 0 && state.NextReviewAt <= now)
                {
                    var priority = state.State is SrsCardState.Learning or SrsCardState.Relearning ? 0 : 1;
                    due.Add(Candidate(item, direction, priority, state.NextReviewAt));
                }
            }

            if (kind == LearningSessionKind.Review || fresh.Count >= newItemLimit)
                continue;

            var unseen = new List<ReviewDirection>();

            foreach (var direction in directions)
            {
                if (GetState(item.Progress, direction).ReviewCount == 0)
                    unseen.Add(direction);
            }

            if (unseen.Count > 0)
                fresh.Add(Candidate(item, unseen[random.Next(unseen.Count)], 2, null));
        }

        Shuffle(due, random);
        Shuffle(fresh, random);

        var candidates = new List<SessionCardCandidate>(due.Count + Math.Min(fresh.Count, newItemLimit));
        candidates.AddRange(due);

        for (var i = 0; i < fresh.Count && i < newItemLimit; i++)
            candidates.Add(fresh[i]);

        var separated = SeparateRelatedCards(candidates, random);
        var result = new List<SessionCardPlan>(separated.Count);

        foreach (var candidate in separated)
            result.Add(new SessionCardPlan(candidate.Item.Id, candidate.Direction));

        return result;
    }

    private static bool AreRelated(SessionCardCandidate left, SessionCardCandidate right)
    {
        return left.Item.Id == right.Item.Id || Similar(left.ForeignKey, right.ForeignKey) || Similar(left.TranslationKey, right.TranslationKey);
    }

    private static IReadOnlyList<SessionCardCandidate> SeparateRelatedCards(List<SessionCardCandidate> remaining, Random random)
    {
        var result = new List<SessionCardCandidate>(remaining.Count);

        while (remaining.Count > 0)
        {
            var priority = int.MaxValue;

            foreach (var candidate in remaining)
                priority = Math.Min(priority, candidate.Priority);

            var preferredDirection = result.Count == 0 ? (ReviewDirection?)null : Opposite(result[^1].Direction);
            var eligible = new List<SessionCardCandidate>();

            foreach (var candidate in remaining)
            {
                if (candidate.Priority == priority && !IsRelatedToRecent(candidate, result))
                    eligible.Add(candidate);
            }

            if (eligible.Count == 0)
            {
                foreach (var candidate in remaining)
                {
                    if (!IsRelatedToRecent(candidate, result))
                        eligible.Add(candidate);
                }
            }

            if (eligible.Count == 0)
            {
                foreach (var candidate in remaining)
                {
                    if (candidate.Priority == priority)
                        eligible.Add(candidate);
                }
            }

            var directionMatches = new List<SessionCardCandidate>();

            if (preferredDirection is not null)
            {
                foreach (var candidate in eligible)
                {
                    if (candidate.Direction == preferredDirection)
                        directionMatches.Add(candidate);
                }
            }

            var choices = directionMatches.Count > 0 ? directionMatches : eligible;
            var next = choices[random.Next(choices.Count)];

            result.Add(next);
            remaining.Remove(next);
        }

        return result;
    }

    private static bool IsRelatedToRecent(SessionCardCandidate candidate, IReadOnlyList<SessionCardCandidate> result)
    {
        var startIndex = Math.Max(0, result.Count - SimilarCardSeparationWindow);

        for (var i = startIndex; i < result.Count; i++)
        {
            if (AreRelated(candidate, result[i]))
                return true;
        }

        return false;
    }

    internal static CardReviewState GetState(LearningProgress progress, ReviewDirection direction)
    {
        if (direction == ReviewDirection.ForeignToTranslation)
        {
            return new(progress.ForeignToTranslationState, progress.ForeignToTranslationStability, progress.ForeignToTranslationDifficulty, progress.ForeignToTranslationIntervalDays,
                progress.ForeignToTranslationReviewCount, progress.ForeignToTranslationLapseCount, progress.ForeignToTranslationLastReviewedAt, progress.ForeignToTranslationNextReviewAt);
        }

        return new(progress.TranslationToForeignState, progress.TranslationToForeignStability, progress.TranslationToForeignDifficulty, progress.TranslationToForeignIntervalDays,
            progress.TranslationToForeignReviewCount, progress.TranslationToForeignLapseCount, progress.TranslationToForeignLastReviewedAt, progress.TranslationToForeignNextReviewAt);
    }

    private static ReviewDirection Opposite(ReviewDirection direction)
    {
        return direction == ReviewDirection.ForeignToTranslation
            ? ReviewDirection.TranslationToForeign
            : ReviewDirection.ForeignToTranslation;
    }

    private static bool Similar(string left, string right)
    {
        if (left.Length < 3 || right.Length < 3)
            return false;

        if (left == right || left.Contains(right, StringComparison.Ordinal) || right.Contains(left, StringComparison.Ordinal))
            return true;

        var common = 0;
        var maxCommon = Math.Min(left.Length, right.Length);

        while (common < maxCommon && left[common] == right[common])
            common++;

        return common >= 4 && common >= maxCommon * 0.6;
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var clean = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
                clean.Append(character);
            else if (char.IsWhiteSpace(character))
                clean.Append(' ');
        }

        var words = clean.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        clean.Clear();

        foreach (var word in words)
        {
            if (!Articles.Contains(word))
                clean.Append(word);
        }

        return clean.ToString();
    }

    private static SessionCardCandidate Candidate(VocabularyItem item, ReviewDirection direction, int priority, DateTimeOffset? dueAt)
    {
        return new(item, direction, priority, dueAt, Normalize(item.ForeignText), Normalize(item.Translation));
    }

    private static void Shuffle<T>(IList<T> values, Random random)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
