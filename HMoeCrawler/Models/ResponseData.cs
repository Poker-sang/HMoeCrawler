using System.Text.Json.Serialization;

namespace HMoeCrawler.Models;

public record ResponseData
{
    [JsonPropertyName("posts")]
    public required Stack<Post> Posts { get; init; }
}
