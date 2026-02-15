using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HMoeCrawler.Models;

public record PostsSearchResult
{
    [JsonPropertyName("posts")]
    public required Stack<Post> Posts { get; init; }
}
