using System;
using System.Collections.Generic;
using System.IO;

namespace TocflQuiz.Services
{
    internal sealed class CardSetNameNormalizer
    {
        public string EnsureUniqueTitle(string? title, HashSet<string> existingTitles)
        {
            var baseTitle = NormalizeDisplayTitle(title);
            var candidate = baseTitle;
            var suffix = 2;

            while (existingTitles.Contains(candidate))
            {
                candidate = $"{baseTitle}({suffix})";
                suffix++;
            }

            return candidate;
        }

        public string NormalizeDisplayTitle(string? title)
        {
            var normalized = (title ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return "Untitled";

            return normalized;
        }

        public string NormalizeLanguageCode(string? languageCode)
            => (languageCode ?? "").Trim();

        public string MakeSafeFolderSegment(string? value)
        {
            var safe = MakeSafeFileName(value ?? "");
            safe = safe.Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(safe) ? "Course" : safe;
        }

        public string MakeSafeFileName(string s)
        {
            s ??= "";

            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');

            return s.Trim();
        }
    }
}
