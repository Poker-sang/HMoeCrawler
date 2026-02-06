using System.Collections.Generic;
using HMoeCrawler.Models;

namespace HMoeCrawler.LocalModels;

public record ReadPostsList
{
    public required int PostsCount { get; init; }

    public required LinkedList<Post> Posts { get; init; }
}

public record WritePostsList
{
    public required int PostsCount { get; init; }

    public required IEnumerable<Post> Posts { get; init; }
}
