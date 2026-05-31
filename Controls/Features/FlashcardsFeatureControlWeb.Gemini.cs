#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TocflQuiz.Forms;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class FlashcardsFeatureControlWeb
    {
        private static readonly JsonSerializerOptions GeminiJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private async Task ShowGeminiExamplesAsync(bool regenerate)
        {
            try
            {
                if (!EnsureApiKeysOrPrompt(requireGemini: true, requirePixabay: true))
                {
                    ShowGeminiError("Đã dừng tạo. Hãy cài đặt Gemini/Pixabay API key rồi thử lại.");
                    return;
                }

                if (_set?.Items == null || _order.Count == 0)
                {
                    ShowGeminiError("ChÆ°a cÃ³ tháº» Ä‘á»ƒ táº¡o vÃ­ dá»¥.");
                    return;
                }

                var item = _set.Items[_order[_index]];
                var storeSet = _sourceSet ?? _set;

                if (!regenerate)
                {
                    var cached = GeminiExampleStore.TryGet(storeSet, item);
                    if (cached != null)
                    {
                        if (await EnsureGeminiImageCacheAsync(storeSet, item, cached))
                            GeminiExampleStore.Save(storeSet, item, cached);

                        if (HasUsableGeminiExamples(cached) || !string.IsNullOrWhiteSpace(cached.ImagePath))
                        {
                            if (HasUsableGeminiExamples(cached))
                            {
                                SendGeminiExamplesToWeb(cached, fromCache: true);
                                return;
                            }

                            ExecuteScript("if(window.showGeminiLoading) window.showGeminiLoading('Gemini đang tạo ví dụ...');");
                        }
                    }
                }

                ExecuteScript("if(window.showGeminiLoading) window.showGeminiLoading('Gemini đang tạo ví dụ...');");

                var generated = await GeminiService.GenerateExamplesAsync(storeSet, item);
                
                if (!await GenerateAndCacheGeminiImageAsync(storeSet, item, generated))
                    await EnsureGeminiImageCacheAsync(storeSet, item, generated);
                GeminiExampleStore.Save(storeSet, item, generated);
                SendGeminiExamplesToWeb(generated, fromCache: false);
            }
            catch (Exception ex)
            {
                if (SettingsService.IsLikelyApiKeyError(ex.Message))
                    PromptApiKeys();
                ShowGeminiError(ex.Message);
            }
        }

        private async Task ShowGeminiImageOnlyAsync()
        {
            try
            {
                if (!EnsureApiKeysOrPrompt(requireGemini: false, requirePixabay: true))
                {
                    ShowGeminiError("Đã dừng tạo ảnh. Hãy cài đặt Pixabay API key rồi thử lại.");
                    return;
                }

                if (_set?.Items == null || _order.Count == 0) return;
                var item = _set.Items[_order[_index]];
                var storeSet = _sourceSet ?? _set;

                var cached = GeminiExampleStore.TryGet(storeSet, item);
                cached ??= new GeminiExamplesPayload
                {
                    Term = item.Term ?? "",
                    Definition = item.Definition ?? "",
                    Pinyin = item.Pinyin ?? "",
                    UpdatedAt = DateTimeOffset.Now.ToString("O")
                };

                if (await GenerateAndCacheGeminiImageAsync(storeSet, item, cached, randomize: true))
                {
                    GeminiExampleStore.Save(storeSet, item, cached);
                }
                else
                {
                    ShowGeminiError("Khong tao duoc anh Pixabay. Hay kiem tra Pixabay API key trong cai dat.");
                    return;
                }

                SendGeminiExamplesToWeb(cached, fromCache: false);
            }
            catch (Exception ex)
            {
                if (SettingsService.IsLikelyApiKeyError(ex.Message))
                    PromptApiKeys();
                ShowGeminiError(ex.Message);
            }
        }

        private bool EnsureApiKeysOrPrompt(bool requireGemini, bool requirePixabay)
        {
            var missingGemini = requireGemini && !SettingsService.HasGeminiApiKey();
            var missingPixabay = requirePixabay && !SettingsService.HasPixabayApiKey();
            if (!missingGemini && !missingPixabay)
                return true;

            PromptApiKeys();
            return false;
        }

        private void PromptApiKeys()
        {
            if (ParentForm is CardFormWeb form)
                form.PromptForApiKeysFromFeature();
        }

        private static async Task<bool> GenerateAndCacheGeminiImageAsync(
            CardSet set,
            CardItem item,
            GeminiExamplesPayload payload,
            bool randomize = false)
        {
            try
            {
                return await PixabayImageService.SaveVocabularyImageAsync(set, item, payload, randomize);
            }
            catch (Exception ex) when (SettingsService.IsLikelyApiKeyError(ex.Message))
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        private void SendGeminiExamplesToWeb(GeminiExamplesPayload payload, bool fromCache)
        {
            var json = JsonSerializer.Serialize(new
            {
                fromCache,
                examples = payload.Examples,
                memoryHint = BuildUsageHintForDisplay(payload),
                imagePath = payload.ImagePath ?? "",
                imageAlt = payload.ImageAlt ?? "",
                imagePrompt = payload.ImagePrompt ?? "",
                updatedAt = payload.UpdatedAt ?? ""
            }, GeminiJsonOptions);

            ExecuteScript($"if(window.showGeminiExamples) window.showGeminiExamples({json});");
        }

        private static bool HasUsableGeminiExamples(GeminiExamplesPayload payload)
        {
            return payload.Examples != null &&
                   payload.Examples.Any(x =>
                       !string.IsNullOrWhiteSpace(x.Source) ||
                       !string.IsNullOrWhiteSpace(x.Vietnamese));
        }

        private static async Task<bool> EnsureGeminiImageCacheAsync(CardSet? set, CardItem? item, GeminiExamplesPayload payload)
        {
            if (set == null || item == null || payload == null)
                return false;

            try
            {
                if (IsPixabayImagePayload(payload))
                    return false;

                if (await PixabayImageService.SaveVocabularyImageAsync(set, item, payload))
                    return true;

                if (string.IsNullOrWhiteSpace(payload.ImagePath) &&
                    string.IsNullOrWhiteSpace(payload.ImagePrompt) &&
                    string.IsNullOrWhiteSpace(payload.ImageAlt))
                {
                    return false;
                }

                payload.ImagePath = "";
                payload.ImagePrompt = "";
                payload.ImageAlt = "";
                payload.UpdatedAt = DateTimeOffset.Now.ToString("O");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPixabayImagePayload(GeminiExamplesPayload payload)
        {
            var prompt = payload.ImagePrompt ?? "";
            var path = payload.ImagePath ?? "";
            return prompt.Contains("pixabay.com", StringComparison.OrdinalIgnoreCase) &&
                   !path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveGeminiImageFile(CardSet set, CardItem item)
        {
            return ResolveGeminiImageFile(set, item, ".svg");
        }

        private static string ResolveGeminiImageFile(CardSet set, CardItem item, string extension)
        {
            var baseFolder = ResolveSetBaseFolder(set);
            if (string.IsNullOrWhiteSpace(baseFolder))
                return "";

            var imageFolder = Path.Combine(baseFolder, CardSetStorage.VocabsFolderNameValue, "images");
            var ext = string.IsNullOrWhiteSpace(extension) ? ".svg" : extension.Trim();
            if (!ext.StartsWith(".", StringComparison.Ordinal))
                ext = "." + ext;

            var fileName = HashText(CardSetStorage.BuildCardKey(item)) + ext.ToLowerInvariant();
            return Path.Combine(imageFolder, fileName);
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

        private static string ToWebAssetUri(string absolutePath)
        {
            return WebViewAssetService.GetLocalFileAssetUri(absolutePath);
        }

        private static void WriteImageIndex(CardSet set, CardItem item, string imageFile, string prompt, string alt)
        {
            var imageFolder = Path.GetDirectoryName(imageFile);
            if (string.IsNullOrWhiteSpace(imageFolder))
                return;

            var indexFile = Path.Combine(imageFolder, "GeminiImages.txt");
            var record = new
            {
                key = CardSetStorage.BuildCardKey(item),
                term = item.Term ?? "",
                definition = item.Definition ?? "",
                imageFile = Path.GetFileName(imageFile),
                imagePath = ToWebAssetUri(imageFile),
                prompt,
                alt,
                updatedAt = DateTimeOffset.Now.ToString("O")
            };

            File.WriteAllText(
                indexFile,
                JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }),
                CardSetStorage.Utf8NoBomEncoding);

            var sidecar = Path.ChangeExtension(imageFile, ".txt");
            File.WriteAllText(
                sidecar,
                JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }),
                CardSetStorage.Utf8NoBomEncoding);
        }

        private static string BuildVocabularySvg(CardItem item, string prompt, string alt)
        {
            var term = WebUtility.HtmlEncode((item.Term ?? "").Trim());
            var meaning = WebUtility.HtmlEncode((item.Definition ?? "").Trim());
            var pinyin = WebUtility.HtmlEncode((item.Pinyin ?? "").Trim());
            var visual = WebUtility.HtmlEncode(Shorten(prompt, 82));
            var altText = WebUtility.HtmlEncode(alt);

            return $$"""
<svg xmlns="http://www.w3.org/2000/svg" width="960" height="960" viewBox="0 0 960 960" role="img" aria-label="{{altText}}">
  <defs>
    <linearGradient id="bg" x1="0" x2="1" y1="0" y2="1">
      <stop offset="0" stop-color="#20273d"/>
      <stop offset=".55" stop-color="#151b2d"/>
      <stop offset="1" stop-color="#0f1424"/>
    </linearGradient>
    <radialGradient id="glow" cx="70%" cy="22%" r="70%">
      <stop offset="0" stop-color="#557fff" stop-opacity=".52"/>
      <stop offset=".42" stop-color="#20b486" stop-opacity=".18"/>
      <stop offset="1" stop-color="#101625" stop-opacity="0"/>
    </radialGradient>
    <filter id="shadow" x="-20%" y="-20%" width="140%" height="140%">
      <feDropShadow dx="0" dy="18" stdDeviation="24" flood-color="#000" flood-opacity=".36"/>
    </filter>
  </defs>
  <rect width="960" height="960" rx="58" fill="url(#bg)"/>
  <rect width="960" height="960" rx="58" fill="url(#glow)"/>
  <g filter="url(#shadow)">
    <rect x="116" y="128" width="728" height="704" rx="44" fill="#f3f6ff" opacity=".94"/>
    <rect x="154" y="168" width="650" height="368" rx="36" fill="#dce8ff"/>
    <circle cx="286" cy="286" r="82" fill="#ffca5c" opacity=".95"/>
    <path d="M162 520 C260 410 344 382 448 470 C512 524 560 552 642 454 C704 380 756 392 804 470 L804 536 L162 536 Z" fill="#4f7cff" opacity=".82"/>
    <path d="M162 536 C254 464 340 450 438 510 C548 578 624 592 804 488 L804 536 Z" fill="#20b486" opacity=".78"/>
    <text x="480" y="640" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="58" font-weight="800" fill="#172033">{{term}}</text>
    <text x="480" y="704" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="27" font-weight="700" fill="#52607a">{{meaning}}</text>
    <text x="480" y="750" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="24" font-weight="650" fill="#7d89a3">{{pinyin}}</text>
  </g>
  <text x="480" y="890" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="24" font-weight="700" fill="#cdd7ff">{{visual}}</text>
</svg>
""";
        }

        private static string BuildFallbackImagePrompt(CardItem item)
        {
            var term = (item.Term ?? "").Trim();
            var definition = (item.Definition ?? "").Trim();
            return string.IsNullOrWhiteSpace(definition)
                ? $"A clear visual illustration of {term}"
                : $"A clear visual illustration of {term}: {definition}";
        }

        private static string BuildGeminiImagePrompt(CardItem item)
        {
            var term = (item.Term ?? "").Trim();
            var definition = (item.Definition ?? "").Trim();
            return string.IsNullOrWhiteSpace(definition)
                ? term
                : $"{term} - {definition}";
        }

        private static string ExtensionFromMediaType(string mediaType)
        {
            return (mediaType ?? "").ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                _ => ".png"
            };
        }

        private static string BuildFallbackImageAlt(CardItem item)
        {
            var term = (item.Term ?? "").Trim();
            var definition = (item.Definition ?? "").Trim();
            return string.IsNullOrWhiteSpace(definition)
                ? $"áº¢nh minh há»a cho {term}"
                : $"áº¢nh minh há»a cho {term}, nghÄ©a lÃ  {definition}";
        }

        private static string HashText(string text)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? ""));
            return Convert.ToHexString(bytes, 0, 12).ToLowerInvariant();
        }

        private static string Shorten(string text, int maxLength)
        {
            var value = (text ?? "").Trim();
            return value.Length <= maxLength ? value : value.Substring(0, Math.Max(0, maxLength - 3)).Trim() + "...";
        }

        private static string BuildUsageHintForDisplay(GeminiExamplesPayload payload)
        {
            var hint = (payload.MemoryHint ?? "").Trim();
            if (!LooksLikeMnemonicHint(hint))
                return hint;

            var term = (payload.Term ?? "").Trim();
            var definition = (payload.Definition ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(term) && !string.IsNullOrWhiteSpace(definition))
                return $"DÃ¹ng \"{term}\" khi muá»‘n diá»…n Ä‘áº¡t: {definition}.";

            if (!string.IsNullOrWhiteSpace(term))
                return $"DÃ¹ng \"{term}\" trong cÃ¢u tá»± nhiÃªn theo Ä‘Ãºng nghÄ©a cá»§a tháº».";

            return "";
        }

        private static bool LooksLikeMnemonicHint(string hint)
        {
            if (string.IsNullOrWhiteSpace(hint))
                return false;

            var normalized = RemoveDiacritics(hint).ToLowerInvariant();
            return normalized.Contains("am thanh", StringComparison.Ordinal) ||
                   normalized.Contains("nghe giong", StringComparison.Ordinal) ||
                   normalized.Contains("doc giong", StringComparison.Ordinal) ||
                   normalized.Contains("phat am", StringComparison.Ordinal) ||
                   normalized.Contains("lien tuong", StringComparison.Ordinal) ||
                   normalized.Contains("meo nho", StringComparison.Ordinal) ||
                   normalized.Contains("sound", StringComparison.Ordinal) ||
                   normalized.Contains("mnemonic", StringComparison.Ordinal);
        }

        private static string RemoveDiacritics(string value)
        {
            var normalized = (value ?? "").Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                if (ch == '\u0111')
                {
                    sb.Append('d');
                    continue;
                }

                if (ch == '\u0110')
                {
                    sb.Append('D');
                    continue;
                }

                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private void ShowGeminiError(string message)
        {
            var json = JsonSerializer.Serialize(message ?? "KhÃ´ng thá»ƒ táº¡o vÃ­ dá»¥ Gemini.");
            ExecuteScript($"if(window.showGeminiError) window.showGeminiError({json});");
        }
    }
}

