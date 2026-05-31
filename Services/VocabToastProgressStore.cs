using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TocflQuiz.Services
{
    public sealed class VocabToastProgressStore
    {
        private readonly string _filePath;

        public VocabToastProgressStore(string appName = "FlashCards")
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                appName);

            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "vocab_toast_learned.json");
        }

        public HashSet<string> Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var json = File.ReadAllText(_filePath);
                var arr = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
                return new HashSet<string>(arr, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public void Save(HashSet<string> learnedKeys)
        {
            try
            {
                var arr = new string[learnedKeys.Count];
                learnedKeys.CopyTo(arr);

                var json = JsonSerializer.Serialize(arr, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // ignore
            }
        }
    }
}
