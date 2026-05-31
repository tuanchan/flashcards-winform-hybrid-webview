using System;
using System.Collections.Generic;
using System.Linq;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features.Quiz
{
    public enum AnswerMode
    {
        SourceLanguage = 0, // ngôn ngữ của học phần từ Config.json
        Vietnamese = 1,
        Both = 2
    }

    public sealed class QuizConfig
    {
        public int Count { get; set; } = 20;
        public AnswerMode AnswerMode { get; set; } = AnswerMode.SourceLanguage;
        public bool EnableMultipleChoice { get; set; } = true;
        public bool EnableSentenceWriting { get; set; }
    }

    public sealed class QuizQuestion
    {
        public string SmallLabel { get; set; } = "Định nghĩa";
        public string QuestionText { get; set; } = "";
        public List<string> Choices { get; set; } = new();
        public string CorrectAnswer { get; set; } = "";
        public string CardKey { get; set; } = "";

        public int Index { get; set; }
        public int Total { get; set; }

        public bool UseChineseFontForQuestion { get; set; }
        public bool UseChineseFontForChoices { get; set; }
    }

    public static class QuizEngine
    {
        public static List<QuizQuestion> BuildQuestions(CardSet set, QuizConfig cfg)
        {
            var items = (set.Items ?? new List<CardItem>())
                .Where(i => !string.IsNullOrWhiteSpace(i.Term) && !string.IsNullOrWhiteSpace(i.Definition))
                .ToList();

            if (items.Count < 4) return new List<QuizQuestion>();

            var rnd = new Random();

            int take = Math.Min(Math.Max(1, cfg.Count), items.Count);
            var selected = items.OrderBy(_ => rnd.Next()).Take(take).ToList();

            var result = new List<QuizQuestion>(take);

            for (int i = 0; i < selected.Count; i++)
            {
                var correct = selected[i];

                string questionText;
                string correctAnswer;
                Func<CardItem, string> answerSelector;
                bool qSourceLang = false;
                bool aSourceLang = false;

                if (cfg.AnswerMode == AnswerMode.SourceLanguage)
                {
                    questionText = FormatDefinition(correct);
                    answerSelector = it => it.Term;
                    correctAnswer = correct.Term;
                    qSourceLang = false;
                    aSourceLang = true;
                }
                else if (cfg.AnswerMode == AnswerMode.Vietnamese)
                {
                    questionText = correct.Term;
                    answerSelector = it => FormatDefinition(it);
                    correctAnswer = FormatDefinition(correct);
                    qSourceLang = true;
                    aSourceLang = false;
                }
                else
                {
                    questionText = $"{correct.Term}\n{FormatDefinition(correct)}";
                    answerSelector = it => $"{it.Term}\n{FormatDefinition(it)}";
                    correctAnswer = $"{correct.Term}\n{FormatDefinition(correct)}";
                    qSourceLang = true;
                    aSourceLang = true;
                }

                var choices = Build4Choices(correct, items, answerSelector, correctAnswer, rnd);

                result.Add(new QuizQuestion
                {
                    SmallLabel = "Định nghĩa",
                    QuestionText = questionText,
                    CorrectAnswer = correctAnswer,
                    CardKey = CardSetStorage.BuildCardKey(correct),
                    Choices = choices,
                    Index = i + 1,
                    Total = selected.Count,
                    UseChineseFontForQuestion = qSourceLang,
                    UseChineseFontForChoices = aSourceLang
                });
            }

            return result;
        }

        private static List<string> Build4Choices(
            CardItem correctItem,
            List<CardItem> all,
            Func<CardItem, string> selector,
            string correctAnswer,
            Random rnd)
        {
            var pool = all
                .Where(it => !ReferenceEquals(it, correctItem))
                .Select(selector)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var choices = new List<string> { correctAnswer };

            while (choices.Count < 4)
            {
                if (pool.Count == 0) break;

                var pick = pool[rnd.Next(pool.Count)];
                pool.Remove(pick);

                if (!choices.Contains(pick, StringComparer.Ordinal))
                    choices.Add(pick);
            }

            while (choices.Count < 4)
            {
                var pick = selector(all[rnd.Next(all.Count)]);
                if (string.IsNullOrWhiteSpace(pick)) continue;
                if (!choices.Contains(pick, StringComparer.Ordinal))
                    choices.Add(pick);
            }

            return choices.OrderBy(_ => rnd.Next()).ToList();
        }

        private static string FormatDefinition(CardItem item)
        {
            var def = (item.Definition ?? "").Trim();
            return CardImportParser.SplitTailPronunciation(def).Definition;
        }
    }
}
