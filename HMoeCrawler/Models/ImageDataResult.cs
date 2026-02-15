using System.Text.Json.Serialization;

namespace HMoeCrawler.Models;

public record ImageDataResult
{
    [JsonPropertyName("imgData")]
    public required string ImgData { get; init; }
}

