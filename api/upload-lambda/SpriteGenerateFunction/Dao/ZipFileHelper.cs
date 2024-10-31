using System.IO.Compression;

namespace SpriteGenerateFunction.Dao
{
    public static class ZipFileHelper
    {
        public static ZipArchive CreateZipArchive(Stream stream)
        {
            return new ZipArchive(stream, ZipArchiveMode.Create, true);
        }

        public static async Task AddFileToZipArchiveAsync(ZipArchive zipArchive, string fileName, Stream stream)
        {
            var entry = zipArchive.CreateEntry(fileName);
            using var entryStream = entry.Open();
            stream.Position = 0;
            await stream.CopyToAsync(entryStream);
        }

        public static async Task AddFileToZipArchiveAsync(ZipArchive zipArchive, string fileName, byte[] bytes)
        {
            var entry = zipArchive.CreateEntry(fileName);
            using var entryStream = entry.Open();
            await entryStream.WriteAsync(bytes);
        }
    }
}
