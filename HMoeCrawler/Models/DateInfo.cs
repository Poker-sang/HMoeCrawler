using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HMoeCrawler.Models;

public record struct DateInfo
{
    [JsonPropertyName("timestamp")]
    public required long Timestamp { get; init; }

    [JsonPropertyName("timestampUtc")]
    public required long TimestampUtc { get; init; }

    [JsonPropertyName("full")]
    public required string Full { get; init; }

    [JsonPropertyName("human")]
    public required string Human { get; init; }

    [JsonPropertyName("short")]
    public required string Short { get; init; }

    public DateTimeOffset ToDateTimeOffset()
    {
        var offsetSeconds = Timestamp - TimestampUtc;
        var offsetHours = Math.Round(offsetSeconds / 3600.0);
        var offset = TimeSpan.FromHours(offsetHours);
        var ticks = (Timestamp * TimeSpan.TicksPerSecond) + DateTime.UnixEpoch.Ticks;
        return new DateTimeOffset(ticks, offset);
    }
}

public class DateInfoToDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateInfo = JsonSerializer.Deserialize(ref reader, new DateInfoSerializerContext(new(options)).DateInfo);
        return dateInfo.ToDateTimeOffset();
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        var localDateTime = value.ToLocalTime();
        writer.WriteNumber("timestamp", localDateTime.ToUnixTimeSeconds());
        writer.WriteNumber("timestampUtc", value.ToUniversalTime().ToUnixTimeSeconds());
        writer.WriteString("full", localDateTime.ToString("yyyy-MM-dd HH:mm:ss"));
        writer.WriteString("human", GetHumanReadableTime(value));
        writer.WriteString("short", localDateTime.ToString("yyyy-MM-dd"));
        writer.WriteEndObject();
    }

    private static string GetHumanReadableTime(DateTimeOffset date)
    {
        var now = DateTimeOffset.Now;
        var diff = now - date;

        if (diff.TotalMinutes < 1)
            return "刚刚";
        if (diff.TotalMinutes < 60)
            return $"{(int) diff.TotalMinutes}分钟前";
        if (diff.TotalHours < 24)
            return $"{(int) diff.TotalHours}小时前";
        if (diff.TotalDays < 30)
            return $"{(int) diff.TotalDays}天前";
        if (diff.TotalDays < 365)
            return $"{(int) (diff.TotalDays / 30)}个月前";
        return $"{(int) (diff.TotalDays / 365)}年前";
    }
}

[JsonSerializable(typeof(DateInfo))]
public partial class DateInfoSerializerContext : JsonSerializerContext;
