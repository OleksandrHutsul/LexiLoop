namespace LexiLoop.Models.Entities;

public class VocabularyItemCollection
{
    public long VocabularyItemId { get; set; }
    public VocabularyItem VocabularyItem { get; set; } = null!;
    public long CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;
}
