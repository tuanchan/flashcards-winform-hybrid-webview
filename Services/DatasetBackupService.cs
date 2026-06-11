using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public sealed class DatasetBackupResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string FolderPath { get; set; } = "";
    }

    public static class DatasetBackupService
    {
        private const string BackupFolderName = "backup";
        private const string DialoguesFolderName = "Dialogues";

        public static string BackupFolderPath =>
            Path.Combine(AppContext.BaseDirectory, BackupFolderName);

        public static DatasetBackupResult ExportDataset()
        {
            try
            {
                var datasetPath = CardSetStorage.BaseDir;
                if (!Directory.Exists(datasetPath))
                {
                    return new DatasetBackupResult
                    {
                        Success = false,
                        Message = "Chua co thu muc Dataset de xuat."
                    };
                }

                Directory.CreateDirectory(BackupFolderPath);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var zipPath = Path.Combine(BackupFolderPath, $"Dataset_{timestamp}.zip");

                ZipFile.CreateFromDirectory(
                    datasetPath,
                    zipPath,
                    CompressionLevel.Optimal,
                    includeBaseDirectory: true);

                return new DatasetBackupResult
                {
                    Success = true,
                    Message = "Da xuat dataset thanh cong.",
                    FilePath = zipPath,
                    FolderPath = BackupFolderPath
                };
            }
            catch (Exception ex)
            {
                return new DatasetBackupResult
                {
                    Success = false,
                    Message = "Khong xuat duoc dataset: " + ex.Message,
                    FolderPath = BackupFolderPath
                };
            }
        }

        public static DatasetBackupResult ImportDataset(string path)
        {
            if (Directory.Exists(path))
                return ImportDatasetFolder(path);

            return ImportDatasetZip(path);
        }

        public static DatasetBackupResult ImportDatasetZip(string zipPath)
        {
            var tempRoot = "";

            try
            {
                if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
                {
                    return new DatasetBackupResult
                    {
                        Success = false,
                        Message = "File zip khong ton tai."
                    };
                }

                Directory.CreateDirectory(CardSetStorage.BaseDir);

                tempRoot = Path.Combine(Path.GetTempPath(), "TocflQuiz_DatasetImport_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                ZipFile.ExtractToDirectory(zipPath, tempRoot, overwriteFiles: true);

                var sourceDataset = ResolveDatasetSourceRoot(tempRoot);
                var renamedCount = CopyDatasetDirectory(sourceDataset, CardSetStorage.BaseDir);

                return new DatasetBackupResult
                {
                    Success = true,
                    Message = renamedCount > 0
                        ? $"Da import dataset thanh cong. {renamedCount} hoc phan trung ten da duoc doi ten thu muc."
                        : "Da import dataset thanh cong.",
                    FilePath = zipPath,
                    FolderPath = Path.GetDirectoryName(zipPath) ?? ""
                };
            }
            catch (Exception ex)
            {
                return new DatasetBackupResult
                {
                    Success = false,
                    Message = "Khong import duoc dataset: " + ex.Message,
                    FilePath = zipPath,
                    FolderPath = Path.GetDirectoryName(zipPath) ?? ""
                };
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        public static DatasetBackupResult ImportDatasetFolder(string folderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                {
                    return new DatasetBackupResult
                    {
                        Success = false,
                        Message = "Thu muc Dataset khong ton tai."
                    };
                }

                var sourceDataset = ResolveDatasetSourceRoot(folderPath);
                Directory.CreateDirectory(CardSetStorage.BaseDir);

                var sourceFullPath = Path.GetFullPath(sourceDataset).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var targetFullPath = Path.GetFullPath(CardSetStorage.BaseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new DatasetBackupResult
                    {
                        Success = true,
                        Message = "Dataset da nam trong thu muc hien tai.",
                        FolderPath = sourceDataset
                    };
                }

                var renamedCount = CopyDatasetDirectory(sourceDataset, CardSetStorage.BaseDir);

                return new DatasetBackupResult
                {
                    Success = true,
                    Message = renamedCount > 0
                        ? $"Da import dataset thanh cong. {renamedCount} hoc phan trung ten da duoc doi ten thu muc."
                        : "Da import dataset thanh cong.",
                    FilePath = sourceDataset,
                    FolderPath = sourceDataset
                };
            }
            catch (Exception ex)
            {
                return new DatasetBackupResult
                {
                    Success = false,
                    Message = "Khong import duoc dataset: " + ex.Message,
                    FilePath = folderPath,
                    FolderPath = folderPath
                };
            }
        }

        private static string ResolveDatasetSourceRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return root;

            var directDataset = Path.Combine(root, "Dataset");
            if (Directory.Exists(directDataset) && LooksLikeDatasetRoot(directDataset))
                return directDataset;

            if (LooksLikeDatasetRoot(root))
                return root;

            var children = Directory.GetDirectories(root);
            if (children.Length == 1)
            {
                var child = children[0];
                var childDataset = Path.Combine(child, "Dataset");
                if (Directory.Exists(childDataset) && LooksLikeDatasetRoot(childDataset))
                    return childDataset;

                if (LooksLikeDatasetRoot(child))
                    return child;
            }

            return Directory.Exists(directDataset) ? directDataset : root;
        }

        private static bool LooksLikeDatasetRoot(string folder)
        {
            try
            {
                if (!Directory.Exists(folder))
                    return false;

                foreach (var dir in Directory.GetDirectories(folder))
                {
                    if (File.Exists(Path.Combine(dir, CardSetStorage.ConfigFileNameValue)))
                        return true;

                    if (File.Exists(Path.Combine(dir, CardSetStorage.VocabsFolderNameValue, CardSetStorage.VocabsFileNameValue)))
                        return true;

                    if (File.Exists(Path.Combine(dir, CardSetStorage.VocabsFileNameValue)))
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static int CopyDatasetDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var target = Path.Combine(targetDir, Path.GetFileName(file));
                if (!File.Exists(target))
                    File.Copy(file, target);
            }

            var renamedCount = 0;
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var folderName = Path.GetFileName(dir);
                if (IsDialogueFolderName(folderName))
                {
                    MergeDirectoryWithoutOverwriting(dir, Path.Combine(targetDir, DialoguesFolderName));
                    continue;
                }

                var target = Path.Combine(targetDir, folderName);
                if (Directory.Exists(target))
                {
                    target = ResolveUniqueDirectoryPath(target);
                    renamedCount++;
                }

                CopyDirectory(dir, target);
                if (!string.Equals(Path.GetFileName(dir), Path.GetFileName(target), StringComparison.OrdinalIgnoreCase))
                    RenameImportedCourseMetadata(target, Path.GetFileName(target));
            }

            return renamedCount;
        }

        public static int NormalizeDialogueFolders()
        {
            try
            {
                var datasetRoot = CardSetStorage.BaseDir;
                if (!Directory.Exists(datasetRoot))
                    return 0;

                var canonical = Path.Combine(datasetRoot, DialoguesFolderName);
                var duplicates = Directory.GetDirectories(datasetRoot)
                    .Where(path => IsDialogueFolderName(Path.GetFileName(path)) &&
                                   !string.Equals(Path.GetFileName(path), DialoguesFolderName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var changed = 0;
                foreach (var duplicate in duplicates)
                {
                    MergeDirectoryWithoutOverwriting(duplicate, canonical);
                    TryDeleteDirectory(duplicate);
                    changed++;
                }

                return changed;
            }
            catch
            {
                return 0;
            }
        }

        public static int EnsureUniqueCourseTitles()
        {
            try
            {
                var sets = CardSetStorage.LoadAllSetsSafe();
                var changed = 0;

                foreach (var group in sets
                    .Where(s => !string.IsNullOrWhiteSpace(s.Title))
                    .GroupBy(s => s.Title.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1))
                {
                    var ordered = group
                        .OrderByDescending(s => string.Equals(s.FolderName, group.Key, StringComparison.OrdinalIgnoreCase))
                        .ThenBy(s => s.FolderName ?? "")
                        .ToList();

                    for (var i = 1; i < ordered.Count; i++)
                    {
                        var set = ordered[i];
                        var newTitle = !string.IsNullOrWhiteSpace(set.FolderName)
                            ? set.FolderName!
                            : $"{group.Key}({i + 1})";

                        if (string.Equals(set.Title, newTitle, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (RenameImportedCourseMetadata(set.BaseFolder ?? "", newTitle))
                            changed++;
                    }
                }

                return changed;
            }
            catch
            {
                return 0;
            }
        }

        private static string ResolveUniqueDirectoryPath(string desiredPath)
        {
            if (!Directory.Exists(desiredPath))
                return desiredPath;

            var parent = Path.GetDirectoryName(desiredPath) ?? "";
            var name = Path.GetFileName(desiredPath);
            var suffix = 2;

            while (true)
            {
                var candidate = Path.Combine(parent, $"{name}({suffix})");
                if (!Directory.Exists(candidate))
                    return candidate;

                suffix++;
            }
        }

        private static bool RenameImportedCourseMetadata(string courseFolder, string newTitle)
        {
            if (string.IsNullOrWhiteSpace(courseFolder) || string.IsNullOrWhiteSpace(newTitle) || !Directory.Exists(courseFolder))
                return false;

            var changed = false;
            var folderName = Path.GetFileName(courseFolder);
            var configPath = Path.Combine(courseFolder, CardSetStorage.ConfigFileNameValue);
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath, CardSetStorage.Utf8NoBomEncoding);
                    var config = JsonSerializer.Deserialize<CardSetConfig>(json, CardSetStorage.JsonOptionsValue);
                    if (config != null)
                    {
                        config.Title = newTitle;
                        config.FolderName = folderName;
                        File.WriteAllText(
                            configPath,
                            JsonSerializer.Serialize(config, CardSetStorage.JsonOptionsValue),
                            CardSetStorage.Utf8NoBomEncoding);
                        changed = true;
                    }
                }
                catch
                {
                }
            }

            var legacyPath = Path.Combine(courseFolder, "set.json");
            if (File.Exists(legacyPath))
            {
                try
                {
                    var json = File.ReadAllText(legacyPath, CardSetStorage.Utf8NoBomEncoding);
                    var set = JsonSerializer.Deserialize<CardSet>(json, CardSetStorage.JsonOptionsValue);
                    if (set != null)
                    {
                        set.Id = folderName;
                        set.Title = newTitle;
                        set.FolderName = folderName;
                        set.BaseFolder = courseFolder;
                        File.WriteAllText(
                            legacyPath,
                            JsonSerializer.Serialize(set, CardSetStorage.JsonOptionsValue),
                            CardSetStorage.Utf8NoBomEncoding);
                        changed = true;
                    }
                }
                catch
                {
                }
            }

            return changed;
        }

        private static bool IsDialogueFolderName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var value = name.Trim();
            if (string.Equals(value, DialoguesFolderName, StringComparison.OrdinalIgnoreCase))
                return true;

            return value.StartsWith(DialoguesFolderName + "(", StringComparison.OrdinalIgnoreCase) &&
                   value.EndsWith(")", StringComparison.Ordinal);
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var target = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, target, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var target = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, target);
            }
        }

        private static void MergeDirectoryWithoutOverwriting(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var target = Path.Combine(targetDir, Path.GetFileName(file));
                if (!File.Exists(target))
                    File.Copy(file, target);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var target = Path.Combine(targetDir, Path.GetFileName(dir));
                MergeDirectoryWithoutOverwriting(dir, target);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
