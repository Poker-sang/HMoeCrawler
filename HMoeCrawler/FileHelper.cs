using System.IO;

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
}
