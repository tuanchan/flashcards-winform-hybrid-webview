#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace TocflQuiz.Controls.Features.Quiz
{
    public sealed partial class QuizEssayControlWeb
    {
        private static string StripDefinitionForAnswer(string? definition, string? pinyin)
        {
            var def = (definition ?? "").Trim();
            if (string.IsNullOrWhiteSpace(def)) return "";

            def = StripBracketTail(def);
            def = StripParenTail(def);

            return def.Trim();
        }

        private static string StripBracketTail(string s)
        {
            s = (s ?? "").Trim();

            int close = s.LastIndexOf(']');
            int open = s.LastIndexOf('[');
            if (open >= 0 && close == s.Length - 1 && open < close)
            {
                var head = s.Substring(0, open).Trim();
                if (head.Length > 0) return head;
            }

            return s;
        }

        private static string StripParenTail(string s)
        {
            int close = s.LastIndexOf(')');
            int open = s.LastIndexOf('(');
            if (open >= 0 && close == s.Length - 1 && open < close)
            {
                var head = s.Substring(0, open).Trim();
                if (head.Length > 0) return head;
            }

            return s.Trim();
        }

        private static bool IsAnswerCorrect(QuizQuestion q, string user)
        {
            bool answerIsChinese = q.UseChineseFontForChoices;

            string userN = Normalize(user, answerIsChinese);
            if (string.IsNullOrWhiteSpace(userN)) return false;

            string correctRaw = (q.CorrectAnswer ?? "").Trim();

            if (!answerIsChinese)
                correctRaw = StripParenTail(correctRaw);

            IEnumerable<string> candidates = SplitCandidates(correctRaw);
            if (!candidates.Any()) candidates = new[] { correctRaw };

            foreach (var cand in candidates)
            {
                string correctN = Normalize(cand, answerIsChinese);

                if (answerIsChinese)
                {
                    if (string.Equals(userN, correctN, StringComparison.Ordinal))
                        return true;
                }
                else
                {
                    var a = RemoveDiacritics(userN).ToLowerInvariant();
                    var b = RemoveDiacritics(correctN).ToLowerInvariant();
                    if (a == b) return true;
                }
            }

            return false;

            static IEnumerable<string> SplitCandidates(string s)
                => (s ?? "")
                    .Split(new[] { '/', ';', '|', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0);
        }
    }
}
