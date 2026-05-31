#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    public sealed partial class CardFormWeb
    {
        private static readonly JsonSerializerOptions WritingJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private void SendWritingOptionsToWeb()
        {
            var defaultVoice = EdgeTtsRunner.ResolveVoiceByLanguageCode(_selectedSet?.LanguageCode);

            PostWriting("writingOptions", new
            {
                courses = _allSets.Select(ToWritingCourseDto).ToList(),
                selectedCourseId = _selectedSet?.Id ?? "",
                voices = EdgeTtsRunner.GetSupportedVoices().Select(v => new
                {
                    voice = v.Voice,
                    label = v.Label,
                    languageKey = v.LanguageKey,
                    languageName = v.LanguageName,
                    languageCode = v.LanguageCode,
                    country = v.Country,
                    gender = v.Gender
                }).ToList(),
                selectedLanguage = VoiceToLanguageKey(defaultVoice),
                defaults = new
                {
                    leftVoice = defaultVoice,
                    rightVoice = PickAlternateVoice(defaultVoice)
                }
            });
        }

        private async Task GenerateWritingPracticeAsync(string data)
        {
            try
            {
                var root = ParsePayload(data);
                var mode = GetString(root, "mode");
                var courseId = GetString(root, "courseId");
                var topic = GetString(root, "topic");
                var difficulty = NormalizeWritingDifficulty(GetString(root, "difficulty"));
                var sentenceCount = WritingSentenceCountForDifficulty(difficulty);
                var targetLanguageName = GetString(root, "targetLanguageName");
                var targetLanguageCode = GetString(root, "targetLanguageCode");

                CardSet? sourceSet = null;
                List<CardItem> vocabulary = new();

                if (string.Equals(mode, "course", StringComparison.OrdinalIgnoreCase))
                {
                    sourceSet = _allSets.FirstOrDefault(s => string.Equals(s.Id ?? "", courseId, StringComparison.Ordinal));
                    if (sourceSet == null)
                        throw new InvalidOperationException("Chọn học phần trước khi tạo đoạn viết.");

                    vocabulary = CardSetStorage.LoadVocabularyItems(sourceSet);
                }
                else if (string.IsNullOrWhiteSpace(topic))
                {
                    throw new InvalidOperationException("Nhập chủ đề trước khi tạo đoạn viết.");
                }

                PostWriting("writingBusy", new { busy = true, phase = "generate" });

                var generated = await GeminiService.GenerateWritingPracticeAsync(
                    sourceSet,
                    vocabulary,
                    topic,
                    sentenceCount,
                    difficulty,
                    targetLanguageName,
                    targetLanguageCode);

                PostWriting("writingPractice", generated);
            }
            catch (Exception ex)
            {
                if (SettingsService.IsLikelyApiKeyError(ex.Message))
                    PromptForApiKeysFromFeature();
                PostWriting("writingToast", new { type = "warn", text = ex.Message });
            }
            finally
            {
                PostWriting("writingBusy", new { busy = false, phase = "generate" });
            }
        }

        private async Task GenerateWritingHintAsync(string data)
        {
            try
            {
                var root = ParsePayload(data);
                var courseId = GetString(root, "courseId");
                var sourceSet = _allSets.FirstOrDefault(s => string.Equals(s.Id ?? "", courseId, StringComparison.Ordinal))
                    ?? _selectedSet;

                var script = new GeminiWritingHintScript
                {
                    VietnameseText = GetString(root, "vietnameseText"),
                    TargetLanguageName = GetString(root, "targetLanguageName"),
                    TargetLanguageCode = GetString(root, "targetLanguageCode"),
                    Topic = GetString(root, "topic"),
                    Difficulty = GetString(root, "difficulty"),
                    UsedVocabulary = GetStringArray(root, "usedVocabulary"),
                    ContextNote = GetString(root, "contextNote")
                };

                if (string.IsNullOrWhiteSpace(script.VietnameseText))
                    throw new InvalidOperationException("Hãy tạo đoạn viết trước khi xem gợi ý.");

                PostWriting("writingBusy", new { busy = true, phase = "hint" });

                var result = await GeminiService.GenerateWritingHintAsync(sourceSet, script);
                PostWriting("writingHint", result);
            }
            catch (Exception ex)
            {
                if (SettingsService.IsLikelyApiKeyError(ex.Message))
                    PromptForApiKeysFromFeature();
                PostWriting("writingToast", new { type = "warn", text = ex.Message });
            }
            finally
            {
                PostWriting("writingBusy", new { busy = false, phase = "hint" });
            }
        }

        private async Task GradeWritingPracticeAsync(string data)
        {
            try
            {
                var root = ParsePayload(data);
                var courseId = GetString(root, "courseId");
                var sourceSet = _allSets.FirstOrDefault(s => string.Equals(s.Id ?? "", courseId, StringComparison.Ordinal))
                    ?? _selectedSet;

                var script = new GeminiWritingGradeScript
                {
                    VietnameseText = GetString(root, "vietnameseText"),
                    ExpectedText = GetString(root, "expectedText"),
                    UserText = GetString(root, "userText"),
                    TargetLanguageName = GetString(root, "targetLanguageName"),
                    TargetLanguageCode = GetString(root, "targetLanguageCode"),
                    Topic = GetString(root, "topic")
                };

                if (string.IsNullOrWhiteSpace(script.VietnameseText) || string.IsNullOrWhiteSpace(script.ExpectedText))
                    throw new InvalidOperationException("Hãy tạo đoạn viết trước khi chấm.");

                if (string.IsNullOrWhiteSpace(script.UserText))
                    throw new InvalidOperationException("Nhập đoạn viết của bạn trước khi gửi chấm.");

                PostWriting("writingBusy", new { busy = true, phase = "grade" });

                var result = await GeminiService.GradeWritingPracticeAsync(sourceSet, script);
                PostWriting("writingGrade", result);
            }
            catch (Exception ex)
            {
                if (SettingsService.IsLikelyApiKeyError(ex.Message))
                    PromptForApiKeysFromFeature();
                PostWriting("writingToast", new { type = "warn", text = ex.Message });
            }
            finally
            {
                PostWriting("writingBusy", new { busy = false, phase = "grade" });
            }
        }

        private object ToCourseDto(CardSet s)
        {
            var items = LoadWritingVocabularyItems(s);
            var total = s.VocabCount > 0 ? s.VocabCount : items.Count;
            var memorized = items.Count(item => item.SrsLevel >= 5);

            return new
            {
                id = s.Id ?? "",
                title = s.Title ?? "Untitled",
                count = total,
                unlearnedCount = Math.Max(0, total - memorized),
                dueCount = SpacedRepetitionService.CountDue(s),
                language = s.Language ?? "",
                languageCode = s.LanguageCode ?? "",
                coverImagePath = s.CoverImagePath ?? "",
                coverImageUrl = ResolveCourseCoverUri(s)
            };
        }

        private static string ResolveCourseCoverUri(CardSet s)
        {
            var cover = CourseCoverImageService.ToWebUri(s.CoverImagePath);
            return string.IsNullOrWhiteSpace(cover)
                ? PixabayImageService.TryGetFirstVocabularyImageUri(s)
                : cover;
        }

        private object ToWritingCourseDto(CardSet s)
        {
            var vocabulary = LoadWritingVocabularyItems(s)
                .Where(x => !string.IsNullOrWhiteSpace(x.Term))
                .Select(x => new
                {
                    term = (x.Term ?? "").Trim(),
                    meaning = (x.Definition ?? "").Trim(),
                    pinyin = (x.Pinyin ?? "").Trim()
                })
                .ToList();

            return new
            {
                id = s.Id ?? "",
                title = s.Title ?? "Untitled",
                count = s.VocabCount > 0 ? s.VocabCount : vocabulary.Count,
                language = s.Language ?? "",
                languageCode = s.LanguageCode ?? "",
                vocabulary
            };
        }

        private static List<CardItem> LoadWritingVocabularyItems(CardSet s)
        {
            if (s.Items != null && s.Items.Count > 0)
                return s.Items;

            try
            {
                return CardSetStorage.LoadVocabularyItems(s);
            }
            catch
            {
                return new List<CardItem>();
            }
        }

        private static string NormalizeWritingDifficulty(string value)
        {
            var normalized = (value ?? "").Trim().ToLowerInvariant();
            return normalized switch
            {
                "hard" or "kho" or "khó" => "hard",
                "advanced" or "nangcao" or "nâng cao" or "nang cao" => "advanced",
                _ => "basic"
            };
        }

        private static int WritingSentenceCountForDifficulty(string difficulty)
        {
            return difficulty switch
            {
                "hard" => 5,
                "advanced" => 7,
                _ => 3
            };
        }

        private void PostWriting(string action, object data)
        {
            var json = JsonSerializer.Serialize(new { action, data }, WritingJsonOptions);
            ExecuteScript($"if(window.handleWritingHostMessage) window.handleWritingHostMessage({json});");
        }

        private static JsonElement ParsePayload(string data)
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(data) ? "{}" : data);
            return doc.RootElement.Clone();
        }

        private static string GetString(JsonElement data, string name)
        {
            try
            {
                if (data.ValueKind == JsonValueKind.Object &&
                    data.TryGetProperty(name, out var prop))
                {
                    return prop.ValueKind == JsonValueKind.String
                        ? prop.GetString() ?? ""
                        : prop.ToString();
                }
            }
            catch { }

            return "";
        }

        private static List<string> GetStringArray(JsonElement data, string name)
        {
            try
            {
                if (data.ValueKind == JsonValueKind.Object &&
                    data.TryGetProperty(name, out var prop) &&
                    prop.ValueKind == JsonValueKind.Array)
                {
                    return prop.EnumerateArray()
                        .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : x.ToString())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x.Trim())
                        .ToList();
                }
            }
            catch { }

            return new List<string>();
        }

        private static int GetInt(JsonElement data, string name, int fallback)
        {
            try
            {
                if (data.ValueKind == JsonValueKind.Object &&
                    data.TryGetProperty(name, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
                        return value;

                    if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
                        return parsed;
                }
            }
            catch { }

            return fallback;
        }

        private static string VoiceToLanguageKey(string? voice)
        {
            var found = EdgeTtsRunner.GetSupportedVoices()
                .FirstOrDefault(v => string.Equals(v.Voice, voice, StringComparison.OrdinalIgnoreCase));

            if (found != null && !string.IsNullOrWhiteSpace(found.LanguageKey))
                return found.LanguageKey;

            var value = (voice ?? "").Trim();
            var dash = value.IndexOf('-', StringComparison.Ordinal);
            return dash > 0 ? value.Substring(0, dash).ToLowerInvariant() : "en";
        }

        private static string PickAlternateVoice(string? voice)
        {
            var current = (voice ?? "").Trim();
            var voices = EdgeTtsRunner.GetSupportedVoices();
            var source = voices.FirstOrDefault(v => string.Equals(v.Voice, current, StringComparison.OrdinalIgnoreCase));
            if (source != null)
            {
                var alternateGender = string.Equals(source.Gender, "male", StringComparison.OrdinalIgnoreCase)
                    ? "female"
                    : "male";

                var sameLanguage = voices.FirstOrDefault(v =>
                    string.Equals(v.LanguageKey, source.LanguageKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(v.Gender, alternateGender, StringComparison.OrdinalIgnoreCase));

                if (sameLanguage != null)
                    return sameLanguage.Voice;
            }

            return string.IsNullOrWhiteSpace(current) ? "en-US-GuyNeural" : current;
        }
    }
}
