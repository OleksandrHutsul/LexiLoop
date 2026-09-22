using LexiLoop.Models.Enums;

namespace LexiLoop.Services.Models;

public record CollectionCreateResult(CollectionCreateStatus Status, string Name, long? CollectionId = null);
