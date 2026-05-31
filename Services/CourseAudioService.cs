using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public static class CourseAudioService
    {
        public static async Task GenerateMissingAudioAsync(CardSet? set, CancellationToken token = default)
        {
            if (set?.Items == null || set.Items.Count == 0)
                return;

            var voice = EdgeTtsRunner.ResolveVoiceByLanguageCode(set.LanguageCode);
            var audioDir = GetAudioDir(set);

            foreach (var item in set.Items)
            {
                token.ThrowIfCancellationRequested();

                var text = (item.Term ?? "").Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                try
                {
                    var file = GetAudioFilePath(set, text, voice);
                    if (File.Exists(file))
                        continue;

                    var legacy = FindLegacyAudioFile(set, text, voice);
                    if (!string.IsNullOrWhiteSpace(legacy) && File.Exists(legacy))
                    {
                        Directory.CreateDirectory(audioDir);
                        File.Copy(legacy, file, overwrite: false);
                        continue;
                    }

                    var mp3 = await EdgeTtsRunner.SynthesizeMp3Async(text, voice, token);
                    Directory.CreateDirectory(audioDir);
                    await File.WriteAllBytesAsync(file, mp3, token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Keep the course creation flow alive if one term cannot be synthesized.
                }
            }
        }

        public static async Task<byte[]> GetOrCreateAudioAsync(CardSet? set, string text, CancellationToken token = default)
        {
            if (set == null || string.IsNullOrWhiteSpace(text))
                return Array.Empty<byte>();

            var voice = EdgeTtsRunner.ResolveVoiceByLanguageCode(set.LanguageCode);
            var file = GetAudioFilePath(set, text, voice);

            if (File.Exists(file))
                return await File.ReadAllBytesAsync(file, token);

            var legacy = FindLegacyAudioFile(set, text, voice);
            if (!string.IsNullOrWhiteSpace(legacy) && File.Exists(legacy))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.Copy(legacy, file, overwrite: false);
                return await File.ReadAllBytesAsync(file, token);
            }

            var mp3 = await EdgeTtsRunner.SynthesizeMp3Async(text, voice, token);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllBytesAsync(file, mp3, token);
            return mp3;
        }

        public static void ClearAudioCache(CardSet? set)
        {
            if (set == null)
                return;

            var baseFolder = ResolveBaseFolder(set);
            if (string.IsNullOrWhiteSpace(baseFolder))
                return;

            var audioDir = Path.Combine(baseFolder, CardSetStorage.VocabsFolderNameValue, CardSetStorage.AudioFolderNameValue);
            DeleteDirectoryContents(audioDir);

            var legacyDir = Path.Combine(baseFolder, "tts_mp3");
            if (Directory.Exists(legacyDir))
            {
                try { Directory.Delete(legacyDir, recursive: true); } catch { }
            }
        }

        public static string GetAudioDir(CardSet set)
        {
            var baseFolder = ResolveBaseFolder(set);
            var dir = Path.Combine(baseFolder, CardSetStorage.VocabsFolderNameValue, CardSetStorage.AudioFolderNameValue);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string GetAudioFilePath(CardSet set, string text, string voice)
        {
            var fileName = HashTextToFileName($"{voice}|{text}") + ".mp3";
            return Path.Combine(GetAudioDir(set), fileName);
        }

        private static string ResolveBaseFolder(CardSet set)
        {
            if (!string.IsNullOrWhiteSpace(set.BaseFolder))
                return set.BaseFolder;

            if (!string.IsNullOrWhiteSpace(set.ConfigFilePath))
                return Path.GetDirectoryName(set.ConfigFilePath) ?? CardSetStorage.BaseDir;

            if (!string.IsNullOrWhiteSpace(set.VocabsFilePath))
            {
                var vocabsDir = Path.GetDirectoryName(set.VocabsFilePath);
                var parent = string.IsNullOrWhiteSpace(vocabsDir) ? null : Directory.GetParent(vocabsDir);
                if (parent != null) return parent.FullName;
            }

            var folderName = string.IsNullOrWhiteSpace(set.FolderName)
                ? (string.IsNullOrWhiteSpace(set.Id) ? "unknown_set" : set.Id)
                : set.FolderName;

            return Path.Combine(CardSetStorage.BaseDir, MakeSafeFileName(folderName));
        }

        private static string? FindLegacyAudioFile(CardSet set, string text, string voice)
        {
            var baseFolder = ResolveBaseFolder(set);
            var candidates = new List<string>
            {
                Path.Combine(baseFolder, "tts_mp3", MakeSafeFileName(voice), HashTextToFileName(text) + ".mp3"),
                Path.Combine(baseFolder, "tts_mp3", MakeSafeFileName(voice), HashTextToFileName($"{voice}|{text}") + ".mp3")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static string HashTextToFileName(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text ?? "");
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
        }

        private static string MakeSafeFileName(string s)
        {
            s ??= "";

            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');

            return s.Trim();
        }

        private static void DeleteDirectoryContents(string directory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                    return;

                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    try { File.Delete(file); } catch { }
                }

                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    try { Directory.Delete(child, recursive: true); } catch { }
                }
            }
            catch
            {
            }
        }
    }
}
