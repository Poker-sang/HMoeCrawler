using System.Text.Json.Serialization;
using HMoeClawler.Models;

namespace HMoeClawler.LocalModels;

public record MyPostsList
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenReading)]
    public int NewPostsCount { get; set; }

    public required LinkedList<Post> Posts { get; init; }
}
