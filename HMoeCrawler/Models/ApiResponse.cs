using System.Text.Json.Serialization;

namespace HMoeCrawler.Models;

public record ApiResponse
{
    [JsonPropertyName("code")]
    public required int Code { get; init; }

    [JsonPropertyName("data")]
    public required ResponseData Data { get; init; }
}
