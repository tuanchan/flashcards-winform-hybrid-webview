using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public static class CardSetStorage
    {
        private const string DatasetFolderNameConst = "Dataset";
        private const string VocabsFolderNameConst = "Vocabs";
        private const string VocabsFileNameConst = "Vocabs.txt";
        private const string NotYetFileNameConst = "NotYet.txt";
        private const string AudioFolderNameConst = "audio";
        private const string ConfigFileNameConst = "Config.json";

        private static readonly UTF8Encoding Utf8NoBom = new(false);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private static string? _configuredDatasetRoot;

        private static readonly CardSetNameNormalizer NameNormalizer = new();
        private static readonly CardSetPathResolver PathResolver = new(NameNormalizer);
        private static readonly CardSetTextParser TextParser = new();
        private static readonly CardSetLegacyReader LegacyReader = new();
        private static readonly CardSetRepository Repository = new(PathResolver, LegacyReader, TextParser, NameNormalizer);

        internal static string DatasetRoot => PathResolver.ResolveDatasetRoot(_configuredDatasetRoot);

        public static string BaseDir => Path.Combine(DatasetRoot, DatasetFolderNameConst);

        public static void ConfigureDatasetRoot(string? datasetRoot)
        {
            _configuredDatasetRoot = string.IsNullOrWhiteSpace(datasetRoot)
                ? null
                : datasetRoot.Trim();
        }

        public static string EnsureDir() => Repository.EnsureDir();

        public static string SaveSet(CardSet set, string rawInput, string termDefSep, string cardSep)
            => Repository.SaveSet(set, rawInput, termDefSep, cardSep);

        public static string SaveSetJson(CardSet set) => Repository.SaveSetJson(set);

        public static IReadOnlyList<CardSet> LoadAllSetsSafe() => Repository.LoadAllSetsSafe();

        public static bool DeleteSetById(string? setId) => Repository.DeleteSetById(setId);

        public static bool UpdateSetMetadata(
            string? setId,
            string? title,
            string? language,
            string? languageCode,
            out CardSet? updatedSet)
            => Repository.UpdateSetMetadata(setId, title, language, languageCode, out updatedSet);

        public static List<CardItem> LoadVocabularyItems(CardSet? set) => Repository.LoadVocabularyItems(set);

        public static List<CardItem> LoadStudyItems(CardSet? set) => Repository.LoadStudyItems(set);

        public static void ResetNotYet(CardSet? set) => Repository.ResetNotYet(set);

        public static List<CardItem> LoadCardsFromFile(string? path) => TextParser.LoadCardsFromFile(path);

        public static void WriteCardsToFile(string? path, IEnumerable<CardItem>? items) => TextParser.WriteCardsToFile(path, items);

        public static string BuildCardKey(CardItem? item) => TextParser.BuildCardKey(item);

        internal static string VocabsFolderNameValue => VocabsFolderNameConst;
        internal static string VocabsFileNameValue => VocabsFileNameConst;
        internal static string NotYetFileNameValue => NotYetFileNameConst;
        internal static string AudioFolderNameValue => AudioFolderNameConst;
        internal static string ConfigFileNameValue => ConfigFileNameConst;
        internal static UTF8Encoding Utf8NoBomEncoding => Utf8NoBom;
        internal static JsonSerializerOptions JsonOptionsValue => JsonOptions;
    }
}
