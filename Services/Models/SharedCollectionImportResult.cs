using LexiLoop.Models.Enums;

namespace LexiLoop.Services.Models;

public record SharedCollectionImportResult(SharedCollectionImportStatus Status, long? CollectionId = null, string? Name = null, int AddedItems = 0, int ReusedItems = 0);
