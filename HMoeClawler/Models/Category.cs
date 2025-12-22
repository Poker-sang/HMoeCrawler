using System.Text.Json.Serialization;

namespace HMoeClawler.Models;

public record Category
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("parentId")]
    public required int ParentId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    [JsonPropertyName("uuid")]
    public required string Uuid { get; init; }

    [JsonPropertyName("url")]
    public required Uri Url { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }
}
