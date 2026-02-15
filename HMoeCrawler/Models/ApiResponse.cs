using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace HMoeCrawler.Models;

public record ApiResponse
{
    [JsonPropertyName("code")]
    public required int Code { get; init; }

    [JsonPropertyName("data")]
    public required JsonElement Data { get; init; }

    [JsonPropertyName("msg")]
    public required string Message { get; init; }

    public T GetData<T>(JsonTypeInfo<T> info)
    {
        if (Code is not 0)
            throw new InvalidOperationException($"API returned error code {Code}: {Message}");
        return Data.Deserialize(info) ?? throw new InvalidOperationException("Failed to deserialize API response data.");
    }
}
