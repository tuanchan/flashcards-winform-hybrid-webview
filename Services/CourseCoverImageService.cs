using System;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public static class CourseCoverImageService
    {
        private const string CoverFolderName = "Cover";
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        static CourseCoverImageService()
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
        }

        public static async Task<string> SaveCoverAsync(CardSet set, string? imageSource, string? fallbackTerm = null)
        {
            if (set == null || string.IsNullOrWhiteSpace(set.BaseFolder))
                return "";

            var source = (imageSource ?? "").Trim();
            if (string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(fallbackTerm))
            {
                var pixabayCover = await PixabayImageService.SaveCourseCoverFromFirstCardAsync(set);
                if (!string.IsNullOrWhiteSpace(pixabayCover))
                    return pixabayCover;

                return "";
            }

            if (string.IsNullOrWhiteSpace(source))
                return "";

            Directory.CreateDirectory(Path.Combine(set.BaseFolder, CoverFolderName));

            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return await DownloadCoverAsync(set.BaseFolder, uri);
            }

            return CopyLocalCover(set.BaseFolder, source);
        }

        public static string ToWebUri(string? imagePath)
        {
            var path = (imagePath ?? "").Trim();
            if (string.IsNullOrWhiteSpace(path))
                return "";

            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return path;
            }

            return File.Exists(path) ? WebViewAssetService.GetLocalFileAssetUri(path) : "";
        }

        private static async Task<string> DownloadCoverAsync(string baseFolder, Uri uri)
        {
            var response = await Http.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var html = await response.Content.ReadAsStringAsync();
                var directImage = ExtractImageUri(uri, html);
                if (directImage != null &&
                    !string.Equals(directImage.AbsoluteUri, uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
                {
                    return await DownloadCoverAsync(baseFolder, directImage);
                }

                return "";
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var ext = Path.GetExtension(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(ext) || ext.Length > 6)
                ext = ExtensionFromMediaType(mediaType);

            var path = Path.Combine(baseFolder, CoverFolderName, "cover" + ext.ToLowerInvariant());
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }

        private static async Task<string> GenerateGeminiCoverAsync(string baseFolder, string term)
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(baseFolder, CoverFolderName));
                var generated = await GeminiService.GenerateImageAsync(term);
                var ext = ExtensionFromMediaType(generated.MimeType);
                var path = Path.Combine(baseFolder, CoverFolderName, "cover" + ext);
                await File.WriteAllBytesAsync(path, generated.Bytes);
                return path;
            }
            catch
            {
                return "";
            }
        }

        private static Uri? ExtractImageUri(Uri pageUri, string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return null;

            var patterns = new[]
            {
                "<meta[^>]+property=[\"']og:image[\"'][^>]+content=[\"'](?<url>[^\"']+)[\"']",
                "<meta[^>]+content=[\"'](?<url>[^\"']+)[\"'][^>]+property=[\"']og:image[\"']",
                "<a[^>]+class=[\"'][^\"']*internal[^\"']*[\"'][^>]+href=[\"'](?<url>[^\"']+)[\"']",
                "<div[^>]+class=[\"']fullImageLink[\"'][\\s\\S]*?<a[^>]+href=[\"'](?<url>[^\"']+)[\"']"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;

                var raw = WebUtility.HtmlDecode(match.Groups["url"].Value).Trim();
                if (raw.StartsWith("//", StringComparison.Ordinal))
                    raw = pageUri.Scheme + ":" + raw;

                if (Uri.TryCreate(pageUri, raw, out var result) &&
                    (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
                {
                    return result;
                }
            }

            return null;
        }

        private static string ExtensionFromMediaType(string mediaType)
        {
            return mediaType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                _ => ".jpg"
            };
        }

        private static string CopyLocalCover(string baseFolder, string source)
        {
            var path = source;
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeFile)
                path = uri.LocalPath;

            if (!File.Exists(path))
                return "";

            var ext = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".jpg";

            var dest = Path.Combine(baseFolder, CoverFolderName, "cover" + ext.ToLowerInvariant());
            File.Copy(path, dest, overwrite: true);
            return dest;
        }
    }
}
