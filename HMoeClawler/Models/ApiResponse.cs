using System.Text.Json.Serialization;

namespace HMoeClawler.Models;

public record ApiResponse
{
    [JsonPropertyName("code")]
    public required int Code { get; init; }

    [JsonPropertyName("data")]
    public required ResponseData Data { get; init; }
}
