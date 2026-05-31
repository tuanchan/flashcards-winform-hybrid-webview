using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public static class GeminiExampleStore
    {
        private const string FileName = "GeminiExamplesCache.txt";
        private const string LegacyFileName = "GeminiExamples.js";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private sealed class ExampleFile
        {
            public Dictionary<string, GeminiExamplesPayload> Items { get; set; } = new(StringComparer.Ordinal);
        }

        public static GeminiExamplesPayload? TryGet(CardSet? set, CardItem? item)
        {
            if (set == null || item == null) return null;

            var file = Load(set);
            var key = BuildKey(item);
            return file.Items.TryGetValue(key, out var value) ? value : null;
        }

        public static void Save(CardSet? set, CardItem? item, GeminiExamplesPayload payload)
        {
            if (set == null || item == null) return;

            var path = ResolvePath(set);
            if (string.IsNullOrWhiteSpace(path)) return;

            var file = Load(set);
            file.Items[BuildKey(item)] = payload;

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(file, JsonOptions);
            File.WriteAllText(path, json, CardSetStorage.Utf8NoBomEncoding);
        }

        private static ExampleFile Load(CardSet set)
        {
            try
            {
                var path = ResolvePath(set);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    path = ResolvePath(set, LegacyFileName);

                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return new ExampleFile();

                var text = File.ReadAllText(path, CardSetStorage.Utf8NoBomEncoding).Trim();
                var start = text.IndexOf('{');
                var end = text.LastIndexOf('}');
                if (start < 0 || end <= start)
                    return new ExampleFile();

                var json = text.Substring(start, end - start + 1);
                var parsed = JsonSerializer.Deserialize<ExampleFile>(json, JsonOptions);
                parsed ??= new ExampleFile();
                parsed.Items ??= new Dictionary<string, GeminiExamplesPayload>(StringComparer.Ordinal);
                return parsed;
            }
            catch
            {
                return new ExampleFile();
            }
        }

        private static string ResolvePath(CardSet set, string fileName = FileName)
        {
            var baseFolder = set.BaseFolder;

            if (string.IsNullOrWhiteSpace(baseFolder) && !string.IsNullOrWhiteSpace(set.ConfigFilePath))
                baseFolder = Path.GetDirectoryName(set.ConfigFilePath);

            if (string.IsNullOrWhiteSpace(baseFolder) && !string.IsNullOrWhiteSpace(set.VocabsFilePath))
                baseFolder = Directory.GetParent(Path.GetDirectoryName(set.VocabsFilePath) ?? "")?.FullName;

            if (string.IsNullOrWhiteSpace(baseFolder))
                return "";

            return Path.Combine(baseFolder, fileName);
        }

        private static string BuildKey(CardItem item)
        {
            var raw = CardSetStorage.BuildCardKey(item);
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
