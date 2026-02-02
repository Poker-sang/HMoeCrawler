using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace HMoeCrawler.Models;

public record Post
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("url")]
    public required Uri Url { get; init; }

    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    [JsonPropertyName("commentsCount")]
    public required int CommentsCount { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("thumbnail")]
    public required Thumbnail Thumbnail { get; init; }

    [JsonPropertyName("author")]
    public required Author Author { get; init; }

    [JsonPropertyName("tags")]
    public required IReadOnlyList<Tag> Tags { get; init; }

    [JsonPropertyName("cats")]
    public required IReadOnlyList<Category> Cats { get; init; }

    [JsonPropertyName("excerpt")]
    public required string Excerpt { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("date")]
    [JsonConverter(typeof(DateInfoToDateTimeOffsetConverter))]
    public required DateTimeOffset Date { get; init; }

    [JsonPropertyName("modifiedDate")]
    [JsonConverter(typeof(DateInfoToDateTimeOffsetConverter))]
    public required DateTimeOffset ModifiedDate { get; init; }

    [JsonPropertyName("views")]
    public required int Views { get; init; }

    public string ThumbnailFileName => Id + Path.GetExtension(Thumbnail.Url.AbsolutePath);
}
