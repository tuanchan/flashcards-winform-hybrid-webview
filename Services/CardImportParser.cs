using System;
using System.Collections.Generic;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public static class CardImportParser
    {
        public static List<CardItem> Parse(string raw, string termDefSep, string cardSep)
        {
            raw ??= "";
            raw = NormalizeNewlines(raw).Trim();

            var results = new List<CardItem>();
            if (string.IsNullOrWhiteSpace(raw)) return results;

            // split cards
            var cardChunks = SplitBySeparator(raw, cardSep);

            foreach (var chunk in cardChunks)
            {
                var line = chunk.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Split term/definition at FIRST occurrence
                var pair = SplitFirst(line, termDefSep);
                if (pair == null) continue;

                var term = pair.Value.left.Trim();
                var defRaw = pair.Value.right.Trim();

                if (string.IsNullOrWhiteSpace(term) || string.IsNullOrWhiteSpace(defRaw))
                    continue;

                var (def, pronunciation) = SplitTailPronunciation(defRaw);

                results.Add(new CardItem
                {
                    Term = term,
                    Definition = def,
                    Pinyin = string.IsNullOrWhiteSpace(pronunciation) ? null : pronunciation
                });
            }

            return results;
        }

        public static string NormalizeNewlines(string s)
            => s.Replace("\r\n", "\n").Replace("\r", "\n");

        public static (string Definition, string? Pronunciation) SplitTailPronunciation(string? definitionRaw)
        {
            var text = (definitionRaw ?? "").Trim();
            if (text.Length == 0 || !IsCloseParen(text[^1]))
                return (text, null);

            var openIndex = FindTailOpenParen(text);
            if (openIndex <= 0)
                return (text, null);

            var definition = text[..openIndex].Trim();
            var pronunciation = text[(openIndex + 1)..^1].Trim();

            return string.IsNullOrWhiteSpace(pronunciation)
                ? (text, null)
                : (definition, pronunciation);
        }

        // cardSep supports: "\n", ";", or any custom string
        private static List<string> SplitBySeparator(string text, string sep)
        {
            if (string.IsNullOrEmpty(sep)) return new List<string> { text };

            // If separator is newline, split on '\n' but keep empty lines out later
            if (sep == "\n")
            {
                return new List<string>(text.Split('\n', StringSplitOptions.None));
            }

            return new List<string>(text.Split(sep, StringSplitOptions.None));
        }

        private static (string left, string right)? SplitFirst(string text, string sep)
        {
            if (string.IsNullOrEmpty(sep)) return null;

            var idx = text.IndexOf(sep, StringComparison.Ordinal);
            if (idx < 0) return null;

            var left = text.Substring(0, idx);
            var right = text.Substring(idx + sep.Length);
            return (left, right);
        }

        private static int FindTailOpenParen(string text)
        {
            var depth = 0;

            for (var i = text.Length - 1; i >= 0; i--)
            {
                var ch = text[i];

                if (IsCloseParen(ch))
                {
                    depth++;
                    continue;
                }

                if (!IsOpenParen(ch))
                    continue;

                depth--;
                if (depth == 0)
                    return i;

                if (depth < 0)
                    return -1;
            }

            return -1;
        }

        private static bool IsOpenParen(char ch)
            => ch == '(' || ch == '（';

        private static bool IsCloseParen(char ch)
            => ch == ')' || ch == '）';
    }
}
