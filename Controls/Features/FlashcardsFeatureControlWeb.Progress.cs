#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class FlashcardsFeatureControlWeb
    {
        private void ToggleShuffle()
        {
            _shuffleEnabled = true;
            BuildShuffleOrder();
            _index = 0;
            _lastCardIndex = -1;
            _completionShown = false;
            _seenLastCardInNonProgress = false;
            RebuildOrder(false);
        }

        private void SetProgressTracking(bool enabled)
        {
            if (_progressTracking == enabled) return;

            _progressTracking = enabled;
            _completionShown = false;
            _seenLastCardInNonProgress = false;
            ShowCard();
        }

        private void MarkProgress(CardProgressState state)
        {
            if (_set?.Items == null || _order.Count == 0) return;

            var itemIndex = _order[_index];
            var prevState = _progressMap.TryGetValue(itemIndex, out var existing)
                ? existing
                : CardProgressState.None;

            _progressMap[itemIndex] = state;

            var item = _set.Items[itemIndex];
            var key = Services.CardSetStorage.BuildCardKey(item);
            var previousSrs = CloneSrs(item);
            var reviewResult = state == CardProgressState.Known
                ? Services.FlashcardReviewResult.Good
                : Services.FlashcardReviewResult.Again;
            Services.SpacedRepetitionService.ApplyReview(item, reviewResult);
            Services.SpacedRepetitionService.ApplyReview(_sourceSet, key, reviewResult);

            if (state == CardProgressState.Known)
                _sessionPendingKnownKeys.Add(key);
            else
                _sessionPendingKnownKeys.Remove(key);

            if (_inLearningReview)
                _learningReviewTouched.Add(itemIndex);

            _undoStack.Push(new ProgressAction
            {
                CardIndex = itemIndex,
                PreviousState = prevState,
                PreviousSrs = previousSrs
            });

            bool isLastCard = _index >= _order.Count - 1;
            if (!isLastCard)
            {
                _index++;
                ShowCard();
                return;
            }

            _lastCardIndex = -1;
            CheckCompletion();
            _ = PushStateAsync();
        }

        private void UndoProgress()
        {
            if (_undoStack.Count == 0) return;
            if (_set?.Items == null) return;

            var action = _undoStack.Pop();

            if (action.PreviousState == CardProgressState.None)
                _progressMap.Remove(action.CardIndex);
            else
                _progressMap[action.CardIndex] = action.PreviousState;

            if (action.CardIndex >= 0 && action.CardIndex < _set.Items.Count)
            {
                var item = _set.Items[action.CardIndex];
                var key = Services.CardSetStorage.BuildCardKey(item);

                if (action.PreviousState == CardProgressState.Known)
                    _sessionPendingKnownKeys.Add(key);
                else
                    _sessionPendingKnownKeys.Remove(key);

                if (action.PreviousSrs != null)
                {
                    Services.SpacedRepetitionService.CopySrs(action.PreviousSrs, item);
                    RestoreSourceSrs(key, action.PreviousSrs);
                }
            }

            var newPos = _order.IndexOf(action.CardIndex);
            if (newPos >= 0)
                _index = newPos;

            ShowCard();
        }

        private static Models.CardItem CloneSrs(Models.CardItem item)
        {
            return new Models.CardItem
            {
                SrsLevel = item.SrsLevel,
                SrsDueDate = item.SrsDueDate,
                SrsLastReviewedAt = item.SrsLastReviewedAt,
                SrsReviewCount = item.SrsReviewCount,
                SrsLapseCount = item.SrsLapseCount
            };
        }

        private void RestoreSourceSrs(string key, Models.CardItem snapshot)
        {
            if (_sourceSet == null) return;

            _sourceSet.Items = Services.CardSetStorage.LoadVocabularyItems(_sourceSet);
            var sourceItem = _sourceSet.Items.FirstOrDefault(x => Services.CardSetStorage.BuildCardKey(x) == key);
            if (sourceItem == null) return;

            Services.SpacedRepetitionService.CopySrs(snapshot, sourceItem);

            if (!string.IsNullOrWhiteSpace(_sourceSet.VocabsFilePath))
                Services.CardSetStorage.WriteCardsToFile(_sourceSet.VocabsFilePath, _sourceSet.Items);

            if (!string.IsNullOrWhiteSpace(_sourceSet.ConfigFilePath))
                Services.CardSetStorage.SaveSetJson(_sourceSet);

            if (string.IsNullOrWhiteSpace(_sourceSet.NotYetFilePath) || !System.IO.File.Exists(_sourceSet.NotYetFilePath))
                return;

            var studyItems = Services.CardSetStorage.LoadCardsFromFile(_sourceSet.NotYetFilePath);
            var studyItem = studyItems.FirstOrDefault(x => Services.CardSetStorage.BuildCardKey(x) == key);
            if (studyItem == null) return;

            Services.SpacedRepetitionService.CopySrs(snapshot, studyItem);
            Services.CardSetStorage.WriteCardsToFile(_sourceSet.NotYetFilePath, studyItems);
        }

        private void ResetProgress()
        {
            if (_sourceSet != null)
            {
                Services.CardSetStorage.ResetNotYet(_sourceSet);
                _sourceSet.Items = Services.CardSetStorage.LoadVocabularyItems(_sourceSet);
            }

            if (_set != null)
            {
                _set.Items = _sourceSet != null
                    ? Services.CardSetStorage.LoadStudyItems(_sourceSet)
                    : new List<Models.CardItem>();
            }

            _progressMap.Clear();
            _undoStack.Clear();
            _learningReviewTouched.Clear();
            _sessionPendingKnownKeys.Clear();
            _sessionNotYetFlushed = false;

            if (_inLearningReview)
            {
                _inLearningReview = false;
                _orderBeforeReview.Clear();
                _indexBeforeReview = 0;
            }

            _index = 0;
            _lastCardIndex = -1;
            _completionShown = false;
            _seenLastCardInNonProgress = false;
            _visitedNonProgress.Clear();
            _lastVisitedItemIndex = -1;

            RebuildOrder(false);
        }

        private void RebuildOrder(bool preserveCurrent)
        {
            var oldOrder = _order;

            if (_set?.Items == null)
            {
                _order = new List<int>();
                _filteredOrder = new List<int>();
                _shuffleOrder = new List<int>();
                _seenLastCardInNonProgress = false;
                ShowCard();
                return;
            }

            int? currentItem = preserveCurrent && _order.Count > 0 ? _order[_index] : null;

            if (_shuffleEnabled && _shuffleOrder.Count != _set.Items.Count)
                BuildShuffleOrder();

            var baseOrder = _shuffleEnabled && _shuffleOrder.Count == _set.Items.Count
                ? _shuffleOrder
                : Enumerable.Range(0, _set.Items.Count).ToList();

            _filteredOrder = baseOrder
                .Where(i => !_starredOnly || _set.Items[i].IsStarred)
                .ToList();

            _order = new List<int>(_filteredOrder);
            _visitedNonProgress.Clear();
            _lastVisitedItemIndex = -1;
            _completionShown = false;

            bool orderChanged =
                oldOrder == null ||
                oldOrder.Count != _order.Count ||
                !oldOrder.SequenceEqual(_order);

            if (!preserveCurrent || orderChanged)
                _seenLastCardInNonProgress = false;

            if (currentItem.HasValue)
            {
                var pos = _order.IndexOf(currentItem.Value);
                _index = pos >= 0 ? pos : 0;
            }
            else
            {
                _index = 0;
            }

            ShowCard();
        }

        private void BuildShuffleOrder()
        {
            if (_set?.Items == null) return;

            _shuffleOrder = Enumerable.Range(0, _set.Items.Count).ToList();
            Shuffle(_shuffleOrder);
        }

        private static void Shuffle(List<int> list)
        {
            var rng = new Random();

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
