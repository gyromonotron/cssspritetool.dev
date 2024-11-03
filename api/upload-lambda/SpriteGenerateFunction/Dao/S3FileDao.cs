using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace SpriteGenerateFunction.Dao;

public class S3FileDao
{
    private readonly string _bucket;
    private readonly string _resultFolderPath;
    private readonly AmazonS3Client _client;

    public S3FileDao(string bucket, string resultFolderPath)
    {
        _bucket = bucket;
        _resultFolderPath = resultFolderPath;
        _client = new AmazonS3Client();
    }

    public async Task<Stream> GetStreamAsync(string key)
    {
        var request = new GetObjectRequest
        {
            BucketName = _bucket,
            Key = key
        };

        var response = await _client.GetObjectAsync(request);
        return response.ResponseStream;
    }

    public async Task<Dictionary<string, Stream>> GetStreamsAsync(IEnumerable<string> keys)
    {
        var tasks = keys.Select(async key =>
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucket,
                Key = key
            };

            var response = await _client.GetObjectAsync(request);
            return (key, response.ResponseStream);
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(x => x.key, x => x.Item2);
    }

    public async Task<Dictionary<string, Stream>> GetStreamsAsync(IEnumerable<string> keys, int maxDegreeOfParallelism)
    {
        var tasks = keys.Select(async key =>
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucket,
                Key = key
            };

            var response = await _client.GetObjectAsync(request);
            using (var hashStream = response.ResponseStream)
            {
                var memoryStream = new MemoryStream();
                await hashStream.CopyToAsync(memoryStream);
                return (key, memoryStream);
            }
        });

        var results = new List<(string, Stream)>();
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        foreach (var task in tasks)
        {
            await semaphore.WaitAsync();
            var result = await task;
            results.Add(result);
            semaphore.Release();
        }

        return results.ToDictionary(x => x.Item1, x => x.Item2);
    }

    public async Task<string> UploadStreamAsync(Stream stream, string ext)
    {
        var tu = new TransferUtility(_client);
        var key = GenerateKey(ext);
        stream.Position = 0;
        await tu.UploadAsync(stream, _bucket, key);
        return key;
    }

    public async Task<string> GetPresignedLinkAsync(string key)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Expires = DateTime.Now.AddDays(1)
        };

        return await _client.GetPreSignedURLAsync(request);
    }

    private string GenerateKey(string extension)
    {
        var dateFolder = DateTime.Now.ToString("yyyyMMdd");
        return $"{_resultFolderPath}{dateFolder}/Sprite_{Guid.NewGuid()}.{extension}";
    }
}
