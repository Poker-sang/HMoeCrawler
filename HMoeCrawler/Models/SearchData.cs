using System;
using System.IO;
using System.Text;
using System.Text.Json;
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

    public string Encode()
    {
        var u8Str = JsonSerializer.SerializeToUtf8Bytes(this, SerializerContext.DefaultOverride.SearchData);
        var str = Convert.ToBase64String(u8Str);
        return Uri.EscapeDataString(str);
    }

    public static SearchData? Decode(string data)
    {
        while (data.Contains('%'))
            data = Uri.UnescapeDataString(data);
        var u8Str = Encoding.UTF8.GetString(Convert.FromBase64String(data));
        return JsonSerializer.Deserialize(u8Str, SerializerContext.DefaultOverride.SearchData);
    }
}
