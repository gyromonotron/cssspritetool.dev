using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace SpriteGenerateFunction.Dao;

public class S3FileDao
{
    private readonly string _bucket;
    private readonly AmazonS3Client _client;

    public S3FileDao(string bucket)
    {
        _bucket = bucket;
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

    // get the streams of multiple files in S3 buckets in parallel
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
        try
        {
            var tasks = keys.Select(async key =>
            {
                var request = new GetObjectRequest
                {
                    BucketName = _bucket,
                    Key = key
                };

                Console.WriteLine($"Downloading {key}");
                var response = await _client.GetObjectAsync(request);
                Console.WriteLine($"Downloaded {key}");
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

            Console.WriteLine("All files downloaded");
            return results.ToDictionary(x => x.Item1, x => x.Item2);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
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

    private static string GenerateKey(string extension)
    {
        var dateFolder = DateTime.Now.ToString("yyyyMMdd");
        return $"{dateFolder}/Sprite_{Guid.NewGuid()}.{extension}";
    }
}
