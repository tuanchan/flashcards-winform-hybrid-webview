#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TocflQuiz.Controls.Features.Quiz
{
    public sealed partial class QuizEssayControlWeb
    {
        private static string Normalize(string s, bool isChinese)
        {
            s = (s ?? "").Trim();
            s = string.Join(" ", s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            if (isChinese) s = s.Replace(" ", "");
            return s;
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string FormatTime(System.TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";

            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        private static string? PickInstalledFont(IEnumerable<string> candidates)
        {
            try
            {
                using var fonts = new System.Drawing.Text.InstalledFontCollection();
                var installed = fonts.Families.Select(f => f.Name).ToHashSet(System.StringComparer.OrdinalIgnoreCase);

                foreach (var c in candidates)
                {
                    if (installed.Contains(c)) return c;
                }
            }
            catch { }

            return null;
        }

        private static JsonSerializerOptions JsonOpt()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }
    }
}
