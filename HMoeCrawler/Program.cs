using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HMoeCrawler;
using HMoeCrawler.LocalModels;
using HMoeCrawler.Models;

// 连续获取到n个已存在的项目后，停止爬取
const int continuousExistenceThreshold = 5;
// 网站每页项目数（20）
// const int itemsPerPage = 20;
Settings? settings = null;

// 记录日志路径
var loggerPath =
#if DEBUG
    @"D:\HMoeCrawler";
#else
    Environment.CurrentDirectory;
#endif
var loggerImgPath = Path.Combine(loggerPath, "img");
var loggerJsonPath = Path.Combine(loggerPath, "current.json");
var loggerLastJsonPath = Path.Combine(loggerPath, "last.json");
var loggerSettingsPath = Path.Combine(loggerPath, "settings.json");

_ = Directory.CreateDirectory(loggerImgPath);

if (!File.Exists(loggerSettingsPath))
    throw new("Missing Settings in " + loggerSettingsPath);

// {
//     "NewSession": bool,
//     "Email": "...",
//     "Password": "...",
//     "Cookies": "..." // Optional
// }

try
{
    settings = await JsonSerializer.OpenDeserializeAsync(loggerSettingsPath, SerializerContext.DefaultOverride.Settings);
}
catch (Exception e)
{
    WriteException(e);
}

if (settings is null)
    throw new InvalidDataException("Invalid settings " + loggerSettingsPath);

using var session = new HMoeSession();
await session.SetCookieAsync(settings.Cookies);
session.OnLoginRequired += () => (settings.Email, settings.Password);
session.CookieRefreshed += async (_, cookie) =>
{
    settings.Cookies = cookie;
    await JsonSerializer.CreateSerializeAsync(loggerSettingsPath, settings, SerializerContext.DefaultOverride.Settings);
    Console.WriteLine("\e[32msettings.json updated\e[0m");
};

HashSet<Post>? postSet = null;

if (File.Exists(loggerJsonPath)
    && await JsonSerializer.OpenDeserializeAsync(loggerJsonPath, SerializerContext.DefaultOverride.HashSetPost) is { } r)
{
    postSet = r;
    foreach (var post in postSet)
    {
        session.DownloadThumbnailAddToList(post, loggerImgPath);
        if (settings.NewSession)
            post.IsNew = false;
    }   
}

postSet ??= [];

var newItemsCount = 0;
var continuousExistence = 0;
var data = new SearchData(1);
while (true)
{
    var tempPosts = await session.SearchPageAsync(data);

    while (tempPosts.TryPop(out var post))
        if (postSet.Add(post))
        {
            Console.WriteLine($"New Item [{post.Id}]: {post.Url}");
            newItemsCount++;
            if (continuousExistence < continuousExistenceThreshold)
                continuousExistence = 0;
            session.DownloadThumbnailAddToList(post, loggerImgPath);
        }
        else
        {
            Console.WriteLine($"Item existed: {post.Id} Continuous existence count: {continuousExistence}");
            continuousExistence++;
        }

    if (continuousExistence >= continuousExistenceThreshold)
        break;

    data.Paged++;
}

Console.WriteLine("\e[32mReached continuous existence threshold. Stopping crawl. Waiting for the thumbnail download task to complete\e[0m");

await session.WhenAllDownloadAsync();

if (newItemsCount is 0)
{
    Console.WriteLine("Not save for no new items");
}
else
{
    var resultPosts = postSet.OrderByDescending(t => t.Date).ToList();
    Console.Write("\e[32mGet ");
    // 本次的总项目数 = 上次的项目数 + 本次新项目数（如果是新会话则不加上次的项目数）
    var allPostsCount = settings.NewSession
        ? newItemsCount
        : resultPosts.Count(t => t.IsNew);
    if (!settings.NewSession)
        Console.Write(allPostsCount - newItemsCount + " + ");
    Console.WriteLine($"{newItemsCount} new items\e[0m");

    var myList = resultPosts.Take(allPostsCount + (continuousExistenceThreshold * 4)).ToArray();

    try
    {
        if (File.Exists(loggerJsonPath))
            File.Move(loggerJsonPath, loggerLastJsonPath, true);

        Console.WriteLine("Saving " + loggerJsonPath);
        await JsonSerializer.CreateSerializeAsync(loggerJsonPath, myList, SerializerContext.DefaultOverride.IReadOnlyListPost);
    }
    catch (Exception e)
    {
        WriteException(e);
        var fileName = $"TempLog {DateTime.Now:yyyy.MM.dd HH-mm-ss}.json";
        Console.WriteLine($"\e[31mSave failed. Backing up {fileName}\e[0m");
        var loggerTempJsonPath = Path.Combine(loggerPath, fileName);
        await JsonSerializer.CreateSerializeAsync(loggerTempJsonPath, myList, SerializerContext.DefaultOverride.IReadOnlyListPost);
    }
}

Console.ReadKey();

return;

static void WriteException(Exception e) => Console.WriteLine($"\e[90m{e.Message}\e[0m");
