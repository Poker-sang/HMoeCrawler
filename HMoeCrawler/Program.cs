using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using HMoeCrawler;
using HMoeCrawler.LocalModels;
using HMoeCrawler.Models;

// 每次请求时间间隔
var defaultCoolDown = TimeSpan.FromSeconds(3);
// 最大请求间隔，超过后中断
var coolDownThreshold = TimeSpan.FromMinutes(1);
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
//     "Cookies": "...",
//     "NonceParam": "action=...", // https://www.mhh1.com/wp-admin/admin-ajax.php?
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

using var client = new HttpClient();
client.Timeout = TimeSpan.FromSeconds(8);
client.DefaultRequestHeaders.Referrer = new("https://www.mhh1.com/search/-");
client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0");
_ = client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", settings.Cookies);

var postIdSet = new HashSet<int>(1000);
LinkedList<Post>? postList = null;
// 上次的项目数
var originalCount = 0;
// 本次新项目数
var newPostsCount = 0;
var imgTasks = new List<Task>();

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
                imgTasks.Add(DownloadThumbnail(client, post));
    }
}

postList ??= [];

Console.WriteLine("Fetching nonce ");
using var nonceJson = await client.GetAsync("https://www.mhh1.com/wp-admin/admin-ajax.php?" + settings.NonceParam);
_ = nonceJson.EnsureSuccessStatusCode();
using var jsonDocument = await JsonDocument.ParseAsync(nonceJson.Content.ReadAsStream());
var nonce = jsonDocument.RootElement.GetProperty("_nonce").GetString();

Console.WriteLine("Get nonce " + nonce);

var uri = "https://www.mhh1.com/wp-admin/admin-ajax.php"
          + "?_nonce=" + nonce
          + "&action=b9338a11fcc41c1ed5447625d1c0e743"
          + "&query=";

var continuousExistence = 0;
var data = new SearchData(1);
while (true)
{
    var coolDown = defaultCoolDown;
    Stack<Post> tempPosts;
    DateTime lastRequest;

    while (true)
        try
        {
            Console.WriteLine("Downloading page " + data.Paged);
            using var response = await client.GetAsync(uri + Encode(data));
            _ = response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();

            if (result.Contains("请登录后继续"))
            {
                Console.WriteLine("\e[31mCookies expired, please update cookies in Settings.json\e[0m");
                Console.WriteLine("Paste new cookie in HeaderString:");
                var newCookies = Console.ReadLine()?.Trim();
                if (!string.IsNullOrWhiteSpace(newCookies))
                {
                    settings = settings with { Cookies = newCookies };
                    await using var fs = File.OpenAsyncWrite(loggerSettingsPath, FileMode.Create);
                    await JsonSerializer.SerializeAsync(fs, settings, SerializerContext.DefaultOverride.Settings);
                    Console.WriteLine("\e[32mSettings.json updated. Restart the program\e[0m");
                }
                return;
            }

            if (await response.Content.ReadFromJsonAsync(SerializerContext.DefaultOverride.ApiResponse) is { Data.Posts: { } p })
            {
                Console.WriteLine("Downloaded page " + data.Paged);
                lastRequest = DateTime.UtcNow;
                coolDown = defaultCoolDown;
                tempPosts = p;
                break;
            }
        }
        catch (Exception e)
        {
            WriteException(e);
            if (coolDown > coolDownThreshold)
                Debugger.Break();
            coolDown *= 2;
            await Task.Delay(coolDown);
        }

    while (tempPosts.TryPop(out var post))
        if (postIdSet.Add(post.Id))
        {
            Console.WriteLine($"New Item [{post.Id}]: {post.Url}");
            if (continuousExistence < continuousExistenceThreshold)
                continuousExistence = 0;
            postList.AddFirst(post);
            newPostsCount++;
            imgTasks.Add(DownloadThumbnail(client, post));
        }
        else
        {
            Console.WriteLine($"Item existed: {post.Id} Continuous existence count: {continuousExistence}");
            continuousExistence++;
        }

    if (continuousExistence >= continuousExistenceThreshold)
        break;

    while (DateTime.UtcNow < lastRequest + coolDown)
        await Task.Delay(500);
    data.Paged++;
}

Console.WriteLine("\e[32mReached continuous existence threshold. Stopping crawl. Waiting for the thumbnail download task to complete\e[0m");

await Task.WhenAll(imgTasks);

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

async Task DownloadThumbnail(HttpClient httpClient, Post post)
{
    var postThumbnailUrl = post.Thumbnail.Url;
    var fileName = post.ThumbnailFileName;
    var imgPath = Path.Combine(loggerImgPath, post.ThumbnailFileName);
    if (File.Exists(imgPath))
        return;
    try
    {
        await using var stream = await httpClient.GetStreamAsync(postThumbnailUrl);
        await using var fileStream = File.OpenAsyncWrite(imgPath, FileMode.CreateNew);
        await stream.CopyToAsync(fileStream);
        Console.WriteLine("Downloaded thumbnail " + fileName);
    }
    catch (Exception e)
    {
        WriteException(e);
        Console.WriteLine($"Download thumbnail failed [{post.Id}]: {postThumbnailUrl} ({post.Url})");
        if (File.Exists(imgPath))
            File.Delete(imgPath);
    }
}

static string Encode(SearchData data)
{
    var u8Str = JsonSerializer.SerializeToUtf8Bytes(data, SerializerContext.DefaultOverride.SearchData);
    var str = Convert.ToBase64String(u8Str);
    return Uri.EscapeDataString(str);
}

static SearchData? Decode(string data)
{
    while (data.Contains('%'))
        data = Uri.UnescapeDataString(data);
    var u8Str = Encoding.UTF8.GetString(Convert.FromBase64String(data));
    return JsonSerializer.Deserialize<SearchData>(u8Str);
}

static void WriteException(Exception e) => Console.WriteLine($"\e[90m{e.Message}\e[0m");

[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(SearchData))]
[JsonSerializable(typeof(ApiResponse))]
[JsonSerializable(typeof(ReadPostsList))]
[JsonSerializable(typeof(WritePostsList))]
public partial class SerializerContext : JsonSerializerContext
{
    public static SerializerContext DefaultOverride => field ??= new(new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });
}
