#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public static class PixabayImageService
    {
        private const string SourceName = "Pixabay";
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        static PixabayImageService()
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124 Safari/537.36");
            Http.DefaultRequestHeaders.Referrer = new Uri("https://pixabay.com/");
        }

        public static async Task<string> SaveCourseCoverFromFirstCardAsync(
            CardSet set,
            CancellationToken cancellationToken = default)
        {
            var first = set.Items?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Term));
            if (first == null)
                return "";

            var image = await DownloadFirstImageAsync(first.Term, cancellationToken);
            if (image.Bytes.Length == 0)
                return "";

            var baseFolder = ResolveSetBaseFolder(set);
            if (string.IsNullOrWhiteSpace(baseFolder))
                return "";

            var coverFolder = Path.Combine(baseFolder, "Cover");
            Directory.CreateDirectory(coverFolder);

            var ext = ExtensionFromMediaType(image.MimeType, image.Uri);
            var path = Path.Combine(coverFolder, "cover" + ext);
            await File.WriteAllBytesAsync(path, image.Bytes, cancellationToken);
            return path;
        }

        public static async Task<bool> SaveVocabularyImageAsync(
            CardSet set,
            CardItem item,
            GeminiExamplesPayload? payload = null,
            bool randomize = false,
            CancellationToken cancellationToken = default)
        {
            var term = (item.Term ?? "").Trim();
            if (string.IsNullOrWhiteSpace(term))
                return false;

            var image = await DownloadImageAsync(term, randomize, cancellationToken);
            if (image.Bytes.Length == 0)
                return false;

            var ext = ExtensionFromMediaType(image.MimeType, image.Uri);
            var imageFile = ResolveVocabularyImageFile(set, item, ext);
            if (string.IsNullOrWhiteSpace(imageFile))
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(imageFile) ?? "");
            await File.WriteAllBytesAsync(imageFile, image.Bytes, cancellationToken);

            payload ??= GeminiExampleStore.TryGet(set, item) ?? new GeminiExamplesPayload
            {
                Term = item.Term ?? "",
                Definition = item.Definition ?? "",
                Pinyin = item.Pinyin ?? ""
            };

            var prompt = BuildPixabaySearchPageUrl(term);
            var alt = BuildImageAlt(item);
            payload.ImagePath = AppendCacheBust(WebViewAssetService.GetLocalFileAssetUri(imageFile));
            payload.ImagePrompt = prompt;
            payload.ImageAlt = alt;
            payload.UpdatedAt = DateTimeOffset.Now.ToString("O");

            GeminiExampleStore.Save(set, item, payload);
            WriteImageIndex(set, item, imageFile, prompt, alt, image.Uri);
            SaveCoverFromImageIfNeeded(set, item, imageFile);
            return true;
        }

        public static string TryGetFirstVocabularyImageUri(CardSet set)
        {
            var items = set.Items != null && set.Items.Count > 0
                ? set.Items
                : LoadVocabularyItemsQuietly(set);
            var first = items.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Term));
            if (first == null)
                return "";

            foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" })
            {
                var path = ResolveVocabularyImageFile(set, first, ext);
                if (File.Exists(path))
                {
                    SaveCoverFromImageIfNeeded(set, first, path);
                    return WebViewAssetService.GetLocalFileAssetUri(path);
                }
            }

            return "";
        }

        public static async Task GenerateVocabularyImagesAsync(
            CardSet set,
            int maxCount = 100,
            CancellationToken cancellationToken = default)
        {
            if (set.Items == null || set.Items.Count == 0)
                return;

            foreach (var item in set.Items.Where(x => !string.IsNullOrWhiteSpace(x.Term)).Take(maxCount))
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                try
                {
                    var cached = GeminiExampleStore.TryGet(set, item);
                    if (!string.IsNullOrWhiteSpace(cached?.ImagePath))
                        continue;

                    await SaveVocabularyImageAsync(set, item, cached, randomize: false, cancellationToken);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Pixabay vocab image failed: {ex.Message}");
                }
            }
        }

        private static List<CardItem> LoadVocabularyItemsQuietly(CardSet set)
        {
            try
            {
                return CardSetStorage.LoadVocabularyItems(set);
            }
            catch
            {
                return new List<CardItem>();
            }
        }

        private static void SaveCoverFromImageIfNeeded(CardSet set, CardItem item, string imageFile)
        {
            if (!string.IsNullOrWhiteSpace(set.CoverImagePath) && File.Exists(set.CoverImagePath))
                return;

            var items = set.Items != null && set.Items.Count > 0
                ? set.Items
                : LoadVocabularyItemsQuietly(set);
            var first = items.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Term));
            if (first == null ||
                !string.Equals(CardSetStorage.BuildCardKey(first), CardSetStorage.BuildCardKey(item), StringComparison.Ordinal))
            {
                return;
            }

            var baseFolder = ResolveSetBaseFolder(set);
            if (string.IsNullOrWhiteSpace(baseFolder) || !File.Exists(imageFile))
                return;

            try
            {
                var coverFolder = Path.Combine(baseFolder, "Cover");
                Directory.CreateDirectory(coverFolder);
                var ext = Path.GetExtension(imageFile);
                if (string.IsNullOrWhiteSpace(ext))
                    ext = ".jpg";

                var coverPath = Path.Combine(coverFolder, "cover" + ext.ToLowerInvariant());
                File.Copy(imageFile, coverPath, overwrite: true);
                set.CoverImagePath = coverPath;
                CardSetStorage.SaveSetJson(set);
            }
            catch
            {
            }
        }

        private static async Task<(byte[] Bytes, string MimeType, Uri? Uri)> DownloadFirstImageAsync(
            string term,
            CancellationToken cancellationToken)
        {
            return await DownloadImageAsync(term, randomize: false, cancellationToken);
        }

        private static async Task<(byte[] Bytes, string MimeType, Uri? Uri)> DownloadImageAsync(
            string term,
            bool randomize,
            CancellationToken cancellationToken)
        {
            var apiKey = SettingsService.GetPixabaySettings().ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                return default;

            var apiUri = BuildApiSearchUri(apiKey, term, randomize ? 20 : 3);
            using var pageRequest = new HttpRequestMessage(HttpMethod.Get, apiUri);
            using var pageResponse = await Http.SendAsync(pageRequest, cancellationToken);
            if (!pageResponse.IsSuccessStatusCode)
            {
                if (pageResponse.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new InvalidOperationException("Pixabay API key không hợp lệ hoặc chưa được cấp quyền.");

                return default;
            }

            var json = await pageResponse.Content.ReadAsStringAsync(cancellationToken);
            var uris = ExtractPixabayApiImageUris(json).ToList();
            if (randomize && uris.Count > 1)
            {
                var start = RandomNumberGenerator.GetInt32(uris.Count);
                uris = uris.Skip(start).Concat(uris.Take(start)).ToList();
            }

            foreach (var uri in uris)
            {
                try
                {
                    using var imageRequest = new HttpRequestMessage(HttpMethod.Get, uri);
                    using var imageResponse = await Http.SendAsync(imageRequest, cancellationToken);
                    if (!imageResponse.IsSuccessStatusCode)
                        continue;

                    var mediaType = imageResponse.Content.Headers.ContentType?.MediaType ?? "";
                    if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var bytes = await imageResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                    if (bytes.Length > 0)
                        return (bytes, mediaType, uri);
                }
                catch
                {
                }
            }

            return default;
        }

        private static IEnumerable<Uri> ExtractPixabayApiImageUris(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                yield break;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("hits", out var hits) ||
                hits.ValueKind != JsonValueKind.Array)
                yield break;

            foreach (var hit in hits.EnumerateArray())
            {
                foreach (var propertyName in new[] { "largeImageURL", "webformatURL", "previewURL" })
                {
                    if (!hit.TryGetProperty(propertyName, out var value))
                        continue;

                    var raw = value.GetString() ?? "";
                    if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    {
                        yield return uri;
                    }
                }
            }
        }

        private static string BuildApiSearchUri(string apiKey, string term, int perPage)
        {
            perPage = Math.Clamp(perPage, 3, 30);
            return "https://pixabay.com/api/?" +
                   "key=" + Uri.EscapeDataString(apiKey.Trim()) +
                   "&q=" + Uri.EscapeDataString(term.Trim()) +
                   "&image_type=photo" +
                   "&safesearch=true" +
                   "&per_page=" + perPage.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildPixabaySearchPageUrl(string term)
        {
            return "https://pixabay.com/vi/images/search/" + Uri.EscapeDataString(term.Trim()) + "/";
        }

        private static string ResolveVocabularyImageFile(CardSet set, CardItem item, string extension)
        {
            var baseFolder = ResolveSetBaseFolder(set);
            if (string.IsNullOrWhiteSpace(baseFolder))
                return "";

            var imageFolder = Path.Combine(baseFolder, CardSetStorage.VocabsFolderNameValue, "images");
            var ext = string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension.Trim();
            if (!ext.StartsWith(".", StringComparison.Ordinal))
                ext = "." + ext;

            return Path.Combine(imageFolder, HashText(CardSetStorage.BuildCardKey(item)) + ext.ToLowerInvariant());
        }

        private static string ResolveSetBaseFolder(CardSet set)
        {
            if (!string.IsNullOrWhiteSpace(set.BaseFolder))
                return set.BaseFolder;

            if (!string.IsNullOrWhiteSpace(set.ConfigFilePath))
                return Path.GetDirectoryName(set.ConfigFilePath) ?? "";

            if (!string.IsNullOrWhiteSpace(set.VocabsFilePath))
            {
                var vocabsDir = Path.GetDirectoryName(set.VocabsFilePath);
                return Directory.GetParent(vocabsDir ?? "")?.FullName ?? "";
            }

            return "";
        }

        private static void WriteImageIndex(
            CardSet set,
            CardItem item,
            string imageFile,
            string prompt,
            string alt,
            Uri? sourceUri)
        {
            var imageFolder = Path.GetDirectoryName(imageFile);
            if (string.IsNullOrWhiteSpace(imageFolder))
                return;

            var record = new
            {
                source = SourceName,
                key = CardSetStorage.BuildCardKey(item),
                term = item.Term ?? "",
                definition = item.Definition ?? "",
                imageFile = Path.GetFileName(imageFile),
                imagePath = WebViewAssetService.GetLocalFileAssetUri(imageFile),
                prompt,
                alt,
                sourceUrl = sourceUri?.AbsoluteUri ?? "",
                updatedAt = DateTimeOffset.Now.ToString("O")
            };

            var sidecar = Path.ChangeExtension(imageFile, ".txt");
            File.WriteAllText(
                sidecar,
                JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }),
                CardSetStorage.Utf8NoBomEncoding);
        }

        private static string BuildImageAlt(CardItem item)
        {
            var term = (item.Term ?? "").Trim();
            var definition = (item.Definition ?? "").Trim();
            return string.IsNullOrWhiteSpace(definition)
                ? $"Anh minh hoa Pixabay cho {term}"
                : $"Anh minh hoa Pixabay cho {term}, nghia la {definition}";
        }

        private static string AppendCacheBust(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return "";

            var separator = uri.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return uri + separator + "v=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        }

        private static string ExtensionFromMediaType(string mediaType, Uri? uri)
        {
            var ext = Path.GetExtension(uri?.AbsolutePath ?? "");
            if (!string.IsNullOrWhiteSpace(ext) && ext.Length <= 6)
                return ext.ToLowerInvariant();

            return (mediaType ?? "").ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                _ => ".jpg"
            };
        }

        private static string HashText(string text)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? ""));
            return Convert.ToHexString(bytes, 0, 12).ToLowerInvariant();
        }
    }
}
