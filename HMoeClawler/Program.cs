using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using HMoeClawler.LocalModels;
using HMoeClawler.Models;

// 是否为新会话
// true时，将读到的NewPostsCount清零，并从头开始计数新项目
// false时，继续在已有NewPostsCount基础上计数新项目
const bool newSession = true;
// 每次请求时间间隔
var defaultCoolDown = TimeSpan.FromSeconds(3);
// 最大请求间隔，超过后中断
var coolDownThreshold = TimeSpan.FromMinutes(1);
// 连续获取到n个已存在的项目后，停止爬取
const int continuousExistenceThreshold = 5;
// 网站每页项目数（20）
const int itemsPerPage = 20;
Settings settings = null!;

// 记录日志路径
const string loggerPath = @"D:\new\HMoeLogger";
const string loggerImgPath = loggerPath+ "\\img";
const string loggerJsonPath = loggerPath + "\\CurrLog.json";
const string loggerLastJsonPath = loggerPath+ "\\LastLog.json";
const string loggerSettingsPath = loggerPath+ "\\Settings.json";

_ = Directory.CreateDirectory(loggerImgPath);

if (!File.Exists(loggerSettingsPath))
    throw new("Missing Settings in " + loggerSettingsPath);

// {
//     "Cookies": "...",
//     "NonceParam": "action=...", // https://www.mhh1.com/wp-admin/admin-ajax.php?
//     "Nonce": "..." // Optional
// }

await using (var fs = OpenAsyncRead(loggerSettingsPath, FileMode.Open))
{
    try
    {
        if (await JsonSerializer.DeserializeAsync<Settings>(fs) is { } s)
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
client.DefaultRequestHeaders.Referrer = new("https://www.mhh1.com/search/-");
client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0");
_ = client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", settings.Cookies);

var postIdSet = new HashSet<int>(1000);
LinkedList<Post>? postList = null;
var originalCount = 0;
var newPostsCount = 0;

if (File.Exists(loggerJsonPath))
{
    await using var fs = OpenAsyncRead(loggerJsonPath, FileMode.Open);
    if (await JsonSerializer.DeserializeAsync<ReadPostsList>(fs) is { } r)
    {
        postList = r.Posts;
        if (!newSession)
            originalCount = newPostsCount = r.NewPostsCount;
        var imgTasks = new List<Task>(postList.Count);
        foreach (var post in postList)
            if (postIdSet.Add(post.Id))
                imgTasks.Add(DownloadThumbnail(client, post));
        await Task.WhenAll(imgTasks);
    }
}

postList ??= [];

if (settings.Nonce is not { } nonce)
{
    Console.WriteLine("Fetching nonce ");
    using var nonceJson = await client.GetAsync("https://www.mhh1.com/wp-admin/admin-ajax.php?" + settings.NonceParam);
    _ = nonceJson.EnsureSuccessStatusCode();
    using var jsonDocument = await JsonDocument.ParseAsync(nonceJson.Content.ReadAsStream());
    nonce = jsonDocument.RootElement.GetProperty("_nonce").GetString();
}

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
            if (await response.Content.ReadFromJsonAsync<ApiResponse>() is { Data.Posts: { } p })
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

    var imgDownloadTasks = new List<Task>(itemsPerPage);

    while (tempPosts.TryPop(out var post))
        if (postIdSet.Add(post.Id))
        {
            Console.WriteLine($"New Item [{post.Id}]: {post.Url}");
            if (continuousExistence < continuousExistenceThreshold)
                continuousExistence = 0;
            postList.AddFirst(post);
            newPostsCount++;
            imgDownloadTasks.Add(DownloadThumbnail(client, post));
        }
        else
        {
            Console.WriteLine($"Item existed: {post.Id} Continuous existence count: {continuousExistence}");
            continuousExistence++;
        }

    await Task.WhenAll(imgDownloadTasks);

    if (continuousExistence >= continuousExistenceThreshold)
        break;

    while (DateTime.UtcNow < lastRequest + coolDown)
        await Task.Delay(500);
    data.Paged++;
}

Console.WriteLine("\e[32mReached continuous existence threshold. Stopping crawl.");
Console.Write("Get ");
if (newSession)
    Console.Write(newPostsCount);
else
    Console.Write(originalCount + " + " + (newPostsCount - originalCount));
Console.WriteLine(" new items\e[0m");

JsonSerializerOptions options = new()
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

var resultPosts = postList.OrderByDescending(t => t.Date);
var myList = new WritePostsList
{
    NewPostsCount = newPostsCount,
    Posts = resultPosts.Take(newPostsCount + continuousExistenceThreshold)
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
    await using var fs = OpenAsyncWrite(loggerJsonPath, FileMode.CreateNew);
    await JsonSerializer.SerializeAsync(fs, myList, options);
}
catch (Exception e)
{
    WriteException(e);
    var fileName = $"TempLog {DateTime.Now:yyyy.MM.dd HH-mm-ss}.json";
    Console.WriteLine($"\e[31mSave failed. Backing up {fileName}\e[0m");
    var loggerTempJsonPath = Path.Combine(loggerPath, fileName);
    await using var fs = OpenAsyncWrite(loggerTempJsonPath, FileMode.CreateNew);
    await JsonSerializer.SerializeAsync(fs, myList, options);
}

return;

static async Task DownloadThumbnail(HttpClient client, Post post)
{
    var postThumbnailUrl = post.Thumbnail.Url;
    var fileName = post.ThumbnailFileName;
    var imgPath = Path.Combine(loggerImgPath, post.ThumbnailFileName);
    if (File.Exists(imgPath))
        return;
    try
    {
        await using var stream = await client.GetStreamAsync(postThumbnailUrl);
        await using var fileStream = OpenAsyncWrite(imgPath, FileMode.CreateNew);
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
    var u8Str = JsonSerializer.SerializeToUtf8Bytes(data);
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

static FileStream OpenAsyncRead(string path, FileMode mode)
    => new(path, mode, FileAccess.Read, FileShare.Read, 4096, true);

static FileStream OpenAsyncWrite(string path, FileMode mode)
    => new(path, mode, FileAccess.ReadWrite, FileShare.None, 4096, true);

static FileStream OpenAsyncStream(string path, FileMode mode, FileAccess access, FileShare share)
    => new(path, mode, access, share, 4096, true);

static void WriteException(Exception e) => Console.WriteLine($"\e[90m{e.Message}\e[0m");
