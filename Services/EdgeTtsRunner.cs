using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TocflQuiz.Services
{
    public sealed class EdgeTtsVoice
    {
        public string Voice { get; set; } = "";
        public string Label { get; set; } = "";
        public string LanguageKey { get; set; } = "";
        public string LanguageName { get; set; } = "";
        public string LanguageCode { get; set; } = "";
        public string Country { get; set; } = "";
        public string Gender { get; set; } = "";
    }

    public static class EdgeTtsRunner
    {
        public const string DefaultVoice = "zh-CN-XiaoxiaoNeural";

        private static readonly IReadOnlyList<EdgeTtsVoice> Voices = new List<EdgeTtsVoice>
        {
            new() { Voice = "zh-CN-XiaoxiaoNeural", Label = "China - Xiaoxiao (Nữ)", LanguageKey = "zh", LanguageName = "Chinese", LanguageCode = "zh-CN", Country = "China", Gender = "female" },
            new() { Voice = "zh-CN-YunxiNeural", Label = "China - Yunxi (Nam)", LanguageKey = "zh", LanguageName = "Chinese", LanguageCode = "zh-CN", Country = "China", Gender = "male" },
            new() { Voice = "zh-TW-HsiaoChenNeural", Label = "Taiwan - HsiaoChen (Nữ)", LanguageKey = "zh", LanguageName = "Chinese", LanguageCode = "zh-TW", Country = "Taiwan", Gender = "female" },
            new() { Voice = "zh-TW-YunJheNeural", Label = "Taiwan - YunJhe (Nam)", LanguageKey = "zh", LanguageName = "Chinese", LanguageCode = "zh-TW", Country = "Taiwan", Gender = "male" },

            new() { Voice = "en-US-JennyNeural", Label = "United States - Jenny (Nữ)", LanguageKey = "en", LanguageName = "English", LanguageCode = "en-US", Country = "United States", Gender = "female" },
            new() { Voice = "en-US-GuyNeural", Label = "United States - Guy (Nam)", LanguageKey = "en", LanguageName = "English", LanguageCode = "en-US", Country = "United States", Gender = "male" },
            new() { Voice = "en-GB-SoniaNeural", Label = "United Kingdom - Sonia (Nữ)", LanguageKey = "en", LanguageName = "English", LanguageCode = "en-GB", Country = "United Kingdom", Gender = "female" },
            new() { Voice = "en-GB-RyanNeural", Label = "United Kingdom - Ryan (Nam)", LanguageKey = "en", LanguageName = "English", LanguageCode = "en-GB", Country = "United Kingdom", Gender = "male" },

            new() { Voice = "vi-VN-HoaiMyNeural", Label = "Vietnam - HoaiMy (Nữ)", LanguageKey = "vi", LanguageName = "Vietnamese", LanguageCode = "vi-VN", Country = "Vietnam", Gender = "female" },
            new() { Voice = "vi-VN-NamMinhNeural", Label = "Vietnam - NamMinh (Nam)", LanguageKey = "vi", LanguageName = "Vietnamese", LanguageCode = "vi-VN", Country = "Vietnam", Gender = "male" },

            new() { Voice = "ja-JP-NanamiNeural", Label = "Japan - Nanami (Nữ)", LanguageKey = "ja", LanguageName = "Japanese", LanguageCode = "ja-JP", Country = "Japan", Gender = "female" },
            new() { Voice = "ja-JP-KeitaNeural", Label = "Japan - Keita (Nam)", LanguageKey = "ja", LanguageName = "Japanese", LanguageCode = "ja-JP", Country = "Japan", Gender = "male" },

            new() { Voice = "ko-KR-SunHiNeural", Label = "Korea - SunHi (Nữ)", LanguageKey = "ko", LanguageName = "Korean", LanguageCode = "ko-KR", Country = "Korea", Gender = "female" },
            new() { Voice = "ko-KR-InJoonNeural", Label = "Korea - InJoon (Nam)", LanguageKey = "ko", LanguageName = "Korean", LanguageCode = "ko-KR", Country = "Korea", Gender = "male" },

            new() { Voice = "de-DE-KatjaNeural", Label = "Germany - Katja (Nữ)", LanguageKey = "de", LanguageName = "German", LanguageCode = "de-DE", Country = "Germany", Gender = "female" },
            new() { Voice = "de-DE-ConradNeural", Label = "Germany - Conrad (Nam)", LanguageKey = "de", LanguageName = "German", LanguageCode = "de-DE", Country = "Germany", Gender = "male" },

            new() { Voice = "fr-FR-DeniseNeural", Label = "France - Denise (Nữ)", LanguageKey = "fr", LanguageName = "French", LanguageCode = "fr-FR", Country = "France", Gender = "female" },
            new() { Voice = "fr-FR-HenriNeural", Label = "France - Henri (Nam)", LanguageKey = "fr", LanguageName = "French", LanguageCode = "fr-FR", Country = "France", Gender = "male" },

            new() { Voice = "es-ES-ElviraNeural", Label = "Spain - Elvira (Nữ)", LanguageKey = "es", LanguageName = "Spanish", LanguageCode = "es-ES", Country = "Spain", Gender = "female" },
            new() { Voice = "es-ES-AlvaroNeural", Label = "Spain - Alvaro (Nam)", LanguageKey = "es", LanguageName = "Spanish", LanguageCode = "es-ES", Country = "Spain", Gender = "male" },

            new() { Voice = "ru-RU-SvetlanaNeural", Label = "Russia - Svetlana (Nữ)", LanguageKey = "ru", LanguageName = "Russian", LanguageCode = "ru-RU", Country = "Russia", Gender = "female" },
            new() { Voice = "ru-RU-DmitryNeural", Label = "Russia - Dmitry (Nam)", LanguageKey = "ru", LanguageName = "Russian", LanguageCode = "ru-RU", Country = "Russia", Gender = "male" }
        };

        public static IReadOnlyList<EdgeTtsVoice> GetSupportedVoices() => Voices;

        public static string ResolveVoiceByLanguageCode(string? languageCode)
        {
            var code = (languageCode ?? "").Trim().ToLowerInvariant();

            return code switch
            {
                "zh" => "zh-CN-XiaoxiaoNeural",
                "zh-cn" => "zh-CN-XiaoxiaoNeural",
                "zh-hans" => "zh-CN-XiaoxiaoNeural",
                "zh-tw" => "zh-CN-XiaoxiaoNeural",
                "zh-hant" => "zh-CN-XiaoxiaoNeural",
                "zh-hk" => "zh-CN-XiaoxiaoNeural",
                "zh-mo" => "zh-CN-XiaoxiaoNeural",
                "de" => "de-DE-KatjaNeural",
                "de-de" => "de-DE-KatjaNeural",
                "en" => "en-US-JennyNeural",
                "en-us" => "en-US-JennyNeural",
                "en-gb" => "en-GB-SoniaNeural",
                "ja" => "ja-JP-NanamiNeural",
                "ja-jp" => "ja-JP-NanamiNeural",
                "ko" => "ko-KR-SunHiNeural",
                "ko-kr" => "ko-KR-SunHiNeural",
                "vi" => "vi-VN-HoaiMyNeural",
                "vi-vn" => "vi-VN-HoaiMyNeural",
                "fr" => "fr-FR-DeniseNeural",
                "fr-fr" => "fr-FR-DeniseNeural",
                "es" => "es-ES-ElviraNeural",
                "es-es" => "es-ES-ElviraNeural",
                "ru" => "ru-RU-SvetlanaNeural",
                "ru-ru" => "ru-RU-SvetlanaNeural",
                _ => DefaultVoice
            };
        }

        public static async Task<byte[]> SynthesizeMp3Async(string text, string? voice = null, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<byte>();

            text = NormalizeText(text);
            voice = ResolveSupportedVoice(voice);

            var tempDir = Path.Combine(Path.GetTempPath(), "FlashCardsTTS");
            Directory.CreateDirectory(tempDir);

            Exception? lastError = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    return await SynthesizeOnceAsync(text, voice, tempDir, token);
                }
                catch (Exception ex) when (IsRetryableEdgeTtsError(ex) && attempt < 3)
                {
                    lastError = ex;
                    await Task.Delay(TimeSpan.FromMilliseconds(450 * attempt), token);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(BuildFriendlyError(ex, voice), ex);
                }
            }

            throw new InvalidOperationException(BuildFriendlyError(lastError, voice), lastError);
        }

        private static async Task<byte[]> SynthesizeOnceAsync(
            string text,
            string voice,
            string tempDir,
            CancellationToken token)
        {
            var mp3Path = Path.Combine(tempDir, $"tts_{Guid.NewGuid():N}.mp3");

            var psi = new ProcessStartInfo
            {
                FileName = "edge-tts",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            psi.ArgumentList.Add("--voice");
            psi.ArgumentList.Add(voice);
            psi.ArgumentList.Add("--text");
            psi.ArgumentList.Add(text);
            psi.ArgumentList.Add("--write-media");
            psi.ArgumentList.Add(mp3Path);

            try
            {
                using var p = Process.Start(psi);
                if (p == null)
                    throw new InvalidOperationException("Không chạy được edge-tts. Kiểm tra PATH / cài đặt.");

                var outputTask = p.StandardOutput.ReadToEndAsync();
                var errorTask = p.StandardError.ReadToEndAsync();

                await p.WaitForExitAsync(token);
                var output = await outputTask;
                var err = await errorTask;

                if (p.ExitCode != 0)
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(err) ? output : err);
                }

                var bytes = await File.ReadAllBytesAsync(mp3Path, token);
                if (bytes.Length == 0)
                    throw new InvalidOperationException("No audio was received.");

                return bytes;
            }
            finally
            {
                TryDelete(mp3Path);
            }
        }

        private static string ResolveSupportedVoice(string? voice)
        {
            var value = (voice ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value))
                return DefaultVoice;

            foreach (var item in Voices)
            {
                if (string.Equals(item.Voice, value, StringComparison.OrdinalIgnoreCase))
                    return item.Voice;
            }

            return DefaultVoice;
        }

        private static string NormalizeText(string text)
        {
            var normalized = (text ?? "").Replace('\u00a0', ' ').Trim();
            while (normalized.Contains("  ", StringComparison.Ordinal))
                normalized = normalized.Replace("  ", " ");
            return normalized;
        }

        private static bool IsRetryableEdgeTtsError(Exception ex)
        {
            var message = ex.ToString();
            return message.Contains("NoAudioReceived", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("No audio was received", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("ClientConnectorError", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("Cannot connect", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("timed out", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFriendlyError(Exception? ex, string voice)
        {
            var raw = ex?.ToString() ?? "";

            if (raw.Contains("NoAudioReceived", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("No audio was received", StringComparison.OrdinalIgnoreCase))
            {
                return $"Edge TTS không trả audio cho giọng {voice}. Mình đã thử lại 3 lần; hãy bấm lưu lại hoặc đổi giọng khác.";
            }

            if (raw.Contains("Cannot connect", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("ClientConnectorError", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
            {
                return "Không kết nối được Edge TTS. Kiểm tra mạng hoặc quyền truy cập Internet của ứng dụng.";
            }

            var line = "";
            foreach (var part in raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (!trimmed.StartsWith("File ", StringComparison.OrdinalIgnoreCase) &&
                    !trimmed.StartsWith("Traceback", StringComparison.OrdinalIgnoreCase) &&
                    !trimmed.StartsWith("^", StringComparison.Ordinal))
                {
                    line = trimmed;
                }
            }

            if (line.Length > 180)
                line = line.Substring(0, 180) + "...";

            return string.IsNullOrWhiteSpace(line)
                ? "Edge TTS lỗi khi tạo audio."
                : "Edge TTS lỗi khi tạo audio: " + line;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
