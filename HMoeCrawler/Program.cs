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
    @"D:\HMoeLogger";
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

await using (var fs = File.OpenAsyncRead(loggerSettingsPath, FileMode.Open))
{
    try
    {
        if (await JsonSerializer.DeserializeAsync(fs, SerializerContext.DefaultOverride.Settings) is { } s)
            settings = s;
    }
    catch (Exception e)
    {
        WriteException(e);
    }
}

if (settings is null)
    throw new InvalidDataException("Invalid settings " + loggerSettingsPath);

using var session = new HMoeSession();
await session.SetCookieAsync(settings.Cookies);
session.OnLoginRequired += () => (settings.Email, settings.Password);
session.CookieRefreshed += async (_, cookie) =>
{
    settings.Cookies = cookie;
    await using var fs = File.OpenAsyncWrite(loggerSettingsPath, FileMode.Create);
    await JsonSerializer.SerializeAsync(fs, settings, SerializerContext.DefaultOverride.Settings);
    Console.WriteLine("\e[32msettings.json updated\e[0m");
};

var postIdSet = new HashSet<int>(1000);
LinkedList<Post>? postList = null;
// 上次的项目数
var originalCount = 0;
// 本次新项目数
var newPostsCount = 0;

if (File.Exists(loggerJsonPath))
{
    await using var fs = File.OpenAsyncRead(loggerJsonPath, FileMode.Open);
    if (await JsonSerializer.DeserializeAsync(fs, SerializerContext.DefaultOverride.ReadPostsList) is { } r)
    {
        postList = r.Posts;
        if (!settings.NewSession)
            originalCount = r.PostsCount;
        foreach (var post in postList)
            if (postIdSet.Add(post.Id))
                session.DownloadThumbnailAddToList(post, loggerImgPath);
    }
}

postList ??= [];

var continuousExistence = 0;
var data = new SearchData(1);
while (true)
{
    var tempPosts = await session.SearchPageAsync(data);

    while (tempPosts.TryPop(out var post))
        if (postIdSet.Add(post.Id))
        {
            Console.WriteLine($"New Item [{post.Id}]: {post.Url}");
            if (continuousExistence < continuousExistenceThreshold)
                continuousExistence = 0;
            postList.AddFirst(post);
            newPostsCount++;
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

Console.Write("\e[32mGet ");
// 本次的总项目数 = 上次的项目数 + 本次新项目数（如果是新会话则不加上次的项目数）
var allPostsCount = settings.NewSession
    ? newPostsCount
    : originalCount + newPostsCount; 
if (settings.NewSession)
    Console.Write(newPostsCount);
else
    Console.Write(originalCount + " + " + newPostsCount);
Console.WriteLine(" new items\e[0m");

if (newPostsCount is 0)
{
    Console.WriteLine("Not save for no new items");
}
else
{
    var resultPosts = postList.OrderByDescending(t => t.Date);
    var myList = new WritePostsList
    {
        PostsCount = allPostsCount,
        Posts = resultPosts.Take(allPostsCount + (continuousExistenceThreshold * 4))
    };

    try
    {
        if (File.Exists(loggerJsonPath))
        {
            if (File.Exists(loggerLastJsonPath))
                File.Delete(loggerLastJsonPath);
            File.Move(loggerJsonPath, loggerLastJsonPath);
        }

        Console.WriteLine("Saving " + loggerJsonPath);
        await using var fs = File.OpenAsyncWrite(loggerJsonPath, FileMode.CreateNew);
        await JsonSerializer.SerializeAsync(fs, myList, SerializerContext.DefaultOverride.WritePostsList);
    }
    catch (Exception e)
    {
        WriteException(e);
        var fileName = $"TempLog {DateTime.Now:yyyy.MM.dd HH-mm-ss}.json";
        Console.WriteLine($"\e[31mSave failed. Backing up {fileName}\e[0m");
        var loggerTempJsonPath = Path.Combine(loggerPath, fileName);
        await using var fs = File.OpenAsyncWrite(loggerTempJsonPath, FileMode.CreateNew);
        await JsonSerializer.SerializeAsync(fs, myList, SerializerContext.DefaultOverride.WritePostsList);
    }
}

Console.ReadKey();

return;

static void WriteException(Exception e) => Console.WriteLine($"\e[90m{e.Message}\e[0m");
