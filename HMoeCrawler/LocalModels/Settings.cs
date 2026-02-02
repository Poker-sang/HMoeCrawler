namespace HMoeCrawler.LocalModels;

public record Settings
{
    /// <summary>
    /// 是否为新会话
    /// <see langword="true"/>时，将读到的NewPostsCount清零，并从头开始计数新项目
    /// <see langword="false"/>时，继续在已有NewPostsCount基础上计数新项目
    /// </summary>
    public required bool NewSession { get; init; }

    public required string Cookies { get; init; }

    public required string NonceParam { get; init; }
}
