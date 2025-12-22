using System.Text.Json.Serialization;

namespace HMoeClawler.Models;

public record ResponseData
{
    [JsonPropertyName("posts")]
    public required Stack<Post> Posts { get; init; }
}
