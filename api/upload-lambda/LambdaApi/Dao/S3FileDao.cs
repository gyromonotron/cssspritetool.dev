using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace LambdaApi.Dao
{
    public class S3FileDao
    {
        private readonly string _bucket;
        private readonly AmazonS3Client _client;

        public S3FileDao(string bucket)
        {
            _bucket = bucket;
            _client = new AmazonS3Client();
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
}
