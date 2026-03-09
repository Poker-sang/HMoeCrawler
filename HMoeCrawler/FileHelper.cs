using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace HMoeCrawler;

public static class FileHelper
{
    extension(File)
    {
        public static FileStream OpenAsyncRead(string path, FileMode mode)
            => new(path, mode, FileAccess.Read, FileShare.Read, 4096, true);

        public static FileStream OpenAsyncWrite(string path, FileMode mode)
            => new(path, mode, FileAccess.ReadWrite, FileShare.None, 4096, true);

        public static FileStream OpenAsyncStream(string path, FileMode mode, FileAccess access, FileShare share)
            => new(path, mode, access, share, 4096, true);
    }

    extension(JsonSerializer)
    {
        public static async Task<TValue?> OpenDeserializeAsync<TValue>(string path, JsonTypeInfo<TValue> jsonTypeInfo)
        {
            await using var fs = File.OpenAsyncRead(path, FileMode.Open);
            return await JsonSerializer.DeserializeAsync(fs, jsonTypeInfo);
        }

        public static async Task CreateSerializeAsync<TValue>(string path, TValue value, JsonTypeInfo<TValue> jsonTypeInfo)
        {
            await using var fs = File.OpenAsyncWrite(path, FileMode.Create);
            await JsonSerializer.SerializeAsync(fs, value, jsonTypeInfo);
        }
    }
}
