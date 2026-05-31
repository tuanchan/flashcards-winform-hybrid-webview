using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public static class GeminiSentenceStore
    {
        private const string FileName = "GeminiSentenceCache.txt";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private sealed class SentenceFile
        {
            public Dictionary<string, GeminiSentenceQuizPayload> Items { get; set; } = new(StringComparer.Ordinal);
        }

        public static GeminiSentenceQuizPayload? TryGet(CardSet? set, int count)
        {
            if (set == null || count <= 0) return null;

            var file = Load(set);
            return file.Items.TryGetValue(BuildKey(count), out var payload) ? payload : null;
        }

        public static void Save(CardSet? set, int count, GeminiSentenceQuizPayload payload)
        {
            if (set == null || count <= 0 || payload == null) return;

            var path = ResolvePath(set);
            if (string.IsNullOrWhiteSpace(path)) return;

            var file = Load(set);
            file.Items[BuildKey(count)] = payload;

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(file, JsonOptions);
            File.WriteAllText(path, json, CardSetStorage.Utf8NoBomEncoding);
        }

        private static SentenceFile Load(CardSet set)
        {
            try
            {
                var path = ResolvePath(set);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return new SentenceFile();

                var text = File.ReadAllText(path, CardSetStorage.Utf8NoBomEncoding).Trim();
                if (string.IsNullOrWhiteSpace(text))
                    return new SentenceFile();

                var parsed = JsonSerializer.Deserialize<SentenceFile>(text, JsonOptions);
                parsed ??= new SentenceFile();
                parsed.Items ??= new Dictionary<string, GeminiSentenceQuizPayload>(StringComparer.Ordinal);
                return parsed;
            }
            catch
            {
                return new SentenceFile();
            }
        }

        private static string ResolvePath(CardSet set)
        {
            var baseFolder = set.BaseFolder;

            if (string.IsNullOrWhiteSpace(baseFolder) && !string.IsNullOrWhiteSpace(set.ConfigFilePath))
                baseFolder = Path.GetDirectoryName(set.ConfigFilePath);

            if (string.IsNullOrWhiteSpace(baseFolder) && !string.IsNullOrWhiteSpace(set.VocabsFilePath))
                baseFolder = Directory.GetParent(Path.GetDirectoryName(set.VocabsFilePath) ?? "")?.FullName;

            if (string.IsNullOrWhiteSpace(baseFolder))
                return "";

            return Path.Combine(baseFolder, FileName);
        }

        private static string BuildKey(int count) => $"meaning-answer-v3:count:{count}";
    }
}
