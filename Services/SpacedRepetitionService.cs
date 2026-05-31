using System;
using System.Globalization;
using System.IO;
using System.Linq;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public static class SpacedRepetitionService
    {
        private static readonly int[] Intervals = { 0, 1, 3, 7, 14, 30, 60, 120, 240 };

        public static bool ApplyReview(CardSet? set, string? cardKey, bool correct, DateTime? now = null)
        {
            if (set == null || string.IsNullOrWhiteSpace(cardKey))
                return false;

            set.Items = CardSetStorage.LoadVocabularyItems(set);
            var item = set.Items.FirstOrDefault(x => CardSetStorage.BuildCardKey(x) == cardKey);
            if (item == null)
                return false;

            var changed = ApplyReview(item, correct, now);
            if (!changed)
                return false;

            SaveVocabularyAndStudyCopies(set, item, cardKey);
            return true;
        }

        public static bool ApplyReview(CardItem? item, bool correct, DateTime? now = null)
        {
            if (item == null)
                return false;

            var today = (now ?? DateTime.Now).Date;
            if (!IsDue(item, today))
                return false;

            item.SrsLastReviewedAt = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            item.SrsReviewCount = Math.Max(0, item.SrsReviewCount) + 1;

            if (correct)
            {
                item.SrsLevel = Math.Min(Intervals.Length - 1, Math.Max(0, item.SrsLevel) + 1);
            }
            else
            {
                item.SrsLevel = 0;
                item.SrsLapseCount = Math.Max(0, item.SrsLapseCount) + 1;
            }

            item.SrsDueDate = today
                .AddDays(Intervals[Math.Max(0, Math.Min(item.SrsLevel, Intervals.Length - 1))])
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            return true;
        }

        public static object BuildStatus(CardItem? item, DateTime? now = null)
        {
            var today = (now ?? DateTime.Now).Date;
            var due = ParseDate(item?.SrsDueDate) ?? today;
            var isDue = due <= today;
            var level = Math.Max(0, item?.SrsLevel ?? 0);
            var text = isDue
                ? $"SRS Lv {level} - đến hạn"
                : $"SRS Lv {level} - ôn {due:dd/MM}";

            return new
            {
                level,
                dueDate = due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                isDue,
                text,
                reviewCount = Math.Max(0, item?.SrsReviewCount ?? 0),
                lapseCount = Math.Max(0, item?.SrsLapseCount ?? 0)
            };
        }

        public static int CountDue(CardSet? set, DateTime? now = null)
        {
            if (set == null)
                return 0;

            var items = set.Items != null && set.Items.Count > 0
                ? set.Items
                : CardSetStorage.LoadVocabularyItems(set);
            var today = (now ?? DateTime.Now).Date;

            return items.Count(item => IsDue(item, today));
        }

        private static bool IsDue(CardItem item, DateTime today)
        {
            var due = ParseDate(item.SrsDueDate);
            return due == null || due.Value.Date <= today;
        }

        private static DateTime? ParseDate(string? value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
                return parsed.Date;

            return null;
        }

        private static void SaveVocabularyAndStudyCopies(CardSet set, CardItem reviewed, string originalKey)
        {
            if (!string.IsNullOrWhiteSpace(set.VocabsFilePath))
                CardSetStorage.WriteCardsToFile(set.VocabsFilePath, set.Items);

            if (!string.IsNullOrWhiteSpace(set.ConfigFilePath))
                CardSetStorage.SaveSetJson(set);

            if (string.IsNullOrWhiteSpace(set.NotYetFilePath) || !File.Exists(set.NotYetFilePath))
                return;

            var studyItems = CardSetStorage.LoadCardsFromFile(set.NotYetFilePath);
            var studyMatch = studyItems.FirstOrDefault(x => CardSetStorage.BuildCardKey(x) == originalKey);
            if (studyMatch == null)
                return;

            CopySrs(reviewed, studyMatch);
            CardSetStorage.WriteCardsToFile(set.NotYetFilePath, studyItems);
        }

        private static void CopySrs(CardItem source, CardItem target)
        {
            target.SrsLevel = source.SrsLevel;
            target.SrsDueDate = source.SrsDueDate;
            target.SrsLastReviewedAt = source.SrsLastReviewedAt;
            target.SrsReviewCount = source.SrsReviewCount;
            target.SrsLapseCount = source.SrsLapseCount;
        }
    }
}
