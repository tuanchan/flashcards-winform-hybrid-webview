using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    internal sealed class CardSetLegacyReader
    {
        public CardSet? LoadLegacySet(string dir)
        {
            var jsonPath = Path.Combine(dir, "set.json");
            if (!File.Exists(jsonPath))
                return null;

            var json = File.ReadAllText(jsonPath, CardSetStorage.Utf8NoBomEncoding);
            var set = JsonSerializer.Deserialize<CardSet>(json, CardSetStorage.JsonOptionsValue);
            if (set == null)
                return null;

            if (string.IsNullOrWhiteSpace(set.Id))
                set.Id = Path.GetFileName(dir);

            set.Items ??= new List<CardItem>();
            set.BaseFolder = dir;
            set.FolderName = Path.GetFileName(dir);
            set.VocabCount = set.Items.Count;

            return set;
        }
    }
}
