using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using HMoeCrawler.Models;
using SixLabors.ImageSharp.Processing;
using Spectre.Console;

namespace HMoeCrawler;

public class HMoeSession : IDisposable
{
    public const string Domain = "https://www.mhh1.com/";
    public const string WpAdminDomain = Domain + "wp-admin/admin-ajax.php";

    /// <summary>
    /// 最大请求间隔，超过后中断
    /// </summary>
    public TimeSpan CoolDownThreshold { get; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 每次请求时间间隔
    /// </summary>
    public TimeSpan DefaultCoolDown = TimeSpan.FromSeconds(3);

    public event Func<(string Email, string Password)> OnLoginRequired = null!;

    public event EventHandler<HMoeSession, string>? CookieRefreshed;

    private readonly List<Task> _imageDownloadTasks = [];

    public IReadOnlyList<Task> ImageDownloadTasks => _imageDownloadTasks;

    public HttpClient Client { get; } = new()
    {
        Timeout = TimeSpan.FromSeconds(8),
        DefaultRequestHeaders =
        {
            Referrer = new(Domain + "search/-"),
            UserAgent =
            {
                new("Mozilla", "5.0"),
                new("(Windows NT 10.0; Win64; x64)"),
                new("AppleWebKit", "537.36"),
                new("(KHTML, like Gecko)"),
                new("Chrome", "143.0.0.0"),
                new("Safari", "537.36"),
                new("Edg", "143.0.0.0")
            }
        }
    };

    public async Task SetCookieAsync(string? value)
    {
        _ = Client.DefaultRequestHeaders.Remove("Cookie");
        if (value is null)
            _loginNonce = null;
        else
        {
            Client.DefaultRequestHeaders.Add("Cookie", value);
            _loginNonce = await FetchNonceAsync();
        }
    }

    public async Task<string> FetchNonceAsync()
    {
        Console.WriteLine("Fetching nonce ");
        const string url = WpAdminDomain + "?action=285d6af5ed069e78e04b2d054182dcb5&9f9fa05823795c1c74e8c27e8d5e6930%5Btype%5D=checkSigned&d6ca819426678dab7a26ecb2802d8aec%5Btype%5D=checkUnread&48a173a84f9a7283590984a0ff974848%5Btype%5D=getUnreadCount";
        using var nonceJson = await Client.GetAsync(url);
        _ = nonceJson.EnsureSuccessStatusCode();
        using var jsonDocument = await JsonDocument.ParseAsync(await nonceJson.Content.ReadAsStreamAsync());
        var nonce = jsonDocument.RootElement.GetProperty("_nonce").GetString() ?? "";
        Console.WriteLine("Get nonce " + nonce);
        return nonce;
    }

    public Task WhenAllDownloadAsync()
    {
        return Task.WhenAll(_imageDownloadTasks);
    }

    public void DownloadThumbnailAddToList(Post post, string imagePath)
    {
        _imageDownloadTasks.Add(DownloadThumbnailAsync(post, imagePath));
    }

    public async Task DownloadThumbnailAsync(Post post, string imagePath)
    {
        var postThumbnailUrl = post.Thumbnail.Url;

        // 处理相对 URI
        if (!postThumbnailUrl.IsAbsoluteUri)
        {
            var originalString = postThumbnailUrl.OriginalString;
            // 协议相对 URL (以 // 开头)
            postThumbnailUrl = originalString.StartsWith("//")
                ? new Uri("https:" + originalString)
                // 根相对 URL 或其他相对路径
                // 使用基础 URL 构建完整 URL
                : new(new(Domain), postThumbnailUrl);
        }

        var fileName = post.ThumbnailFileName;
        var imgPath = Path.Combine(imagePath, post.ThumbnailFileName);
        if (File.Exists(imgPath))
            return;
        try
        {
            await using var stream = await Client.GetStreamAsync(postThumbnailUrl);
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

    public async Task<string> GetCaptchaAsync(string nonce)
    {
        Console.WriteLine("Fetching captcha ");
        var url = $"{WpAdminDomain}?_nonce={nonce}&action=b9215121b88d889ea28808c5adabbbf5&type=getCaptcha";
        using var imgDataJson = await Client.GetAsync(url);
        _ = imgDataJson.EnsureSuccessStatusCode();
        var response = await imgDataJson.Content.ReadFromJsonAsync(SerializerContext.Default.ApiResponse)
                       ?? throw new InvalidOperationException("Failed to deserialize captcha response.");
        var base64 = response.GetData(SerializerContext.Default.ImageDataResult).ImgData;
        Console.WriteLine("Get captcha " + base64);

        var bytes = Convert.FromBase64String(base64[(base64.IndexOf(',') + 1)..]);
        var image = new CanvasImage(bytes).Mutate(t => t.BackgroundColor(SixLabors.ImageSharp.Color.White));
        AnsiConsole.Write(image);
        string? captcha;
        do
        {
            Console.WriteLine("Input captcha: ");
            captcha = Console.ReadLine();
        } while (string.IsNullOrWhiteSpace(captcha));

        return captcha;
    }

    public async Task RefreshCookieAsync()
    {
        var nonce = await FetchNonceAsync();
        var captcha = await GetCaptchaAsync(nonce);
        var (email, password) = OnLoginRequired();
        Console.WriteLine("Fetching cookie ");
        var url = $"{WpAdminDomain}?_nonce={nonce}&action=0ac2206cd584f32fba03df08b4123264&type=login";
        var response = await Client.PostAsync(url, new FormUrlEncodedContent([
            new("email", email),
            new("pwd", password),
            new("captcha", captcha),
            new("type", "login")
        ]));
        response.EnsureSuccessStatusCode();
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            throw new InvalidOperationException("Login failed: No Set-Cookie header in response.");
        var effectiveCookie = values.First(t => t.StartsWith("wordpress_logged_in"));
        Console.WriteLine("Get cookie " + effectiveCookie);
        CookieRefreshed?.Invoke(this, effectiveCookie);
    }

    private DateTime _lastRequest = DateTime.MinValue;

    private string? _loginNonce;

    public async Task<Stack<Post>> SearchPageAsync(SearchData data)
    {
        var coolDown = DefaultCoolDown;
        Stack<Post> tempPosts;
        while (true)
            try
            {
                if (_loginNonce is not null)
                {
                    while (DateTime.UtcNow < _lastRequest + coolDown)
                        await Task.Delay(500);

                    var uri = WpAdminDomain
                              + "?_nonce=" + _loginNonce
                              + "&action=b9338a11fcc41c1ed5447625d1c0e743"
                              + "&query=";

                    Console.WriteLine("Downloading page " + data.Paged);
                    using var response = await Client.GetAsync(uri + data.Encode());
                    _ = response.EnsureSuccessStatusCode();

                    if (await response.Content.ReadFromJsonAsync(SerializerContext.DefaultOverride.ApiResponse) is not
                        { } result)
                    {
                        throw new InvalidOperationException("Failed to deserialize captcha response.");
                    }

                    if (result.Code is not 10007)
                    {
                        var r = result.GetData(SerializerContext.Default.PostsSearchResult);
                        Console.WriteLine("Downloaded page " + data.Paged);
                        _lastRequest = DateTime.UtcNow;
                        coolDown = DefaultCoolDown;
                        tempPosts = r.Posts;
                        break;
                    }
                }

                await RefreshCookieAsync();
                _loginNonce = await FetchNonceAsync();
            }
            catch (Exception e)
            {
                WriteException(e);
                if (coolDown > CoolDownThreshold)
                    Debugger.Break();
                coolDown *= 2;
                await Task.Delay(coolDown);
            }

        return tempPosts;
    }

    private static void WriteException(Exception e) => Console.WriteLine($"\e[90m{e.Message}\e[0m");

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Client.Dispose();
    }
}
