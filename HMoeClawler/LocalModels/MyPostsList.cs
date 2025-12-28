using HMoeClawler.Models;

namespace HMoeClawler.LocalModels;

public record ReadPostsList
{
    public required int NewPostsCount { get; init; }

    public required LinkedList<Post> Posts { get; init; }
}

public record WritePostsList
{
    public required int NewPostsCount { get; init; }

    public required IEnumerable<Post> Posts { get; init; }
}
