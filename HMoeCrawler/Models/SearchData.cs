using System.IO;
using System.Text.Json.Serialization;

namespace HMoeCrawler.Models;

public record SearchData()
{
    public SearchData(int paged) : this()
    {
        Paged = paged;
    }

    /// <summary>
    /// Start from 1
    /// </summary>
    [JsonPropertyName("paged")]
    public int Paged
    {
        get;
        set
        {
            if (value < 1)
                throw new InvalidDataException("Paged must be greater than or equal to 1.");
            field = value;
        }
    }

    [JsonPropertyName("kw")]
    public string KeyWord { get; init; } = "";

    [JsonPropertyName("tags")]
    public string[] Tags { get; init; } = [];

    [JsonPropertyName("cat")]
    public string[] Cat { get; init; } = [];

    [JsonPropertyName("cats")]
    public string[] Cats { get; init; } = [];
}
