#nullable enable

namespace TocflQuiz.Controls.Features
{
    public sealed partial class FlashcardsFeatureControlWeb
    {
        private void ShowCard()
        {
            if (_set?.Items == null || _set.Items.Count == 0 || _order.Count == 0)
            {
                _ = PushStateAsync();
                return;
            }

            if (_index < 0) _index = 0;
            if (_index >= _order.Count) _index = _order.Count - 1;

            var itemIndex = _order[_index];
            var it = _set.Items[itemIndex];
            var cardChanged = itemIndex != _lastCardIndex;
            _lastCardIndex = itemIndex;

            if (!_progressTracking)
            {
                if (itemIndex != _lastVisitedItemIndex)
                {
                    _lastVisitedItemIndex = itemIndex;
                    _visitedNonProgress.Add(itemIndex);
                }
            }

            if (_autoPronounce && cardChanged)
            {
                _ = PlayChineseTermAsync();
            }

            CheckCompletion();
            _ = PushStateAsync();
        }

        private void Prev()
        {
            if (_set?.Items == null || _order.Count == 0) return;
            if (_index <= 0) return;

            _index--;
            ShowCard();
        }

        private void Next()
        {
            if (_set?.Items == null || _order.Count == 0) return;

            if (_index >= _order.Count - 1)
            {
                if (!_progressTracking)
                {
                    _seenLastCardInNonProgress = true;
                    CheckCompletion();
                }

                return;
            }

            _index++;

            if (!_progressTracking && _index == _order.Count - 1)
            {
                _seenLastCardInNonProgress = true;
            }

            ShowCard();
        }

        private void HandlePrevAction()
        {
            if (_progressTracking)
                MarkProgress(CardProgressState.Learning);
            else
                Prev();
        }

        private void HandleNextAction()
        {
            if (_progressTracking)
                MarkProgress(CardProgressState.Known);
            else
                Next();
        }
    }
}
