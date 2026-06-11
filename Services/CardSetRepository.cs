using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    internal sealed class CardSetRepository
    {
        private readonly CardSetPathResolver _pathResolver;
        private readonly CardSetLegacyReader _legacyReader;
        private readonly CardSetTextParser _textParser;
        private readonly CardSetNameNormalizer _nameNormalizer;

        public CardSetRepository(
            CardSetPathResolver pathResolver,
            CardSetLegacyReader legacyReader,
            CardSetTextParser textParser,
            CardSetNameNormalizer nameNormalizer)
        {
            _pathResolver = pathResolver;
            _legacyReader = legacyReader;
            _textParser = textParser;
            _nameNormalizer = nameNormalizer;
        }

        public string EnsureDir()
        {
            Directory.CreateDirectory(CardSetStorage.BaseDir);
            return CardSetStorage.BaseDir;
        }

        public string SaveSet(CardSet set, string rawInput, string termDefSep, string cardSep)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));

            _ = rawInput;
            _ = termDefSep;
            _ = cardSep;

            EnsureDir();

            set.Items ??= new List<CardItem>();
            if (set.CreatedAt == default)
                set.CreatedAt = DateTime.Now;

            var existingTitles = new HashSet<string>(
                LoadAllSetsSafe()
                    .Select(s => (s.Title ?? "").Trim())
                    .Where(title => !string.IsNullOrWhiteSpace(title)),
                StringComparer.OrdinalIgnoreCase);

            var actualTitle = _nameNormalizer.EnsureUniqueTitle(set.Title, existingTitles);
            var folderBaseName = _pathResolver.BuildFolderBaseName(actualTitle, set.LanguageCode, set.Language);
            var folderName = _pathResolver.EnsureUniqueFolderName(folderBaseName);

            var courseDir = Path.Combine(CardSetStorage.BaseDir, folderName);
            var vocabsDir = Path.Combine(courseDir, CardSetStorage.VocabsFolderNameValue);
            var audioDir = Path.Combine(vocabsDir, CardSetStorage.AudioFolderNameValue);
            var vocabPath = Path.Combine(vocabsDir, CardSetStorage.VocabsFileNameValue);
            var notYetPath = Path.Combine(vocabsDir, CardSetStorage.NotYetFileNameValue);
            var configPath = Path.Combine(courseDir, CardSetStorage.ConfigFileNameValue);

            Directory.CreateDirectory(audioDir);

            set.Id = folderName;
            set.Title = actualTitle;
            set.FolderName = folderName;
            set.BaseFolder = courseDir;
            set.VocabsFilePath = vocabPath;
            set.NotYetFilePath = notYetPath;
            set.ConfigFilePath = configPath;
            set.Language = (set.Language ?? "").Trim();
            set.LanguageCode = _nameNormalizer.NormalizeLanguageCode(set.LanguageCode);
            set.VocabCount = set.Items.Count;

            _textParser.WriteCardsToFile(vocabPath, set.Items);
            _textParser.WriteCardsToFile(notYetPath, set.Items);
            WriteConfigFile(set);

            return courseDir;
        }

        public string SaveSetJson(CardSet set)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));

            set.Items ??= new List<CardItem>();
            if (set.CreatedAt == default)
                set.CreatedAt = DateTime.Now;

            if (IsStructuredSet(set))
            {
                EnsureStructuredPaths(set);
                set.VocabCount = set.Items.Count;

                _textParser.WriteCardsToFile(set.VocabsFilePath, set.Items);

                if (!string.IsNullOrWhiteSpace(set.NotYetFilePath) && !File.Exists(set.NotYetFilePath))
                    _textParser.WriteCardsToFile(set.NotYetFilePath, set.Items);

                WriteConfigFile(set);
                return set.BaseFolder ?? "";
            }

            EnsureDir();

            if (string.IsNullOrWhiteSpace(set.Id))
                set.Id = $"set_{DateTime.Now:yyyyMMdd_HHmmss}";

            var safeId = _nameNormalizer.MakeSafeFileName(set.Id);
            var setDir = Path.Combine(CardSetStorage.BaseDir, safeId);
            Directory.CreateDirectory(setDir);

            var jsonPath = Path.Combine(setDir, "set.json");
            var json = JsonSerializer.Serialize(set, CardSetStorage.JsonOptionsValue);
            File.WriteAllText(jsonPath, json, CardSetStorage.Utf8NoBomEncoding);

            set.BaseFolder = setDir;
            set.FolderName = Path.GetFileName(setDir);
            set.VocabCount = set.Items.Count;

            return setDir;
        }

        public IReadOnlyList<CardSet> LoadAllSetsSafe()
        {
            EnsureDir();

            var sets = new List<CardSet>();
            var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in _pathResolver.EnumerateSetRoots())
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    try
                    {
                        CardSet? set = null;

                        if (File.Exists(Path.Combine(dir, CardSetStorage.ConfigFileNameValue)))
                        {
                            set = LoadStructuredSet(dir);
                        }
                        else if (File.Exists(Path.Combine(dir, "set.json")))
                        {
                            set = _legacyReader.LoadLegacySet(dir);
                        }

                        if (set == null)
                            continue;

                        if (!string.IsNullOrWhiteSpace(set.BaseFolder) && !seenFolders.Add(set.BaseFolder))
                            continue;

                        sets.Add(set);
                    }
                    catch
                    {
                        // bỏ qua set lỗi để app vẫn chạy
                    }
                }
            }

            return sets
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
        }

        public bool DeleteSetById(string? setId)
        {
            if (string.IsNullOrWhiteSpace(setId))
                return false;

            try
            {
                var match = LoadAllSetsSafe().FirstOrDefault(s =>
                    string.Equals(s.Id, setId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.FolderName, setId, StringComparison.OrdinalIgnoreCase));

                if (match != null && !string.IsNullOrWhiteSpace(match.BaseFolder) && Directory.Exists(match.BaseFolder))
                {
                    Directory.Delete(match.BaseFolder, recursive: true);
                    return true;
                }

                foreach (var root in _pathResolver.EnumerateSetRoots())
                {
                    if (!Directory.Exists(root))
                        continue;

                    var candidate = Path.Combine(root, _nameNormalizer.MakeSafeFileName(setId));
                    if (!Directory.Exists(candidate))
                        continue;

                    Directory.Delete(candidate, recursive: true);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public bool UpdateSetMetadata(
            string? setId,
            string? title,
            string? language,
            string? languageCode,
            string? topicId,
            out CardSet? updatedSet)
        {
            updatedSet = null;

            if (string.IsNullOrWhiteSpace(setId))
                return false;

            var match = LoadAllSetsSafe().FirstOrDefault(s =>
                string.Equals(s.Id, setId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.FolderName, setId, StringComparison.OrdinalIgnoreCase));

            if (match == null)
                return false;

            var nextTitle = (title ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(nextTitle))
                match.Title = nextTitle;

            match.Language = (language ?? "").Trim();
            match.LanguageCode = _nameNormalizer.NormalizeLanguageCode(languageCode);
            match.TopicId = topicId;
            match.Items = LoadVocabularyItems(match);
            match.VocabCount = match.Items.Count;

            if (!string.IsNullOrWhiteSpace(match.ConfigFilePath) ||
                (!string.IsNullOrWhiteSpace(match.BaseFolder) &&
                 File.Exists(Path.Combine(match.BaseFolder, CardSetStorage.ConfigFileNameValue))))
            {
                EnsureStructuredPaths(match);
                WriteConfigFile(match);
                updatedSet = match;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(match.BaseFolder))
            {
                var jsonPath = Path.Combine(match.BaseFolder, "set.json");
                if (File.Exists(jsonPath))
                {
                    var json = JsonSerializer.Serialize(match, CardSetStorage.JsonOptionsValue);
                    File.WriteAllText(jsonPath, json, CardSetStorage.Utf8NoBomEncoding);
                    updatedSet = match;
                    return true;
                }
            }

            updatedSet = match;
            return false;
        }

        public List<CardItem> LoadVocabularyItems(CardSet? set)
        {
            if (set == null)
                return new List<CardItem>();

            if (!string.IsNullOrWhiteSpace(set.VocabsFilePath))
            {
                var items = _textParser.LoadCardsFromFile(set.VocabsFilePath);
                if (items.Count > 0 || File.Exists(set.VocabsFilePath))
                    return items;
            }

            return _textParser.CloneCards(set.Items);
        }

        public List<CardItem> LoadStudyItems(CardSet? set)
        {
            if (set == null)
                return new List<CardItem>();

            if (!string.IsNullOrWhiteSpace(set.NotYetFilePath))
            {
                if (File.Exists(set.NotYetFilePath))
                    return _textParser.LoadCardsFromFile(set.NotYetFilePath);

                var fullItems = LoadVocabularyItems(set);
                _textParser.WriteCardsToFile(set.NotYetFilePath, fullItems);
                return fullItems;
            }

            return _textParser.CloneCards(set.Items);
        }

        public void ResetNotYet(CardSet? set)
        {
            if (set == null || string.IsNullOrWhiteSpace(set.NotYetFilePath))
                return;

            var fullItems = LoadVocabularyItems(set);
            _textParser.WriteCardsToFile(set.NotYetFilePath, fullItems);
        }

        private CardSet? LoadStructuredSet(string dir)
        {
            var configPath = Path.Combine(dir, CardSetStorage.ConfigFileNameValue);
            if (!File.Exists(configPath))
                return null;

            var json = File.ReadAllText(configPath, CardSetStorage.Utf8NoBomEncoding);
            var config = JsonSerializer.Deserialize<CardSetConfig>(json, CardSetStorage.JsonOptionsValue);
            if (config == null)
                return null;

            var vocabPath = _pathResolver.ResolveStructuredPath(
                dir,
                config.RelativeVocabPath,
                Path.Combine(CardSetStorage.VocabsFolderNameValue, CardSetStorage.VocabsFileNameValue));

            var notYetPath = _pathResolver.ResolveStructuredPath(
                dir,
                config.RelativeNotYetPath,
                Path.Combine(CardSetStorage.VocabsFolderNameValue, CardSetStorage.NotYetFileNameValue));

            var items = _textParser.LoadCardsFromFile(vocabPath);
            var createdAt = ParseCreatedAt(config.CreatedAt);
            var folderName = Path.GetFileName(dir);

            return new CardSet
            {
                Id = folderName,
                Title = string.IsNullOrWhiteSpace(config.Title) ? folderName : config.Title.Trim(),
                CreatedAt = createdAt,
                Language = config.Language?.Trim(),
                LanguageCode = _nameNormalizer.NormalizeLanguageCode(config.LanguageCode),
                FolderName = folderName,
                BaseFolder = dir,
                VocabsFilePath = vocabPath,
                NotYetFilePath = notYetPath,
                ConfigFilePath = configPath,
                CoverImagePath = ResolveCoverImagePath(dir, config.CoverImagePath),
                VocabCount = config.VocabCount > 0 ? config.VocabCount : items.Count,
                Items = items,
                TopicId = config.TopicId
            };
        }

        private void WriteConfigFile(CardSet set)
        {
            if (set == null)
                return;

            EnsureStructuredPaths(set);

            var config = new CardSetConfig
            {
                Title = (set.Title ?? "").Trim(),
                CreatedAt = (set.CreatedAt == default ? DateTime.Now : set.CreatedAt).ToString("O"),
                Language = (set.Language ?? "").Trim(),
                LanguageCode = _nameNormalizer.NormalizeLanguageCode(set.LanguageCode),
                VocabCount = set.VocabCount > 0 ? set.VocabCount : (set.Items?.Count ?? 0),
                FolderName = set.FolderName ?? Path.GetFileName(set.BaseFolder ?? ""),
                CoverImagePath = ToConfigCoverImagePath(set),
                TopicId = set.TopicId,
                RelativeVocabPath = $"{CardSetStorage.VocabsFolderNameValue}/{CardSetStorage.VocabsFileNameValue}",
                RelativeNotYetPath = $"{CardSetStorage.VocabsFolderNameValue}/{CardSetStorage.NotYetFileNameValue}",
                RelativeAudioDir = $"{CardSetStorage.VocabsFolderNameValue}/{CardSetStorage.AudioFolderNameValue}"
            };

            if (!string.IsNullOrWhiteSpace(set.ConfigFilePath))
            {
                var dir = Path.GetDirectoryName(set.ConfigFilePath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(
                    set.ConfigFilePath,
                    JsonSerializer.Serialize(config, CardSetStorage.JsonOptionsValue),
                    CardSetStorage.Utf8NoBomEncoding);
            }
        }

        private void EnsureStructuredPaths(CardSet set)
        {
            if (string.IsNullOrWhiteSpace(set.FolderName))
            {
                set.FolderName = !string.IsNullOrWhiteSpace(set.Id)
                    ? _nameNormalizer.MakeSafeFolderSegment(set.Id)
                    : _nameNormalizer.MakeSafeFolderSegment(set.Title);
            }

            if (string.IsNullOrWhiteSpace(set.BaseFolder))
                set.BaseFolder = Path.Combine(CardSetStorage.BaseDir, set.FolderName ?? _nameNormalizer.MakeSafeFolderSegment(set.Title));

            var vocabsDir = Path.Combine(set.BaseFolder, CardSetStorage.VocabsFolderNameValue);
            var audioDir = Path.Combine(vocabsDir, CardSetStorage.AudioFolderNameValue);

            Directory.CreateDirectory(audioDir);

            set.VocabsFilePath ??= Path.Combine(vocabsDir, CardSetStorage.VocabsFileNameValue);
            set.NotYetFilePath ??= Path.Combine(vocabsDir, CardSetStorage.NotYetFileNameValue);
            set.ConfigFilePath ??= Path.Combine(set.BaseFolder, CardSetStorage.ConfigFileNameValue);
            set.Id = string.IsNullOrWhiteSpace(set.Id) ? (set.FolderName ?? Path.GetFileName(set.BaseFolder)) : set.Id;
        }

        private static bool IsStructuredSet(CardSet set)
        {
            if (set == null)
                return false;

            if (!string.IsNullOrWhiteSpace(set.ConfigFilePath) ||
                !string.IsNullOrWhiteSpace(set.VocabsFilePath) ||
                !string.IsNullOrWhiteSpace(set.NotYetFilePath))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(set.BaseFolder))
                return false;

            return Directory.Exists(Path.Combine(set.BaseFolder, CardSetStorage.VocabsFolderNameValue));
        }

        private static DateTime ParseCreatedAt(string? value)
        {
            if (DateTime.TryParse(value, out var parsed))
                return parsed;

            return DateTime.Now;
        }

        private static string ResolveCoverImagePath(string baseFolder, string? value)
        {
            var path = (value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(path))
                return "";

            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFile))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(baseFolder, path.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ToConfigCoverImagePath(CardSet set)
        {
            var path = (set.CoverImagePath ?? "").Trim();
            if (string.IsNullOrWhiteSpace(path))
                return "";

            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFile))
            {
                return path;
            }

            var baseFolder = set.BaseFolder ?? "";
            if (string.IsNullOrWhiteSpace(baseFolder))
                return path;

            var rel = Path.GetRelativePath(baseFolder, path);
            return rel.StartsWith("..", StringComparison.Ordinal) ? path : rel.Replace('\\', '/');
        }

       
    }
}
