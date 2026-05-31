#nullable enable

using System.Linq;
using System.Text.Json;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class FlashcardsFeatureControlWeb
    {
        private void ToggleStar()
        {
            if (_set?.Items == null || _set.Items.Count == 0 || _order.Count == 0) return;

            var itemIndex = _order[_index];
            var item = _set.Items[itemIndex];
            var oldKey = Services.CardSetStorage.BuildCardKey(item);

            item.IsStarred = !item.IsStarred;

            if (_sourceSet?.Items != null)
            {
                var match = _sourceSet.Items.FirstOrDefault(x => Services.CardSetStorage.BuildCardKey(x) == oldKey);
                if (match != null)
                    match.IsStarred = item.IsStarred;
            }

            SaveCurrentStudyAndSourceFiles();
            RebuildOrder(true);
        }

        private void OpenEditModalForCurrentCard()
        {
            if (_set?.Items == null || _order.Count == 0) return;

            var itemIndex = _order[_index];
            var item = _set.Items[itemIndex];
            var definition = item.Definition ?? "";
            var pinyin = item.Pinyin ?? "";

            if (string.IsNullOrWhiteSpace(pinyin))
            {
                var split = Services.CardImportParser.SplitTailPronunciation(definition);
                definition = split.Definition;
                pinyin = split.Pronunciation ?? "";
            }

            var payload = new
            {
                term = item.Term ?? "",
                definition,
                pinyin
            };

            var json = JsonSerializer.Serialize(payload);
            ExecuteScript($"openEdit({json});");
        }

        private void SaveEditFromWeb(JsonElement root)
        {
            try
            {
                if (_set?.Items == null || _order.Count == 0) return;

                var term = root.TryGetProperty("term", out var t) ? (t.GetString() ?? "") : "";
                var definition = root.TryGetProperty("definition", out var d) ? (d.GetString() ?? "") : "";
                var pinyin = root.TryGetProperty("pinyin", out var p) ? (p.GetString() ?? "") : "";

                if (string.IsNullOrWhiteSpace(pinyin))
                {
                    var split = Services.CardImportParser.SplitTailPronunciation(definition);
                    definition = split.Definition;
                    pinyin = split.Pronunciation ?? "";
                }

                var itemIndex = _order[_index];
                var item = _set.Items[itemIndex];
                var oldKey = Services.CardSetStorage.BuildCardKey(item);

                item.Term = term.Trim();
                item.Definition = definition.Trim();
                item.Pinyin = string.IsNullOrWhiteSpace(pinyin) ? null : pinyin.Trim();

                if (_sourceSet?.Items != null)
                {
                    var match = _sourceSet.Items.FirstOrDefault(x => Services.CardSetStorage.BuildCardKey(x) == oldKey);
                    if (match != null)
                    {
                        match.Term = item.Term;
                        match.Definition = item.Definition;
                        match.Pinyin = item.Pinyin;
                        match.IsStarred = item.IsStarred;
                    }
                }

                if (_sessionPendingKnownKeys.Remove(oldKey))
                    _sessionPendingKnownKeys.Add(Services.CardSetStorage.BuildCardKey(item));

                SaveCurrentStudyAndSourceFiles();
                ShowCard();
            }
            catch { }
        }

        private void SaveCurrentStudyAndSourceFiles()
        {
            if (_sourceSet != null)
            {
                _sourceSet.VocabCount = _sourceSet.Items?.Count ?? 0;

                if (!string.IsNullOrWhiteSpace(_sourceSet.VocabsFilePath))
                    Services.CardSetStorage.WriteCardsToFile(_sourceSet.VocabsFilePath, _sourceSet.Items);

                if (!string.IsNullOrWhiteSpace(_sourceSet.ConfigFilePath))
                    Services.CardSetStorage.SaveSetJson(_sourceSet);
            }

            if (_set != null && !string.IsNullOrWhiteSpace(_set.NotYetFilePath))
                Services.CardSetStorage.WriteCardsToFile(_set.NotYetFilePath, _set.Items);
        }
    }
}
