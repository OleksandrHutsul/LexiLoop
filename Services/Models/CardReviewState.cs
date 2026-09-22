using LexiLoop.Models.Enums;

namespace LexiLoop.Services.Models;

public record CardReviewState(SrsCardState State, double Stability, double Difficulty, int IntervalDays,int ReviewCount, int LapseCount, 
    DateTimeOffset? LastReviewedAt, DateTimeOffset? NextReviewAt);
