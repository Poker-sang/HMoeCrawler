using System.Text.Json.Serialization;

namespace HMoeClawler.Models;

public record Author
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("uid")]
    public required string Uid { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("url")]
    public required Uri Url { get; init; }

    [JsonPropertyName("avatarUrl")]
    public required Uri AvatarUrl { get; init; }

    [JsonPropertyName("des")]
    public required string Des { get; init; }

    [JsonPropertyName("commentsCount")]
    public required int CommentsCount { get; init; }

    [JsonPropertyName("postsCount")]
    public required int PostsCount { get; init; }

    [JsonPropertyName("point")]
    public required int Point { get; init; }

    [JsonPropertyName("medals")]
    public required IReadOnlyList<Medal> Medals { get; init; }

    [JsonPropertyName("followersCount")]
    public required int FollowersCount { get; init; }

    [JsonPropertyName("fansCount")]
    public required int FansCount { get; init; }

    [JsonPropertyName("role")]
    public required Role Role { get; init; }
}
