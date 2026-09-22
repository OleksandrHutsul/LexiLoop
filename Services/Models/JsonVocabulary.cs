using System.Text.Json.Serialization;

namespace LexiLoop.Services.Models;

public class JsonVocabulary
{
    [JsonPropertyName("word")]
    public string? Word { get; set; }

    [JsonPropertyName("translation")]
    public string? Translation { get; set; }

    [JsonPropertyName("pronunciation")]
    public string? Pronunciation { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
