using LambdaApi.Dao;
using LambdaApi.Model;
using RectpackSharp;
using SkiaSharp;
using System.Net;
using System.Text;

namespace LambdaApi
{
    internal class Handler
    {
        private readonly S3FileDao _s3DataAccess;
        private readonly ILogger _logger;
        private readonly string _allowedExtensions;
        private readonly int _allowedTotalFiles;
        private readonly int _allowedTotalFileSizeMb;

        internal Handler(IConfiguration configuration, ILogger logger)
        {
            _logger = logger;

            _allowedExtensions = configuration.GetValue<string>("AllowedExtensions") ?? throw new ArgumentNullException(nameof(configuration) + ".AllowedExtensions");
            _allowedTotalFiles = configuration.GetValue<int>("AllowedTotalFiles");
            _allowedTotalFileSizeMb = configuration.GetValue<int>("AllowedTotalFileSizeMb");

            var bucketName = configuration.GetValue<string>("BucketName") ?? throw new ArgumentNullException(nameof(configuration) + ".BucketName");
            _s3DataAccess = new S3FileDao(bucketName);
        }

        public async Task Upload(HttpContext context)
        {
            var files = context.Request.Form.Files;
            context.Request.Form.TryGetValue("format", out var formatParam);
            var format = ConvertFormat.FromName(formatParam.ToString());

            if (format == ConvertFormat.Unsupported)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid output format");
                return;
            }

            if (files.Count == 0)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Sorry, but you forgot to send files");
                return;
            }

            if (files.Count > _allowedTotalFiles)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Sorry, too many files");
                return;
            }

            if (files.Sum(f => f.Length) > _allowedTotalFileSizeMb * 1024 * 1024)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Sorry, too many megabytes");
                return;
            }

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file.FileName);
                if (!_allowedExtensions.Contains(extension))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Sorry, wrong file found");
                    return;
                }
            }

            var rectangles = new PackingRectangle[files.Count];
            var bitmaps = new Dictionary<int, SKBitmap>();
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                using var stream = file.OpenReadStream();
                var bitmap = SKBitmap.Decode(stream);
                bitmaps.Add(i, bitmap);
                rectangles[i] = new PackingRectangle()
                {
                    Id = i,
                    Width = (uint)bitmap.Width,
                    Height = (uint)bitmap.Height
                };
            }

            RectanglePacker.Pack(rectangles, out PackingRectangle bounds);

            using var image = new SKBitmap((int)bounds.Width, (int)bounds.Height);
            using var canvas = new SKCanvas(image);
            foreach (var rect in rectangles)
            {
                var bitmap = bitmaps[rect.Id];
                canvas.DrawBitmap(bitmap, new SKPoint(rect.X, rect.Y));
            }

            var response = new SpriteResponse();
            try
            {
                using (var zipStream = new MemoryStream())
                {
                    using (var zipArchive = ZipFileHelper.CreateZipArchive(zipStream))
                    {
                        var encodeFormat = format == ConvertFormat.WebP ? SKEncodedImageFormat.Webp : SKEncodedImageFormat.Png;
                        using (var imageStream = new MemoryStream())
                        {
                            image.Encode(encodeFormat, 100).SaveTo(imageStream);
                            await ZipFileHelper.AddFileToZipArchiveAsync(zipArchive, $"sprite.{format}", imageStream);

                            var spriteKey = await _s3DataAccess.UploadStreamAsync(imageStream, formatParam!);
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
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error processing request");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Sorry, something went wrong :'(");
                return;
            }

            // return container object with data
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsJsonAsync(response, typeof(SpriteResponse), ApiSerializerContext.Default);
            return;
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
}