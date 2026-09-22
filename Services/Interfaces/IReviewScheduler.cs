using LexiLoop.Models.Enums;
using LexiLoop.Services.Models;

namespace LexiLoop.Services.Interfaces;

public interface IReviewScheduler
{
    ReviewSchedule Schedule(CardReviewState current, ReviewResult result, DateTimeOffset now);
}
