using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Configuration;
using RectpackSharp;
using SkiaSharp;
using SpriteGenerateFunction.Dao;
using SpriteGenerateFunction.Model;
using System.Text;

namespace SpriteGenerateFunction;

internal class Handler
{
    private readonly S3FileDao _s3DataAccess;
    private readonly ILambdaLogger _logger;
    private readonly string _allowedExtensions;
    private readonly int _allowedTotalFiles;
    private readonly int _allowedFileSize;

    internal Handler(IConfiguration configuration, ILambdaLogger logger)
    {
        _logger = logger;
        _allowedExtensions = configuration["AllowedExtensions"] ?? throw new ArgumentNullException(nameof(configuration) + ".AllowedExtensions");
        _allowedTotalFiles = int.Parse(configuration["AllowedTotalFiles"] ?? throw new ArgumentNullException(nameof(configuration) + ".AllowedTotalFiles"));
        _allowedFileSize = int.Parse(configuration["AllowedFileSize"] ?? throw new ArgumentNullException(nameof(configuration) + ".AllowedFileSize"));

        var resultFolderPath = configuration["ResultFolderPath"] ?? throw new ArgumentNullException(nameof(configuration) + ".ResultFolderPath");
        var bucketName = configuration["BucketName"] ?? throw new ArgumentNullException(nameof(configuration) + ".BucketName");
        _s3DataAccess = new S3FileDao(bucketName, resultFolderPath);

        _logger.LogLine($"Allowed extensions: {_allowedExtensions}");
        _logger.LogLine($"Allowed total files: {_allowedTotalFiles}");
        _logger.LogLine($"Bucket name: {bucketName}");
        _logger.LogLine($"Result folder path: {resultFolderPath}");
        _logger.LogLine("Handler initialized");
    }

    public async Task<IHttpResult> ProcessAsync(InputDto input)
    {
        var files = input.Files;
        var format = ConvertFormat.FromName(input.Format);

        if (format == ConvertFormat.Unsupported)
        {
            return HttpResults.BadRequest("Invalid output format");
        }

        if (files.Length == 0)
        {
            return HttpResults.BadRequest("Sorry, but you forgot to send files");
        }

        if (files.Length > _allowedTotalFiles)
        {
            return HttpResults.BadRequest("Sorry, too many files");
        }

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            if (!_allowedExtensions.Contains(extension))
            {
                return HttpResults.BadRequest("Sorry, but we don't support this file type");
            }
        }

        var rectangles = new PackingRectangle[files.Length];
        var bitmaps = new Dictionary<int, SKBitmap>();
        var fileStreams = await _s3DataAccess.GetStreamsAsync(files, 3);

        for (int i = 0; i < fileStreams.Count; i++)
        {
            var file = fileStreams.ElementAt(i);
            using (var stream = file.Value)
            {
                if (stream.Length > _allowedFileSize)
                {
                    return HttpResults.BadRequest("Sorry, but the file is too big");
                }

                stream.Position = 0;
                var bitmap = SKBitmap.Decode(stream);
                bitmaps.Add(i, bitmap);
                rectangles[i] = new PackingRectangle()
                {
                    Id = i,
                    Width = (uint)bitmap.Width,
                    Height = (uint)bitmap.Height
                };
            }
        }

        RectanglePacker.Pack(rectangles, out PackingRectangle bounds);

        using var image = new SKBitmap((int)bounds.Width, (int)bounds.Height);
        using var canvas = new SKCanvas(image);
        foreach (var rect in rectangles)
        {
            using var bitmap = bitmaps[rect.Id];
            canvas.DrawBitmap(bitmap, new SKPoint(rect.X, rect.Y));
        }

        var response = new SpriteResponse();
        using (var zipStream = new MemoryStream())
        {
            using (var zipArchive = ZipFileHelper.CreateZipArchive(zipStream))
            {
                var encodeFormat =
                    format == ConvertFormat.Png ? SKEncodedImageFormat.Png :
                    format == ConvertFormat.WebP ? SKEncodedImageFormat.Webp :
                    SKEncodedImageFormat.Avif;

                using (var imageStream = new MemoryStream())
                {
                    image.Encode(encodeFormat, 100).SaveTo(imageStream);
                    await ZipFileHelper.AddFileToZipArchiveAsync(zipArchive, $"sprite.{format}", imageStream);

                    var spriteKey = await _s3DataAccess.UploadStreamAsync(imageStream, format.Name);
                    response.SpriteUrl = await _s3DataAccess.GetPresignedLinkAsync(spriteKey);
                }

                response.Css = GenerateCssString(format, rectangles);
                await ZipFileHelper.AddFileToZipArchiveAsync(zipArchive, $"style.css", Encoding.UTF8.GetBytes(response.Css));

                response.Html = GenerateHtmlString(rectangles);
                await ZipFileHelper.AddFileToZipArchiveAsync(zipArchive, $"index.html", Encoding.UTF8.GetBytes(response.Html));
            }

            var zipKey = await _s3DataAccess.UploadStreamAsync(zipStream, "zip");
            response.ZipUrl = await _s3DataAccess.GetPresignedLinkAsync(zipKey);
        }

        return HttpResults.Ok(response);
    }

    private static string GenerateCssString(ConvertFormat format, PackingRectangle[] rectangles)
    {
        var responseCss = new StringBuilder();
        foreach (var rect in rectangles)
        {
            responseCss.AppendLine($".sprite-{rect.Id} {{ background: url('sprite.{format}') -{rect.X}px -{rect.Y}px; width: {rect.Width}px; height: {rect.Height}px; }}");
        }

        return responseCss.ToString();
    }

    private static string GenerateHtmlString(PackingRectangle[] rectangles)
    {
        var responseHtml = new StringBuilder()
            .AppendLine("<html>")
            .AppendLine("  <head>")
            .AppendLine("    <link rel='stylesheet' type='text/css' href='style.css'>")
            .AppendLine("  </head>")
            .AppendLine("  <body>");

        foreach (var r in rectangles)
        {
            responseHtml.AppendLine($"    <div class='sprite-{r.Id}'></div>");
        }

        responseHtml
            .AppendLine("  </body>")
            .AppendLine("</html>");

        return responseHtml.ToString();
    }
}
