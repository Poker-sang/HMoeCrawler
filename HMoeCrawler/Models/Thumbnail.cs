using System;
using System.Text.Json.Serialization;

namespace HMoeCrawler.Models;

public record Thumbnail
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("width")]
    public required string Width { get; init; }

    [JsonPropertyName("height")]
    public required string Height { get; init; }

    [JsonPropertyName("url")]
    public required Uri Url { get; init; }

    [JsonPropertyName("visible")]
    public required bool Visible { get; init; }
}
