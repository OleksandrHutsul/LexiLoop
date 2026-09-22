using LexiLoop.Models.Enums;
using LexiLoop.Services.Interfaces;
using LexiLoop.Services.Models;

namespace LexiLoop.Services;

public class FsrsReviewScheduler : IReviewScheduler
{
    private const double DesiredRetention = 0.90;
    private static readonly double[] InitialStability = [0.40255, 1.18385, 3.173, 15.69105];

    public ReviewSchedule Schedule(CardReviewState current, ReviewResult result, DateTimeOffset now)
    {
        now = now.ToUniversalTime();

        var rating = (int)result + 1;
        var difficulty = InitialDifficulty(rating);

        if (current.ReviewCount == 0 || current.Stability <= 0)
        {
            var stability = InitialStability[rating - 1];

            return result switch
            {
                ReviewResult.Again => Create(SrsCardState.Learning, stability, difficulty, 0, current.ReviewCount + 1, current.LapseCount + 1, now, now.AddMinutes(10)),
                ReviewResult.Hard => Create(SrsCardState.Learning, stability, difficulty, 0, current.ReviewCount + 1, current.LapseCount, now, now.AddHours(8)),
                _ => WithInterval(SrsCardState.Review, stability, difficulty, current.ReviewCount + 1, current.LapseCount, now)
            };
        }

        var elapsedDays = Math.Max(0, (now - (current.LastReviewedAt ?? now)).TotalDays);
        var retrievability = Math.Pow(1 + elapsedDays / (9 * Math.Max(0.1, current.Stability)), -1);
        difficulty = NextDifficulty(current.Difficulty, rating);

        if (result == ReviewResult.Again)
        {
            var relearningStability = 1.9395 * Math.Pow(difficulty, -0.11) * (Math.Pow(current.Stability + 1, 0.29605) - 1) * Math.Exp((1 - retrievability) * 2.2698);

            relearningStability = Math.Clamp(relearningStability, 0.1, Math.Max(0.1, current.Stability * 0.75));

            return Create(SrsCardState.Relearning, relearningStability, difficulty, 0, current.ReviewCount + 1, current.LapseCount + 1, now, now.AddMinutes(10));
        }

        var hardPenalty = result == ReviewResult.Hard ? 0.2315 : 1.0;
        var easyBonus = result == ReviewResult.Easy ? 2.9898 : 1.0;
        var growth = Math.Exp(1.54575) * (11 - difficulty) * Math.Pow(current.Stability, -0.1192) * (Math.Exp((1 - retrievability) * 1.01925) - 1) * hardPenalty * easyBonus;
        var newStability = Math.Max(current.Stability + 0.1, current.Stability * (1 + growth));

        if (result == ReviewResult.Hard && current.State is SrsCardState.Learning or SrsCardState.Relearning)
            return Create(current.State, newStability, difficulty, 0, current.ReviewCount + 1, current.LapseCount, now, now.AddHours(12));

        return WithInterval(SrsCardState.Review, newStability, difficulty, current.ReviewCount + 1, current.LapseCount, now);
    }

    private static ReviewSchedule WithInterval(SrsCardState state, double stability, double difficulty, int reviewCount, int lapseCount, DateTimeOffset now)
    {
        var rawInterval = 9 * stability * (1 / DesiredRetention - 1);
        var interval = Math.Max(1, (int)Math.Round(rawInterval));

        return Create(state, stability, difficulty, interval, reviewCount, lapseCount, now, now.AddDays(interval));
    }

    private static ReviewSchedule Create(SrsCardState state, double stability, double difficulty, int intervalDays, int reviewCount, int lapseCount, 
        DateTimeOffset now, DateTimeOffset next)
    {
        return new(state, stability, Math.Clamp(difficulty, 1, 10), intervalDays, reviewCount, lapseCount, now, next.ToUniversalTime());
    }

    private static double InitialDifficulty(int rating)
    {
        return Math.Clamp(7.1949 - Math.Exp(0.5345 * (rating - 1)) + 1, 1, 10);
    }

    private static double NextDifficulty(double currentDifficulty, int rating)
    {
        var adjusted = currentDifficulty - 1.4604 * (rating - 3);
        var meanReverted = 0.0046 * InitialDifficulty(3) + 0.9954 * adjusted;

        return Math.Clamp(meanReverted, 1, 10);
    }
}
