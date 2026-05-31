#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class FlashcardsFeatureControlWeb
    {
        private void CheckCompletion()
        {
            if (_order.Count == 0) return;
            if (_completionShown) return;

            if (_progressTracking)
            {
                if (_inLearningReview)
                {
                    if (_learningReviewTouched.Count < _order.Count) return;
                    if (_index < _order.Count - 1) return;

                    var cur = _order[_index];
                    if (!_learningReviewTouched.Contains(cur)) return;

                    FlushPendingKnownToNotYet();
                    _completionShown = true;
                    _ = PushStateAsync();
                    return;
                }

                int answered = _order.Count(i =>
                    _progressMap.TryGetValue(i, out var s) && s != CardProgressState.None);

                if (answered < _order.Count) return;

                FlushPendingKnownToNotYet();
                _completionShown = true;
                _ = PushStateAsync();
                return;
            }

            if (_visitedNonProgress.Count < _order.Count) return;

            _completionShown = true;
            _ = PushStateAsync();
        }

        private void HideCompletionOverlay()
        {
            _completionShown = false;

            if (_inLearningReview)
            {
                RestoreFullOrderIfNeeded();
                return;
            }

            if (_sessionNotYetFlushed && _sourceSet != null)
            {
                _sourceSet.Items = Services.CardSetStorage.LoadVocabularyItems(_sourceSet);

                if (_set != null)
                    _set.Items = Services.CardSetStorage.LoadStudyItems(_sourceSet);

                _progressMap.Clear();
                _undoStack.Clear();
                _learningReviewTouched.Clear();
                _sessionPendingKnownKeys.Clear();
                _sessionNotYetFlushed = false;

                _index = 0;
                _lastCardIndex = -1;
                _seenLastCardInNonProgress = false;
                _visitedNonProgress.Clear();
                _lastVisitedItemIndex = -1;

                RebuildOrder(false);
                return;
            }

            _ = PushStateAsync();
        }

        private void ReviewLearning()
        {
            if (_set?.Items == null) return;

            _completionShown = false;

            var learning = _progressMap
                .Where(p => p.Value == CardProgressState.Learning)
                .Select(p => p.Key)
                .Where(i => i >= 0 && i < _set.Items.Count)
                .ToList();

            if (learning.Count == 0)
            {
                _ = PushStateAsync();
                return;
            }

            if (!_inLearningReview)
            {
                _orderBeforeReview = new List<int>(_order);
                _indexBeforeReview = _index;
            }

            _inLearningReview = true;
            _order = learning;
            _index = 0;
            _lastCardIndex = -1;
            _learningReviewTouched.Clear();

            ShowCard();
        }

        private void RestoreFullOrderIfNeeded()
        {
            if (!_inLearningReview) return;

            if (_orderBeforeReview == null || _orderBeforeReview.Count == 0)
            {
                _inLearningReview = false;
                RebuildOrder(false);
                return;
            }

            _order = new List<int>(_orderBeforeReview);
            _index = Math.Max(0, Math.Min(_indexBeforeReview, _order.Count - 1));
            _lastCardIndex = -1;
            _completionShown = false;
            _orderBeforeReview.Clear();
            _inLearningReview = false;

            ShowCard();
        }

        private object GetCompletionData()
        {
            if (!_completionShown) return new { };

            int total;
            int known;
            int learning;
            int remaining;

            if (_inLearningReview)
            {
                total = _order.Count;
                var touched = _learningReviewTouched;

                known = touched.Count(i => _progressMap.TryGetValue(i, out var s) && s == CardProgressState.Known);
                learning = touched.Count(i => _progressMap.TryGetValue(i, out var s) && s == CardProgressState.Learning);
                remaining = Math.Max(0, total - touched.Count);
            }
            else
            {
                total = _set?.Items?.Count ?? 0;
                known = _sessionPendingKnownKeys.Count;
                learning = _progressMap.Count(p => p.Value == CardProgressState.Learning);
                remaining = Math.Max(0, total - known);
            }

            return new
            {
                title = _set?.Title ?? "",
                isReview = _inLearningReview,
                total,
                known,
                learning,
                remaining,
                canReviewLearning = _progressMap.Any(p => p.Value == CardProgressState.Learning)
            };
        }

        private void FlushPendingKnownToNotYet()
        {
            if (_sessionNotYetFlushed) return;
            if (_sourceSet == null) return;
            if (string.IsNullOrWhiteSpace(_sourceSet.NotYetFilePath)) return;

            var currentNotYet = Services.CardSetStorage.LoadCardsFromFile(_sourceSet.NotYetFilePath);
            if (currentNotYet.Count == 0 && _set?.Items != null && _set.Items.Count > 0)
                currentNotYet = _set.Items.Select(CloneCard).ToList();

            var remaining = currentNotYet
                .Where(item => !_sessionPendingKnownKeys.Contains(Services.CardSetStorage.BuildCardKey(item)))
                .ToList();

            Services.CardSetStorage.WriteCardsToFile(_sourceSet.NotYetFilePath, remaining);
            _sessionNotYetFlushed = true;
        }

        private void ApplyLegacyStarred()
        {
            if (_set?.Items == null || _set.Items.Count == 0) return;

            var path = LegacyStarFilePath();
            if (!File.Exists(path)) return;

            try
            {
                var json = File.ReadAllText(path);
                var arr = JsonSerializer.Deserialize<int[]>(json) ?? Array.Empty<int>();

                foreach (var idx in arr.Where(i => i >= 0 && i < _set.Items.Count))
                {
                    _set.Items[idx].IsStarred = true;
                }

                Services.CardSetStorage.SaveSetJson(_set);
            }
            catch { }
        }

        private string GetSetDir()
        {
            var id = _sourceSet?.Id ?? _set?.Id;
            if (string.IsNullOrWhiteSpace(id)) id = "unknown_set";

            var safe = MakeSafeFileName(id);
            return Path.Combine(Services.CardSetStorage.BaseDir, safe);
        }

        private string LegacyStarFilePath() => Path.Combine(GetSetDir(), "starred.json");

        private static string MakeSafeFileName(string s)
        {
            s ??= "";

            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');

            return s.Trim();
        }

        private static CardItem CloneCard(CardItem? item)
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
    }
}
