using System;
using System.Collections.Generic;
using System.IO;

namespace TocflQuiz.Services
{
    internal sealed class CardSetPathResolver
    {
        private readonly CardSetNameNormalizer _nameNormalizer;

        public CardSetPathResolver(CardSetNameNormalizer nameNormalizer)
        {
            _nameNormalizer = nameNormalizer;
        }

        public string ResolveDatasetRoot(string? configuredDatasetRoot)
        {
            var configured = NormalizeRoot(configuredDatasetRoot);
            if (!string.IsNullOrWhiteSpace(configured) && CanUseRoot(configured))
                return configured;

            return NormalizeRoot(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlashCards"))!;
        }

        public IEnumerable<string> EnumerateSetRoots()
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                CardSetStorage.BaseDir
            };

            AddExistingRoot(roots, Path.Combine(NormalizeRoot(AppContext.BaseDirectory)!, "Dataset"));

            AddLegacyHocPhanRoot(roots, CardSetStorage.DatasetRoot);
            AddLegacyHocPhanRoot(roots, NormalizeRoot(AppContext.BaseDirectory));

            return roots;
        }

        public string ResolveStructuredPath(string baseFolder, string? relativePath, string defaultRelativePath)
        {
            var relative = string.IsNullOrWhiteSpace(relativePath)
                ? defaultRelativePath
                : relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

            return Path.Combine(baseFolder, relative);
        }

        public string BuildFolderBaseName(string? title, string? languageCode, string? language)
        {
            var titlePart = _nameNormalizer.MakeSafeFolderSegment(title);
            var languagePart = _nameNormalizer.MakeSafeFolderSegment(
                !string.IsNullOrWhiteSpace(languageCode)
                    ? _nameNormalizer.NormalizeLanguageCode(languageCode)
                    : language);

            if (string.IsNullOrWhiteSpace(languagePart))
                return titlePart;

            return $"{titlePart}_{languagePart}";
        }

        public string EnsureUniqueFolderName(string folderBaseName)
        {
            var baseName = string.IsNullOrWhiteSpace(folderBaseName)
                ? "Course"
                : folderBaseName;

            var candidate = baseName;
            var suffix = 2;

            while (Directory.Exists(Path.Combine(CardSetStorage.BaseDir, candidate)))
            {
                candidate = $"{baseName}({suffix})";
                suffix++;
            }

            return candidate;
        }

        private static string? NormalizeRoot(string? root)
        {
            if (string.IsNullOrWhiteSpace(root))
                return null;

            return root.Trim()
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static void AddExistingRoot(HashSet<string> roots, string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                roots.Add(path);
        }

        private static void AddLegacyHocPhanRoot(HashSet<string> roots, string? root)
        {
            var parent = string.IsNullOrWhiteSpace(root)
                ? null
                : Directory.GetParent(root);

            if (parent == null)
                return;

            AddExistingRoot(roots, Path.Combine(parent.FullName, "hocphan"));
        }

        private static bool CanUseRoot(string root)
        {
            try
            {
                Directory.CreateDirectory(root);

                var probe = Path.Combine(root, ".tocflquiz_write_test_" + Guid.NewGuid().ToString("N"));
                using (File.Create(probe, 1, FileOptions.DeleteOnClose))
                {
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
