using LexiLoop.Models.Entities;

namespace LexiLoop.Services.Models;

public record TypedAnswerResult(LearningSession Session, string EnteredAnswer, string ExpectedAnswer, bool IsCorrect, int PreviousMessageId);
