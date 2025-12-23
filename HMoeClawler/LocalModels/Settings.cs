namespace HMoeClawler.LocalModels;

public record Settings
{
    public required string Cookies { get; init; }

    public required string NonceParam { get; init; }

    public string? Nonce { get; init; }
}
