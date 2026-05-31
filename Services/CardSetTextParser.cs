using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    internal sealed class CardSetTextParser
    {
        public List<CardItem> LoadCardsFromFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new List<CardItem>();

            var text = File.ReadAllText(path, CardSetStorage.Utf8NoBomEncoding);
            if (string.IsNullOrWhiteSpace(text))
                return new List<CardItem>();

            var trimmed = text.TrimStart();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) ||
                trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                var jsonItems = TryLoadJsonCards(text);
                if (jsonItems.Count > 0)
                    return jsonItems;
            }

            return LoadCardsFromTabularText(text);
        }

        public void WriteCardsToFile(string? path, IEnumerable<CardItem>? items)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var normalized = (items ?? Enumerable.Empty<CardItem>())
                .Select(CloneCard)
                .ToList();

            var json = JsonSerializer.Serialize(normalized, CardSetStorage.JsonOptionsValue);
            File.WriteAllText(path, json, CardSetStorage.Utf8NoBomEncoding);
        }

        public string BuildCardKey(CardItem? item)
        {
            if (item == null)
                return "\t\t";

            return string.Join("\t",
                NormalizeKeyPart(item.Term),
                NormalizeKeyPart(item.Definition),
                NormalizeKeyPart(item.Pinyin));
        }

        public CardItem CloneCard(CardItem? item)
        {
            return new CardItem
            {
                Term = item?.Term ?? "",
                Definition = item?.Definition ?? "",
                Pinyin = item?.Pinyin,
                IsStarred = item?.IsStarred ?? false,
                SrsLevel = item?.SrsLevel ?? 0,
                SrsDueDate = item?.SrsDueDate,
                SrsLastReviewedAt = item?.SrsLastReviewedAt,
                SrsReviewCount = item?.SrsReviewCount ?? 0,
                SrsLapseCount = item?.SrsLapseCount ?? 0
            };
        }

        public List<CardItem> CloneCards(IEnumerable<CardItem>? items)
        {
            return (items ?? Enumerable.Empty<CardItem>())
                .Select(CloneCard)
                .ToList();
        }

        private List<CardItem> TryLoadJsonCards(string text)
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<CardItem>>(text, CardSetStorage.JsonOptionsValue);
                if (items != null)
                    return CloneCards(items);
            }
            catch
            {
                // ignore and fallback
            }

            try
            {
                var set = JsonSerializer.Deserialize<CardSet>(text, CardSetStorage.JsonOptionsValue);
                if (set?.Items != null)
                    return CloneCards(set.Items);
            }
            catch
            {
                // ignore and fallback
            }

            return new List<CardItem>();
        }

        private List<CardItem> LoadCardsFromTabularText(string text)
        {
            var results = new List<CardItem>();
            var normalized = CardImportParser.NormalizeNewlines(text);
            var lines = normalized.Split('\n', StringSplitOptions.None);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = rawLine.Split('\t');
                if (parts.Length >= 2)
                {
                    var term = parts[0].Trim();
                    if (string.IsNullOrWhiteSpace(term))
                        continue;

                    string definition;
                    string? pinyin = null;
                    var isStarred = false;

                    if (parts.Length >= 4)
                    {
                        definition = string.Join("\t", parts.Skip(1).Take(parts.Length - 3)).Trim();
                        pinyin = parts[^2].Trim();
                        isStarred = ParseStar(parts[^1]);
                    }
                    else if (parts.Length == 3 && LooksLikeStarValue(parts[2]))
                    {
                        definition = parts[1].Trim();
                        isStarred = ParseStar(parts[2]);
                    }
                    else if (parts.Length == 3)
                    {
                        definition = parts[1].Trim();
                        pinyin = parts[2].Trim();
                    }
                    else
                    {
                        var parsed = CardImportParser.Parse(rawLine, "\t", "\n").FirstOrDefault();
                        if (parsed == null)
                            continue;

                        results.Add(parsed);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(pinyin))
                    {
                        var split = CardImportParser.SplitTailPronunciation(definition);
                        definition = split.Definition;
                        pinyin = split.Pronunciation;
                    }

                    if (string.IsNullOrWhiteSpace(definition))
                        continue;

                    results.Add(new CardItem
                    {
                        Term = term,
                        Definition = definition,
                        Pinyin = string.IsNullOrWhiteSpace(pinyin) ? null : pinyin,
                        IsStarred = isStarred
                    });

                    continue;
                }

                var fallback = CardImportParser.Parse(rawLine, "\t", "\n").FirstOrDefault();
                if (fallback != null)
                    results.Add(fallback);
            }

            return results;
        }

        private bool LooksLikeStarValue(string value)
        {
            var normalized = (value ?? "").Trim();
            return normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("0", StringComparison.OrdinalIgnoreCase);
        }

        private bool ParseStar(string? value)
        {
            var normalized = (value ?? "").Trim();
            return normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private string NormalizeKeyPart(string? value)
        {
            var normalized = CardImportParser.NormalizeNewlines(value ?? "").Trim();
            return normalized;
        }
    }
}
