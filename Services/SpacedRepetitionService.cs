using System;
using System.Globalization;
using System.IO;
using System.Linq;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public static class SpacedRepetitionService
    {
        private static readonly double[] LevelDays = { 0.2, 1, 3, 7, 14, 30, 90, 180 };

        public static bool ApplyReview(CardSet? set, string? cardKey, bool correct, DateTime? now = null)
            => ApplyReview(set, cardKey, correct ? FlashcardReviewResult.Good : FlashcardReviewResult.Again, now);

        public static bool ApplyReview(CardSet? set, string? cardKey, FlashcardReviewResult result, DateTime? now = null)
        {
            if (set == null || string.IsNullOrWhiteSpace(cardKey))
                return false;

            set.Items = CardSetStorage.LoadVocabularyItems(set);
            var item = set.Items.FirstOrDefault(x => CardSetStorage.BuildCardKey(x) == cardKey);
            if (item == null)
                return false;

            var changed = ApplyReview(item, result, now);
            if (!changed)
                return false;

            SaveVocabularyAndStudyCopies(set, item, cardKey);
            return true;
        }

        public static bool ApplyReview(CardItem? item, bool correct, DateTime? now = null)
            => ApplyReview(item, correct ? FlashcardReviewResult.Good : FlashcardReviewResult.Again, now);

        public static bool ApplyReview(CardItem? item, FlashcardReviewResult result, DateTime? now = null)
        {
            if (item == null)
                return false;

            var reviewedAt = now ?? DateTime.Now;
            if (!IsDue(item, reviewedAt))
                return false;

            item.SrsLastReviewedAt = reviewedAt.ToString("O", CultureInfo.InvariantCulture);
            item.SrsReviewCount = Math.Max(0, item.SrsReviewCount) + 1;

            var currentLevel = Math.Max(0, Math.Min(item.SrsLevel, LevelDays.Length - 1));
            int nextLevel;

            if (result == FlashcardReviewResult.Again)
            {
                nextLevel = 0;
                item.SrsLapseCount = Math.Max(0, item.SrsLapseCount) + 1;
            }
            else
            {
                nextLevel = result == FlashcardReviewResult.Hard
                    ? currentLevel
                    : Math.Min(LevelDays.Length - 1, currentLevel + 1);
            }

            item.SrsLevel = nextLevel;

            var modifier = result == FlashcardReviewResult.Hard
                ? 0.8
                : result == FlashcardReviewResult.Easy ? 1.5 : 1.0;
            var dueAfterDays = result == FlashcardReviewResult.Again
                ? 0
                : Math.Max(0.1, LevelDays[nextLevel] * modifier);

            item.SrsDueDate = reviewedAt
                .AddDays(dueAfterDays)
                .ToString("O", CultureInfo.InvariantCulture);

            return true;
        }

        public static object BuildStatus(CardItem? item, DateTime? now = null)
        {
            var current = now ?? DateTime.Now;
            var due = ParseDate(item?.SrsDueDate) ?? current;
            var isDue = due <= current;
            var level = Math.Max(0, item?.SrsLevel ?? 0);
            var text = isDue
                ? $"SRS Lv {level} - due"
                : due.Date == current.Date
                    ? $"SRS Lv {level} - {due:HH:mm}"
                    : $"SRS Lv {level} - {due:dd/MM}";

            return new
            {
                level,
                dueDate = due.ToString("O", CultureInfo.InvariantCulture),
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
            var current = now ?? DateTime.Now;

            return items.Count(item => IsDue(item, current));
        }

        public static void CopySrs(CardItem source, CardItem target)
        {
            target.SrsLevel = source.SrsLevel;
            target.SrsDueDate = source.SrsDueDate;
            target.SrsLastReviewedAt = source.SrsLastReviewedAt;
            target.SrsReviewCount = source.SrsReviewCount;
            target.SrsLapseCount = source.SrsLapseCount;
        }

        private static bool IsDue(CardItem item, DateTime now)
        {
            var due = ParseDate(item.SrsDueDate);
            return due == null || due.Value <= now;
        }

        private static DateTime? ParseDate(string? value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
                return parsed;

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
    }
}
