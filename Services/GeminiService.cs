using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public sealed class GeminiExampleItem
    {
        public string Source { get; set; } = "";
        public string Vietnamese { get; set; } = "";
        public string Note { get; set; } = "";
    }

    public sealed class GeminiExamplesPayload
    {
        public string Term { get; set; } = "";
        public string Definition { get; set; } = "";
        public string Pinyin { get; set; } = "";
        public List<GeminiExampleItem> Examples { get; set; } = new();
        public string MemoryHint { get; set; } = "";
        public string ImagePrompt { get; set; } = "";
        public string ImageAlt { get; set; } = "";
        public string ImagePath { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
    }

    public sealed class GeminiBulkExamplesPayload
    {
        public List<GeminiExamplesPayload> Items { get; set; } = new();
    }

    public sealed class GeminiSentenceQuizPayload
    {
        public List<GeminiSentenceQuestion> Questions { get; set; } = new();
    }

    public sealed class GeminiDialoguePayload
    {
        public string Title { get; set; } = "";
        public List<GeminiDialogueLine> Messages { get; set; } = new();
    }

    public sealed class GeminiDialogueLine
    {
        public string Side { get; set; } = "left";
        public string Text { get; set; } = "";
        public string Vietnamese { get; set; } = "";
        public string VietnamesePronunciation { get; set; } = "";
        public double PauseSeconds { get; set; } = 0.8;
    }

    public sealed class GeminiSentenceQuestion
    {
        public int Index { get; set; }
        public string Prompt { get; set; } = "";
        public List<string> Words { get; set; } = new();
        public string ExpectedAnswer { get; set; } = "";
        public string EnglishMeaning { get; set; } = "";
        public string Vietnamese { get; set; } = "";
        public string Explanation { get; set; } = "";
    }

    public sealed class GeminiEssayGradePayload
    {
        public List<GeminiEssayGradeItem> Items { get; set; } = new();
    }

    public sealed class GeminiEssayGradeItem
    {
        public int Index { get; set; }
        public bool IsCorrect { get; set; }
        public string AcceptedAnswer { get; set; } = "";
        public string Explanation { get; set; } = "";
    }

    public sealed class GeminiEssayAnswerScript
    {
        public int Index { get; set; }
        public string Prompt { get; set; } = "";
        public string CorrectAnswer { get; set; } = "";
        public string UserAnswer { get; set; } = "";
        public bool Skipped { get; set; }
        public bool AnswerIsChinese { get; set; }
    }

    public sealed class GeminiWritingPracticePayload
    {
        public string Title { get; set; } = "";
        public string Topic { get; set; } = "";
        public string VietnameseText { get; set; } = "";
        public string TargetText { get; set; } = "";
        public string TargetLanguageName { get; set; } = "";
        public string TargetLanguageCode { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public List<string> UsedVocabulary { get; set; } = new();
        public string ContextNote { get; set; } = "";
    }

    public sealed class GeminiWritingGradePayload
    {
        public int Score { get; set; }
        public string OverallFeedback { get; set; } = "";
        public string SuggestedRewrite { get; set; } = "";
        public List<GeminiWritingIssue> Issues { get; set; } = new();
    }

    public sealed class GeminiWritingHintPayload
    {
        public List<string> KeyIdeas { get; set; } = new();
        public List<string> WordHints { get; set; } = new();
        public List<string> StructureHints { get; set; } = new();
    }

    public sealed class GeminiWritingIssue
    {
        public string WrongText { get; set; } = "";
        public string Correction { get; set; } = "";
        public string Explanation { get; set; } = "";
        public string Type { get; set; } = "";
    }

    public sealed class GeminiWritingGradeScript
    {
        public string VietnameseText { get; set; } = "";
        public string ExpectedText { get; set; } = "";
        public string UserText { get; set; } = "";
        public string TargetLanguageName { get; set; } = "";
        public string TargetLanguageCode { get; set; } = "";
        public string Topic { get; set; } = "";
    }

    public sealed class GeminiWritingHintScript
    {
        public string VietnameseText { get; set; } = "";
        public string TargetLanguageName { get; set; } = "";
        public string TargetLanguageCode { get; set; } = "";
        public string Topic { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public List<string> UsedVocabulary { get; set; } = new();
        public string ContextNote { get; set; } = "";
    }

    public static class GeminiService
    {
        private const string DefaultImageModel = "gemini-3.1-flash-image";

        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(90)
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public static bool IsConfigured()
        {
            var settings = SettingsService.GetGeminiSettings();
            return !string.IsNullOrWhiteSpace(settings.ApiKey);
        }

        public static async Task<(byte[] Bytes, string MimeType)> GenerateImageAsync(
            string prompt,
            CancellationToken cancellationToken = default)
        {
            var settings = SettingsService.GetGeminiSettings();
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
                throw new InvalidOperationException("Chưa cấu hình Gemini API key.");

            var cleanPrompt = (prompt ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cleanPrompt))
                throw new InvalidOperationException("Không có từ vựng để tạo ảnh.");

            var model = SelectImageModel(settings.Model);
            var url =
                $"https://generativelanguage.googleapis.com/v1/models/{Uri.EscapeDataString(model)}:generateContent";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.TryAddWithoutValidation("x-goog-api-key", settings.ApiKey.Trim());

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = cleanPrompt }
                        }
                    }
                },
                generationConfig = new
                {
                    responseModalities = new[] { "IMAGE" },
                    responseFormat = new
                    {
                        image = new
                        {
                            aspectRatio = "1:1"
                        }
                    }
                }
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await Http.SendAsync(request, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(ParseGeminiError(responseText, response.StatusCode));

            return ExtractInlineImage(responseText);
        }

        public static async Task<GeminiExamplesPayload> GenerateExamplesAsync(
            CardSet set,
            CardItem item,
            CancellationToken cancellationToken = default)
        {
            var prompt = BuildExamplesPrompt(set, item);
            var result = await GenerateJsonAsync<GeminiExamplesPayload>(prompt, 0.65, cancellationToken);

            result.Term = item.Term ?? "";
            result.Definition = item.Definition ?? "";
            result.Pinyin = item.Pinyin ?? "";
            result.ImagePrompt = (result.ImagePrompt ?? "").Trim();
            result.ImageAlt = (result.ImageAlt ?? "").Trim();
            result.UpdatedAt = DateTimeOffset.Now.ToString("O");

            result.Examples ??= new List<GeminiExampleItem>();
            result.Examples = result.Examples
                .Where(x => !string.IsNullOrWhiteSpace(x.Source) || !string.IsNullOrWhiteSpace(x.Vietnamese))
                .Take(4)
                .ToList();

            while (result.Examples.Count < 2)
            {
                result.Examples.Add(new GeminiExampleItem
                {
                    Source = item.Term ?? "",
                    Vietnamese = item.Definition ?? "",
                    Note = "Gemini chưa trả đủ ví dụ, hãy bấm tạo mới để thử lại."
                });
            }

            return result;
        }

        public static async Task GenerateBulkExamplesAsync(CardSet set, CancellationToken cancellationToken = default)
        {
            if (set.Items == null || set.Items.Count == 0) return;

            var itemsToGenerate = new List<CardItem>();
            foreach (var item in set.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Term)) continue;
                if (GeminiExampleStore.TryGet(set, item) == null)
                {
                    itemsToGenerate.Add(item);
                }
            }

            if (itemsToGenerate.Count == 0) return;

            // Chunk to avoid exceeding token limits. 20 items per request is a safe number.
            int chunkSize = 20;
            for (int i = 0; i < itemsToGenerate.Count; i += chunkSize)
            {
                var chunk = itemsToGenerate.Skip(i).Take(chunkSize).ToList();
                var prompt = BuildBulkExamplesPrompt(set, chunk);
                
                try
                {
                    var result = await GenerateJsonAsync<GeminiBulkExamplesPayload>(prompt, 0.65, cancellationToken);
                    if (result?.Items != null)
                    {
                        foreach (var chunkItem in chunk)
                        {
                            var match = result.Items.FirstOrDefault(x => string.Equals(x.Term, chunkItem.Term, StringComparison.OrdinalIgnoreCase));
                            if (match != null)
                            {
                                var existing = GeminiExampleStore.TryGet(set, chunkItem);
                                match.Term = chunkItem.Term ?? "";
                                match.Definition = chunkItem.Definition ?? "";
                                match.Pinyin = chunkItem.Pinyin ?? "";
                                match.ImagePrompt = (match.ImagePrompt ?? "").Trim();
                                match.ImageAlt = (match.ImageAlt ?? "").Trim();
                                match.UpdatedAt = DateTimeOffset.Now.ToString("O");
                                
                                match.ImagePath = existing?.ImagePath ?? "";
                                if (string.IsNullOrWhiteSpace(match.ImagePrompt))
                                    match.ImagePrompt = existing?.ImagePrompt ?? "";
                                if (string.IsNullOrWhiteSpace(match.ImageAlt))
                                    match.ImageAlt = existing?.ImageAlt ?? "";
                                
                                match.Examples ??= new List<GeminiExampleItem>();
                                match.Examples = match.Examples
                                    .Where(x => !string.IsNullOrWhiteSpace(x.Source) || !string.IsNullOrWhiteSpace(x.Vietnamese))
                                    .Take(4)
                                    .ToList();

                                GeminiExampleStore.Save(set, chunkItem, match);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Gemini Bulk Generate Error: {ex.Message}");
                }
            }
        }

        public static async Task<GeminiSentenceQuizPayload> GenerateSentenceQuizAsync(
            CardSet set,
            int count,
            CancellationToken cancellationToken = default)
        {
            var prompt = BuildSentenceQuizPrompt(set, count);
            var result = await GenerateJsonAsync<GeminiSentenceQuizPayload>(prompt, 0.7, cancellationToken);

            result.Questions ??= new List<GeminiSentenceQuestion>();
            result.Questions = result.Questions
                .Where(q =>
                    q.Words != null &&
                    q.Words.Count > 0 &&
                    (!string.IsNullOrWhiteSpace(q.Prompt) || !string.IsNullOrWhiteSpace(q.ExpectedAnswer)) &&
                    !string.IsNullOrWhiteSpace(q.EnglishMeaning) &&
                    !string.IsNullOrWhiteSpace(q.Vietnamese))
                .Take(Math.Max(1, count))
                .ToList();

            for (int i = 0; i < result.Questions.Count; i++)
            {
                var sourceSentence = string.IsNullOrWhiteSpace(result.Questions[i].Prompt)
                    ? result.Questions[i].ExpectedAnswer
                    : result.Questions[i].Prompt;

                result.Questions[i].Index = i + 1;
                result.Questions[i].Prompt = (sourceSentence ?? "").Trim();
                result.Questions[i].ExpectedAnswer = string.IsNullOrWhiteSpace(result.Questions[i].ExpectedAnswer)
                    ? result.Questions[i].Prompt
                    : result.Questions[i].ExpectedAnswer.Trim();
                result.Questions[i].EnglishMeaning = (result.Questions[i].EnglishMeaning ?? "").Trim();
                result.Questions[i].Vietnamese = (result.Questions[i].Vietnamese ?? "").Trim();
                result.Questions[i].Explanation = (result.Questions[i].Explanation ?? "").Trim();
            }

            return result;
        }

        public static async Task<GeminiDialoguePayload> GenerateDialogueAsync(
            CardSet? set,
            IEnumerable<CardItem>? vocabulary,
            string? topic,
            int messageCount,
            string? targetLanguageName = null,
            string? targetLanguageCode = null,
            bool includeVietnameseAids = false,
            CancellationToken cancellationToken = default)
        {
            var prompt = BuildDialoguePrompt(
                set,
                vocabulary,
                topic,
                messageCount,
                targetLanguageName,
                targetLanguageCode,
                includeVietnameseAids);
            var result = await GenerateJsonAsync<GeminiDialoguePayload>(prompt, 0.75, cancellationToken);

            result.Title = string.IsNullOrWhiteSpace(result.Title)
                ? FirstNonEmpty(topic, set?.Title, "Generated dialogue")
                : result.Title.Trim();

            result.Messages ??= new List<GeminiDialogueLine>();
            result.Messages = result.Messages
                .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                .Take(Math.Max(2, messageCount))
                .Select((x, i) => new GeminiDialogueLine
                {
                    Side = string.Equals(x.Side, "right", StringComparison.OrdinalIgnoreCase) ? "right" : "left",
                    Text = (x.Text ?? "").Trim(),
                    Vietnamese = (x.Vietnamese ?? "").Trim(),
                    VietnamesePronunciation = (x.VietnamesePronunciation ?? "").Trim(),
                    PauseSeconds = x.PauseSeconds <= 0 ? 0.8 : Math.Min(8, x.PauseSeconds)
                })
                .ToList();

            if (result.Messages.Count == 1)
            {
                result.Messages.Add(new GeminiDialogueLine
                {
                    Side = "right",
                    Text = "Yes, I understand.",
                    PauseSeconds = 0.8
                });
            }

            for (int i = 0; i < result.Messages.Count; i++)
                result.Messages[i].Side = i % 2 == 0 ? "left" : "right";

            return result;
        }

        public static Task<GeminiEssayGradePayload> GradeEssayAsync(
            CardSet? set,
            IEnumerable<GeminiEssayAnswerScript> answers,
            CancellationToken cancellationToken = default)
        {
            var prompt = BuildEssayGradePrompt(set, answers);
            return GenerateJsonAsync<GeminiEssayGradePayload>(prompt, 0.2, cancellationToken);
        }

        public static async Task<GeminiWritingPracticePayload> GenerateWritingPracticeAsync(
            CardSet? set,
            IEnumerable<CardItem>? vocabulary,
            string? topic,
            int sentenceCount,
            string? difficulty,
            string? targetLanguageName,
            string? targetLanguageCode,
            CancellationToken cancellationToken = default)
        {
            var difficultyKey = NormalizeWritingDifficultyKey(difficulty);
            var difficultyLabel = WritingDifficultyLabel(difficultyKey);
            var difficultyInstruction = WritingDifficultyInstruction(difficultyKey);
            var count = Math.Max(3, Math.Min(8, sentenceCount));
            var sourceWords = (vocabulary ?? Enumerable.Empty<CardItem>())
                .Where(x => !string.IsNullOrWhiteSpace(x.Term))
                .ToList();
            var words = SampleVocabulary(sourceWords, 90);

            var webContext = await FetchWebSearchContextAsync(
                BuildWritingSearchQuery(set, words, topic, targetLanguageName),
                cancellationToken);

            var prompt = BuildWritingPracticePrompt(
                set,
                words,
                topic,
                count,
                difficultyLabel,
                difficultyInstruction,
                targetLanguageName,
                targetLanguageCode,
                webContext);

            var result = await GenerateJsonAsync<GeminiWritingPracticePayload>(prompt, 0.72, cancellationToken);

            result.Title = FirstNonEmpty(result.Title, topic, set?.Title, "Writing practice");
            result.Topic = FirstNonEmpty(result.Topic, topic, set?.Title, "daily communication");
            result.TargetLanguageName = FirstNonEmpty(result.TargetLanguageName, targetLanguageName, set?.Language, "English");
            result.TargetLanguageCode = FirstNonEmpty(result.TargetLanguageCode, targetLanguageCode, set?.LanguageCode, "en");
            result.Difficulty = FirstNonEmpty(result.Difficulty, difficultyLabel);
            result.VietnameseText = (result.VietnameseText ?? "").Trim();
            result.TargetText = (result.TargetText ?? "").Trim();
            result.ContextNote = (result.ContextNote ?? "").Trim();
            result.UsedVocabulary ??= new List<string>();
            result.UsedVocabulary = result.UsedVocabulary
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(18)
                .ToList();

            if (string.IsNullOrWhiteSpace(result.VietnameseText) || string.IsNullOrWhiteSpace(result.TargetText))
                throw new InvalidOperationException("Gemini chưa tạo đủ đoạn tiếng Việt và đoạn ngôn ngữ đích.");

            return result;
        }

        public static async Task<GeminiWritingGradePayload> GradeWritingPracticeAsync(
            CardSet? set,
            GeminiWritingGradeScript script,
            CancellationToken cancellationToken = default)
        {
            var prompt = BuildWritingGradePrompt(set, script);
            var result = await GenerateJsonAsync<GeminiWritingGradePayload>(prompt, 0.2, cancellationToken);

            result.Score = Math.Max(0, Math.Min(100, result.Score));
            result.OverallFeedback = (result.OverallFeedback ?? "").Trim();
            result.SuggestedRewrite = (result.SuggestedRewrite ?? "").Trim();
            result.Issues ??= new List<GeminiWritingIssue>();
            result.Issues = result.Issues
                .Where(x => !string.IsNullOrWhiteSpace(x.WrongText) || !string.IsNullOrWhiteSpace(x.Explanation))
                .Take(18)
                .Select(x => new GeminiWritingIssue
                {
                    WrongText = (x.WrongText ?? "").Trim(),
                    Correction = (x.Correction ?? "").Trim(),
                    Explanation = (x.Explanation ?? "").Trim(),
                    Type = (x.Type ?? "").Trim()
                })
                .ToList();

            if (string.IsNullOrWhiteSpace(result.SuggestedRewrite))
                result.SuggestedRewrite = (script.ExpectedText ?? "").Trim();

            return result;
        }

        public static async Task<GeminiWritingHintPayload> GenerateWritingHintAsync(
            CardSet? set,
            GeminiWritingHintScript script,
            CancellationToken cancellationToken = default)
        {
            var prompt = BuildWritingHintPrompt(set, script);
            var result = await GenerateJsonAsync<GeminiWritingHintPayload>(prompt, 0.35, cancellationToken);

            result.KeyIdeas = NormalizeHintList(result.KeyIdeas, 4);
            result.WordHints = NormalizeHintList(result.WordHints, 6);
            result.StructureHints = NormalizeHintList(result.StructureHints, 4);

            return result;
        }

        private static async Task<T> GenerateJsonAsync<T>(
            string prompt,
            double temperature,
            CancellationToken cancellationToken)
        {
            var settings = SettingsService.GetGeminiSettings();
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
                throw new InvalidOperationException("Chưa cấu hình Gemini API key. Hãy mở cài đặt ngoài màn hình chính để gắn key.");

            var model = NormalizeModel(settings.Model);
            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(settings.ApiKey)}";

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = prompt } }
                    }
                },
                generationConfig = new
                {
                    temperature,
                    responseMimeType = "application/json"
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
            };

            using var response = await Http.SendAsync(request, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(ParseGeminiError(responseText, response.StatusCode));

            var jsonText = ExtractText(responseText);
            var cleaned = CleanJson(jsonText);

            try
            {
                var parsed = JsonSerializer.Deserialize<T>(cleaned, JsonOptions);
                if (parsed != null) return parsed;
            }
            catch
            {
                var objectOnly = ExtractFirstJsonObject(cleaned);
                if (!string.Equals(objectOnly, cleaned, StringComparison.Ordinal))
                {
                    var parsed = JsonSerializer.Deserialize<T>(objectOnly, JsonOptions);
                    if (parsed != null) return parsed;
                }
            }

            throw new InvalidOperationException("Gemini trả về dữ liệu không đúng định dạng JSON.");
        }

        private static string BuildExamplesPrompt(CardSet set, CardItem item)
        {
            return $$"""
You are a precise vocabulary-learning assistant for a flashcard app.
Create short, natural, memorable usage examples for exactly one card.

Course title: {{set.Title}}
Course language: {{set.Language}} ({{set.LanguageCode}})
Vocabulary term: {{item.Term}}
Vietnamese meaning: {{item.Definition}}
Pronunciation/Pinyin: {{item.Pinyin}}

Hard requirements:
- Return only one valid JSON object. Do not use Markdown, comments, prose, or code fences.
- The JSON must match the schema exactly. Use double quotes for every key and string.
- Generate 2 to 4 examples.
- Every example must use the exact target term naturally. If inflection or spacing is required by the language, keep the target word recognizable.
- The example sentence must be in the course language. If the course is zh-TW or Traditional Chinese, prefer Traditional Chinese characters.
- "vietnamese" must be a natural Vietnamese translation of the example sentence.
- "note" must be a short Vietnamese usage note, not another translation.
- "memoryHint" must be Vietnamese and must explain real usage: when to use the term, part of speech, tone/register, common collocation, or grammar pattern.
- Do not create sound-based mnemonics, pronunciation jokes, puns, or fake etymology.
- "imagePath" must always be an empty string.
- "imageAlt" must be a concise Vietnamese visual description for the term, without mentioning Gemini, AI, prompts, or image generation.
- Do not invent cultural facts, proper nouns, or rare idioms unless the term requires them.

Output schema:
{
  "examples": [
    { "source": "example sentence in the course language", "vietnamese": "natural Vietnamese translation", "note": "short Vietnamese usage note" }
  ],
  "memoryHint": "Vietnamese usage hint",
  "imagePath": "",
  "imageAlt": "Vietnamese visual description"
}
""";
        }

        private static string BuildBulkExamplesPrompt(CardSet set, List<CardItem> items)
        {
            var terms = items.Select(x => new
            {
                term = (x.Term ?? "").Trim(),
                definition = (x.Definition ?? "").Trim(),
                pinyin = (x.Pinyin ?? "").Trim()
            }).ToList();

            var vocabJson = JsonSerializer.Serialize(terms, JsonOptions);

            return $@"You are a precise vocabulary-learning assistant for a flashcard app.
Create short, natural usage examples for every vocabulary item in the input list.

Course title: {set?.Title}
Course language: {set?.Language} ({set?.LanguageCode})

Vocabulary list JSON:
{vocabJson}

Hard requirements:
- Return only one valid JSON object. Do not use Markdown, comments, prose, or code fences.
- The top-level object must contain only the ""items"" array.
- Return one output object for every input item, preserving the original ""term"" string exactly.
- Each item must contain 2 to 4 examples.
- Each example must use its own term naturally and must not accidentally explain a different term.
- Example sentences must be in the course language. If the course is zh-TW or Traditional Chinese, prefer Traditional Chinese characters.
- Each ""vietnamese"" field must be a natural Vietnamese translation of the example sentence.
- Each ""note"" field must be a short Vietnamese usage note.
- ""memoryHint"" must be Vietnamese and must explain real usage: when to use the term, part of speech, tone/register, common collocation, or grammar pattern.
- Do not create sound-based mnemonics, pronunciation jokes, puns, or fake etymology.
- ""imagePath"" must always be an empty string.
- ""imageAlt"" must be a concise Vietnamese visual description for the term, without mentioning Gemini, AI, prompts, or image generation.
- If an input term is ambiguous, choose the meaning provided by its ""definition"" field.

Output schema:
{{
  ""items"": [
    {{
      ""term"": ""original input term"",
      ""examples"": [
        {{ ""source"": ""example sentence in the course language"", ""vietnamese"": ""natural Vietnamese translation"", ""note"": ""short Vietnamese usage note"" }}
      ],
      ""memoryHint"": ""Vietnamese usage hint"",
      ""imagePath"": """",
      ""imageAlt"": ""Vietnamese visual description""
    }}
  ]
}}";
        }

        private static string BuildSentenceQuizPrompt(CardSet set, int count)
        {
            var terms = (set.Items ?? new List<CardItem>())
                .Where(x => !string.IsNullOrWhiteSpace(x.Term))
                .Select(x => new
                {
                    term = (x.Term ?? "").Trim(),
                    meaning = (x.Definition ?? "").Trim(),
                    pinyin = (x.Pinyin ?? "").Trim()
                })
                .ToList();

            var vocabJson = JsonSerializer.Serialize(terms, JsonOptions);

            return $$"""
You are a language teacher creating sentence-meaning quiz questions from a vocabulary course.
Create exactly {{count}} complete, natural communication sentences in the course language: {{set.Language}} ({{set.LanguageCode}}).
The learner will see each sentence and type its meaning in English, Vietnamese, or both depending on app settings.

Vocabulary list JSON:
{{vocabJson}}

Hard requirements:
- Return only one valid JSON object. Do not use Markdown, comments, prose, or code fences.
- The top-level object must contain only the "questions" array.
- Create exactly {{count}} question objects.
- "prompt" must be a complete ready-to-show sentence in the course language. Do not write instructions such as "make a sentence".
- If the course language is zh-TW or Traditional Chinese, prefer Traditional Chinese characters.
- "expectedAnswer" must repeat the same sentence as "prompt" for backward compatibility.
- "englishMeaning" must be a natural English translation of "prompt".
- "vietnamese" must be a natural Vietnamese translation of "prompt"; never copy English text into this field.
- Each sentence should use 1 to 4 vocabulary items when natural.
- Spread vocabulary across questions; avoid using the same word repeatedly unless the list is very small.
- "words" must contain only source vocabulary terms that actually appear in or are clearly used by "prompt".
- "explanation" must be short Vietnamese feedback explaining why the meaning is correct.
- Sentences should be practical, everyday, and understandable without extra context.

Output schema:
{
  "questions": [
    {
      "prompt": "I feel both tired and hungry after class.",
      "words": ["source vocabulary term 1", "source vocabulary term 2"],
      "expectedAnswer": "I feel both tired and hungry after class.",
      "englishMeaning": "I am tired and hungry after class.",
      "vietnamese": "Natural Vietnamese translation.",
      "explanation": "Short Vietnamese explanation."
    }
  ]
}
""";
        }

        private static string BuildDialoguePrompt(
            CardSet? set,
            IEnumerable<CardItem>? vocabulary,
            string? topic,
            int messageCount,
            string? targetLanguageName,
            string? targetLanguageCode,
            bool includeVietnameseAids)
        {
            var words = (vocabulary ?? Enumerable.Empty<CardItem>())
                .Where(x => !string.IsNullOrWhiteSpace(x.Term))
                .Take(80)
                .Select(x => new
                {
                    term = (x.Term ?? "").Trim(),
                    meaning = (x.Definition ?? "").Trim(),
                    pinyin = (x.Pinyin ?? "").Trim()
                })
                .ToList();

            var vocabJson = JsonSerializer.Serialize(words, JsonOptions);
            var count = Math.Max(2, Math.Min(24, messageCount));
            var source = string.IsNullOrWhiteSpace(topic)
                ? "course vocabulary"
                : topic.Trim();
            var languageName = FirstNonEmpty(targetLanguageName, set?.Language, "English");
            var languageCode = FirstNonEmpty(targetLanguageCode, set?.LanguageCode, "en");
            var learningAidRules = includeVietnameseAids
                ? """
- "vietnamese" must be a natural Vietnamese translation of the message.
- "vietnamesePronunciation" must show an approximate Vietnamese-style reading of the target-language text, for example "Hello" -> "hé lô". It is a pronunciation aid, not a translation.
"""
                : "";
            var messageSchema = includeVietnameseAids
                ? """
    { "side": "left", "text": "message text", "vietnamese": "bản dịch tiếng Việt", "vietnamesePronunciation": "cách đọc gần âm tiếng Việt", "pauseSeconds": 0.8 },
    { "side": "right", "text": "message text", "vietnamese": "bản dịch tiếng Việt", "vietnamesePronunciation": "cách đọc gần âm tiếng Việt", "pauseSeconds": 0.8 }
"""
                : """
    { "side": "left", "text": "message text", "pauseSeconds": 0.8 },
    { "side": "right", "text": "message text", "pauseSeconds": 0.8 }
""";

            return $$"""
You are a language teacher creating a short listening dialogue for a vocabulary-learning app.
Create a natural two-person dialogue with exactly {{count}} short messages.

Course title: {{set?.Title}}
Course language: {{set?.Language}} ({{set?.LanguageCode}})
Required dialogue language: {{languageName}} ({{languageCode}})
Topic/context: {{source}}

Vocabulary list JSON:
{{vocabJson}}

Hard requirements:
- Return only one valid JSON object. Do not use Markdown, comments, prose, or code fences.
- Use exactly the required dialogue language. Do not switch to English unless English is the required language.
- If the language is zh-TW or Traditional Chinese, prefer Traditional Chinese characters.
- Messages must be short, spoken, and TTS-friendly. Avoid long paragraphs.
- Use some vocabulary items naturally if available; do not force every word.
- "side" must be only "left" or "right".
- Alternate speakers strictly: left, right, left, right...
- "pauseSeconds" must be a number between 0.6 and 1.4.
{{learningAidRules}}
- The title must be short, folder-safe, and in English.
- The conversation must have a clear mini-situation and a natural ending.

Output schema:
{
  "title": "short English folder-safe dialogue title",
  "messages": [
{{messageSchema}}
  ]
}
""";
        }

        private static string BuildWritingPracticePrompt(
            CardSet? set,
            IEnumerable<CardItem> vocabulary,
            string? topic,
            int sentenceCount,
            string difficultyLabel,
            string difficultyInstruction,
            string? targetLanguageName,
            string? targetLanguageCode,
            string webContext)
        {
            var words = vocabulary
                .Where(x => !string.IsNullOrWhiteSpace(x.Term))
                .Select(x => new
                {
                    term = (x.Term ?? "").Trim(),
                    meaning = (x.Definition ?? "").Trim(),
                    pinyin = (x.Pinyin ?? "").Trim()
                })
                .ToList();

            var vocabJson = JsonSerializer.Serialize(words, JsonOptions);
            var minVocabularyUse = Math.Min(words.Count, Math.Max(3, (int)Math.Ceiling(sentenceCount * 0.8)));
            var maxVocabularyUse = Math.Min(words.Count, Math.Max(minVocabularyUse, sentenceCount + 3));
            var source = string.IsNullOrWhiteSpace(topic)
                ? FirstNonEmpty(set?.Title, "daily communication")
                : topic.Trim();
            var languageName = FirstNonEmpty(targetLanguageName, set?.Language, "English");
            var languageCode = FirstNonEmpty(targetLanguageCode, set?.LanguageCode, "en");

            return $$"""
You are a language teacher creating a short guided writing exercise.
The app provides a topic, optional web-search context, and course vocabulary. Create a realistic Vietnamese source text plus an equivalent target-language answer.

Course title: {{set?.Title}}
Course language: {{set?.Language}} ({{set?.LanguageCode}})
Target writing language: {{languageName}} ({{languageCode}})
Topic/context: {{source}}
Approximate Vietnamese sentence count: {{sentenceCount}}
Difficulty label: {{difficultyLabel}}
Difficulty instruction: {{difficultyInstruction}}

Web context, if useful:
{{webContext}}

Vocabulary list JSON:
{{vocabJson}}

Hard requirements:
- Return only one valid JSON object. Do not use Markdown, comments, prose, or code fences.
- Create both a Vietnamese source paragraph and a target-language paragraph.
- "vietnameseText" must be natural Vietnamese and approximately {{sentenceCount}} short sentences.
- "targetText" must express the same meaning as "vietnameseText" in the exact target writing language.
- The two paragraphs must be close enough in meaning to support grading, but the target text should sound natural in its language.
- Difficulty must follow "{{difficultyLabel}}": {{difficultyInstruction}}.
- The topic must be realistic and useful for daily communication, work, study, errands, light news, or practical life.
- Avoid generic AI-sounding filler, slogans, motivational fluff, and overly formal writing unless the topic requires formality.
- The vocabulary list is randomly sampled from the course for this generation. If vocabulary is available, choose about {{minVocabularyUse}} to {{maxVocabularyUse}} different terms/phrases from varied positions in the list.
- Prefer a fresh mix of course vocabulary; do not keep reusing the same familiar few words when many alternatives are available.
- Use course vocabulary naturally in the target-language paragraph. Do not stuff too many words into one paragraph.
- The Vietnamese source paragraph must preserve the same meaning so the learner can translate/rewrite it accurately.
- If the target language is zh-TW or Traditional Chinese, prefer Traditional Chinese characters.
- "usedVocabulary" must list only terms that actually appear or are clearly used.
- "contextNote" must be a short Vietnamese note explaining the situation and how the topic/vocabulary was used.
- Preserve the requested target language name and code in the output.

Output schema:
{
  "title": "short title",
  "topic": "topic used",
  "difficulty": "{{difficultyLabel}}",
  "vietnameseText": "natural Vietnamese source paragraph",
  "targetText": "equivalent paragraph in the target language",
  "targetLanguageName": "{{languageName}}",
  "targetLanguageCode": "{{languageCode}}",
  "usedVocabulary": ["used source vocabulary term"],
  "contextNote": "short Vietnamese note"
}
""";
        }

        private static string BuildEssayGradePrompt(CardSet? set, IEnumerable<GeminiEssayAnswerScript> answers)
        {
            var answerJson = JsonSerializer.Serialize(answers, JsonOptions);

            return $$"""
You are a fair but strict grader for a vocabulary short-answer quiz.
Grade every learner answer flexibly for meaning, but do not accept answers that change the meaning.

Course title: {{set?.Title}}
Course language: {{set?.Language}} ({{set?.LanguageCode}})

Answer list JSON:
{{answerJson}}

Grading rules:
- Return only one valid JSON object. Do not use Markdown, comments, prose, or code fences.
- Return one item for every input answer and preserve each input index exactly.
- If skipped is true, mark it incorrect.
- If answerIsChinese is true, focus on Chinese characters and meaning; ignore minor punctuation or spacing differences.
- If answerIsChinese is false, accept clear synonyms, minor Vietnamese tone-mark mistakes, and equivalent wording.
- Mark incorrect if the learner answer omits the core idea, reverses the meaning, uses the wrong word, or answers a different prompt.
- "acceptedAnswer" should be the expected/correct answer or the learner wording if it is acceptable.
- "explanation" must be concise Vietnamese feedback explaining why the answer is correct or incorrect.

Output schema:
{
  "items": [
    {
      "index": 1,
      "isCorrect": true,
      "acceptedAnswer": "correct or accepted answer",
      "explanation": "short Vietnamese reason"
    }
  ]
}
""";
        }

        private static string BuildWritingHintPrompt(CardSet? set, GeminiWritingHintScript script)
        {
            var hintJson = JsonSerializer.Serialize(new
            {
                script.VietnameseText,
                script.TargetLanguageName,
                script.TargetLanguageCode,
                script.Topic,
                script.Difficulty,
                UsedVocabulary = (script.UsedVocabulary ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(18)
                    .ToList(),
                script.ContextNote
            }, JsonOptions);

            return $$"""
You are a writing-practice hint assistant.
The learner sees a Vietnamese paragraph and must rewrite the meaning in the target language. Give hints only. Do not write the full answer.

Course title: {{set?.Title}}
Course language: {{set?.Language}} ({{set?.LanguageCode}})

Writing task JSON:
{{hintJson}}

Strict rules:
- Return only one valid JSON object. Do not use Markdown, comments, prose, or code fences.
- Do not translate the whole Vietnamese paragraph.
- Do not provide targetText or a complete answer.
- Do not create a sequence of complete sentences that can be copied as the answer.
- Each hint must be short, ideally under 18 Vietnamese words.
- Hints must be in Vietnamese, but may include target-language phrases or grammar labels when useful.
- "keyIdeas" should list ideas that must be included, not model sentences.
- "wordHints" should suggest vocabulary, collocations, register, prepositions, particles, or common mistakes.
- "structureHints" should suggest tense/aspect, sentence order, connectors, or useful grammar patterns.
- Include only high-signal hints. Avoid generic advice.

Output schema:
{
  "keyIdeas": ["short Vietnamese key idea 1", "short Vietnamese key idea 2"],
  "wordHints": ["short Vietnamese vocabulary or collocation hint"],
  "structureHints": ["short Vietnamese grammar or structure hint"]
}
""";
        }

        private static string BuildWritingGradePrompt(CardSet? set, GeminiWritingGradeScript script)
        {
            var scriptJson = JsonSerializer.Serialize(script, JsonOptions);

            return $$"""
You are a strict but helpful writing grader for a foreign-language writing exercise.
The learner saw a Vietnamese paragraph and wrote the full meaning in the target language. Grade meaning, grammar, wording, completeness, and naturalness.

Course title: {{set?.Title}}
Course language: {{set?.Language}} ({{set?.LanguageCode}})

Submission JSON:
{{scriptJson}}

Grading rules:
- Return only one valid JSON object. Do not use Markdown, comments, prose, or code fences.
- Grade against both vietnameseText and expectedText. The learner does not need to match expectedText exactly if their answer is natural and preserves the meaning.
- Grade in the exact target language specified by targetLanguageName/targetLanguageCode.
- Check grammar, word choice, word order, missing ideas, wrong meaning, extra unsupported ideas, and unnatural style.
- "score" must be an integer from 0 to 100.
- "overallFeedback" must be concise Vietnamese feedback.
- "suggestedRewrite" must be a complete, natural, level-appropriate rewrite in the target language.
- Each issue must be concrete and useful. Avoid listing tiny issues if the answer is already good.
- "wrongText" should be an exact substring from userText whenever possible so the UI can highlight it. For missing content, use a nearby phrase from userText or an empty string only if there is no suitable phrase.
- "correction" should be the short corrected phrase or missing phrase.
- "explanation" must be Vietnamese and explain why the correction is needed.
- "type" must be one of: grammar, meaning, wording, missing, style.

Output schema:
{
  "score": 82,
  "overallFeedback": "concise Vietnamese overall feedback",
  "suggestedRewrite": "complete suggested rewrite in the target language",
  "issues": [
    {
      "wrongText": "exact incorrect phrase from userText",
      "correction": "corrected phrase",
      "explanation": "Vietnamese explanation",
      "type": "grammar|meaning|wording|missing|style"
    }
  ]
}
""";
        }

        private static string BuildWritingSearchQuery(
            CardSet? set,
            IEnumerable<CardItem> vocabulary,
            string? topic,
            string? targetLanguageName)
        {
            var pieces = new List<string>();
            if (!string.IsNullOrWhiteSpace(topic))
                pieces.Add(topic.Trim());
            if (!string.IsNullOrWhiteSpace(set?.Title))
                pieces.Add(set!.Title!.Trim());
            if (!string.IsNullOrWhiteSpace(targetLanguageName))
                pieces.Add(targetLanguageName!.Trim());

            pieces.AddRange(vocabulary
                .Where(x => !string.IsNullOrWhiteSpace(x.Term))
                .Take(8)
                .Select(x => x.Term!.Trim()));

            var query = string.Join(" ", pieces.Where(x => !string.IsNullOrWhiteSpace(x)));
            return string.IsNullOrWhiteSpace(query) ? "daily conversation current topic vocabulary" : query;
        }

        private static List<CardItem> SampleVocabulary(List<CardItem> vocabulary, int maxItems)
        {
            if (vocabulary.Count <= maxItems)
                return vocabulary.OrderBy(_ => Random.Shared.Next()).ToList();

            return vocabulary
                .OrderBy(_ => Random.Shared.Next())
                .Take(maxItems)
                .ToList();
        }

        private static string NormalizeWritingDifficultyKey(string? difficulty)
        {
            var value = (difficulty ?? "").Trim().ToLowerInvariant();
            return value switch
            {
                "hard" or "kho" or "khó" => "hard",
                "advanced" or "nangcao" or "nang cao" or "nâng cao" => "advanced",
                _ => "basic"
            };
        }

        private static string WritingDifficultyLabel(string difficulty)
        {
            return difficulty switch
            {
                "hard" => "Khó",
                "advanced" => "Nâng cao",
                _ => "Cơ bản"
            };
        }

        private static string WritingDifficultyInstruction(string difficulty)
        {
            return difficulty switch
            {
                "hard" => "natural sentences with connected ideas, useful phrases, and intermediate structures",
                "advanced" => "longer connected paragraphs with richer structures while still sounding everyday",
                _ => "short sentences, familiar structures, few subordinate clauses, suitable for foundation practice"
            };
        }

        private static List<string> NormalizeHintList(IEnumerable<string>? items, int maxItems)
        {
            return (items ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Select(x => x.Length > 150 ? x.Substring(0, 147).TrimEnd() + "..." : x)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxItems)
                .ToList();
        }

        private static async Task<string> FetchWebSearchContextAsync(string query, CancellationToken cancellationToken)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(8));

                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}&count=8");
                request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124 Safari/537.36");

                using var response = await Http.SendAsync(request, cts.Token);
                if (!response.IsSuccessStatusCode)
                    return "Could not fetch web results; rely on the provided topic and vocabulary.";

                var html = await response.Content.ReadAsStringAsync(cts.Token);
                var snippets = ExtractSearchSnippets(html).Take(5).ToList();
                if (snippets.Count == 0)
                    return "Could not read web snippets; rely on the provided topic and vocabulary.";

                return string.Join("\n", snippets.Select((x, i) => $"{i + 1}. {x}"));
            }
            catch
            {
                return "Could not fetch web results; rely on the provided topic and vocabulary.";
            }
        }

        private static IEnumerable<string> ExtractSearchSnippets(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                yield break;

            var matches = Regex.Matches(
                html,
                "<li class=\"b_algo\".*?<h2.*?>(.*?)</h2>.*?(?:<p>(.*?)</p>)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var title = StripHtml(match.Groups[1].Value);
                var snippet = StripHtml(match.Groups[2].Value);
                var line = string.Join(" - ", new[] { title, snippet }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (!string.IsNullOrWhiteSpace(line))
                    yield return line;
            }
        }

        private static string StripHtml(string html)
        {
            var noTags = Regex.Replace(html ?? "", "<.*?>", " ");
            var decoded = System.Net.WebUtility.HtmlDecode(noTags);
            return Regex.Replace(decoded ?? "", "\\s+", " ").Trim();
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return "";
        }

        private static string NormalizeModel(string? model)
        {
            var value = string.IsNullOrWhiteSpace(model)
                ? SettingsService.DefaultGeminiModel
                : model.Trim();

            if (value.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
                value = value.Substring("models/".Length);

            return value;
        }

        private static string SelectImageModel(string? configuredModel)
        {
            var model = NormalizeModel(configuredModel);
            return model.Contains("image", StringComparison.OrdinalIgnoreCase)
                ? model
                : DefaultImageModel;
        }

        private static (byte[] Bytes, string MimeType) ExtractInlineImage(string responseJson)
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Gemini không trả về ảnh.");
            }

            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts) ||
                    parts.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var part in parts.EnumerateArray())
                {
                    if (!TryGetPropertyCaseInsensitive(part, "inlineData", out var inlineData) &&
                        !TryGetPropertyCaseInsensitive(part, "inline_data", out inlineData))
                    {
                        continue;
                    }

                    var mimeType = TryGetPropertyCaseInsensitive(inlineData, "mimeType", out var mimeTypeElement)
                        ? mimeTypeElement.GetString() ?? "image/png"
                        : TryGetPropertyCaseInsensitive(inlineData, "mime_type", out mimeTypeElement)
                            ? mimeTypeElement.GetString() ?? "image/png"
                            : "image/png";

                    if (!TryGetPropertyCaseInsensitive(inlineData, "data", out var dataElement))
                        continue;

                    var base64 = dataElement.GetString() ?? "";
                    if (string.IsNullOrWhiteSpace(base64))
                        continue;

                    return (Convert.FromBase64String(base64), mimeType);
                }
            }

            throw new InvalidOperationException("Gemini không trả về dữ liệu ảnh.");
        }

        private static bool TryGetPropertyCaseInsensitive(
            JsonElement element,
            string propertyName,
            out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(propertyName, out value))
            {
                return true;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        private static string ExtractText(string responseJson)
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array ||
                candidates.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("Gemini không trả về nội dung.");
            }

            var first = candidates[0];
            if (!first.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Gemini không trả về phần nội dung hợp lệ.");
            }

            var sb = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text))
                    sb.Append(text.GetString());
            }

            return sb.ToString();
        }

        private static string CleanJson(string text)
        {
            var s = (text ?? "").Trim();

            if (s.StartsWith("```", StringComparison.Ordinal))
            {
                var firstLineEnd = s.IndexOf('\n');
                if (firstLineEnd >= 0)
                    s = s.Substring(firstLineEnd + 1).Trim();

                if (s.EndsWith("```", StringComparison.Ordinal))
                    s = s.Substring(0, s.Length - 3).Trim();
            }

            return s;
        }

        private static string ExtractFirstJsonObject(string text)
        {
            var s = (text ?? "").Trim();
            var start = s.IndexOf('{');
            var end = s.LastIndexOf('}');
            if (start >= 0 && end > start)
                return s.Substring(start, end - start + 1);

            return s;
        }

        private static string ParseGeminiError(string responseText, System.Net.HttpStatusCode statusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseText);
                if (doc.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var message))
                {
                    return $"Gemini lỗi ({(int)statusCode}): {message.GetString()}";
                }
            }
            catch { }

            return $"Gemini lỗi ({(int)statusCode}).";
        }
    }
}

