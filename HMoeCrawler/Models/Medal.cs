using System.Text.Json.Serialization;

namespace HMoeCrawler.Models;

public record Medal
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("imgUrl")]
    public required Uri ImgUrl { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("des")]
    public required string Des { get; init; }

    [JsonPropertyName("attrTitle")]
    public required string AttrTitle { get; init; }
}
