using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    

    

    public static class SettingsService
    {
        public const string DefaultGeminiModel = "gemini-2.0-flash-lite";

        private static string SettingsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlashCards",
                "settings.json"
            );

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var cfg = JsonSerializer.Deserialize<AppSettings>(json);
                    cfg ??= new AppSettings();
                    NormalizeSettings(cfg);
                    return cfg;
                }
            }
            catch
            {
            }

            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions { WriteIndented = true }
            );

            File.WriteAllText(SettingsPath, json);
        }

        public static GeminiAppSettings GetGeminiSettings()
        {
            var settings = Load();
            NormalizeSettings(settings);
            return settings.Gemini;
        }

        public static PixabayAppSettings GetPixabaySettings()
        {
            var settings = Load();
            NormalizeSettings(settings);
            return settings.Pixabay;
        }

        public static bool HasGeminiApiKey()
            => !string.IsNullOrWhiteSpace(GetGeminiSettings().ApiKey);

        public static bool HasPixabayApiKey()
            => !string.IsNullOrWhiteSpace(GetPixabaySettings().ApiKey);

        public static bool IsLikelyApiKeyError(string? message)
        {
            var text = (message ?? "").ToLowerInvariant();
            return text.Contains("api key") ||
                   text.Contains("apikey") ||
                   text.Contains("api_key") ||
                   text.Contains("key not valid") ||
                   text.Contains("invalid key") ||
                   text.Contains("invalid_api_key") ||
                   text.Contains("permission_denied") ||
                   text.Contains("unauthorized") ||
                   text.Contains("forbidden") ||
                   text.Contains("401") ||
                   text.Contains("403") ||
                   text.Contains("chưa cấu hình gemini") ||
                   text.Contains("pixabay api key");
        }

        public static void SaveGeminiSettings(string? apiKey, string? model, string? pixabayApiKey = null)
        {
            var settings = Load();
            NormalizeSettings(settings);

            settings.Gemini.ApiKey = (apiKey ?? "").Trim();
            settings.Gemini.Model = string.IsNullOrWhiteSpace(model)
                ? DefaultGeminiModel
                : model.Trim();

            if (pixabayApiKey != null)
                settings.Pixabay.ApiKey = pixabayApiKey.Trim();

            Save(settings);
        }

        private static void NormalizeSettings(AppSettings settings)
        {
            settings.Gemini ??= new GeminiAppSettings();
            settings.Pixabay ??= new PixabayAppSettings();

            if (string.IsNullOrWhiteSpace(settings.Gemini.Model))
                settings.Gemini.Model = DefaultGeminiModel;

            settings.QuizPreferences ??= new Dictionary<string, QuizPreference>();
        }

        public static string BuildQuizPreferenceKey(string? setId, string? folderName, string? title)
        {
            if (!string.IsNullOrWhiteSpace(setId))
                return $"set:{setId.Trim()}";

            if (!string.IsNullOrWhiteSpace(folderName))
                return $"folder:{folderName.Trim()}";

            return $"title:{(title ?? "").Trim()}";
        }

        public static QuizPreference GetQuizPreference(string? setId, string? folderName, string? title)
        {
            var settings = Load();
            var key = BuildQuizPreferenceKey(setId, folderName, title);

            if (settings.QuizPreferences.TryGetValue(key, out var pref) && pref != null)
                return pref;

            return new QuizPreference();
        }

        public static void SaveQuizPreference(
            string? setId,
            string? folderName,
            string? title,
            QuizPreference preference)
        {
            var settings = Load();
            var key = BuildQuizPreferenceKey(setId, folderName, title);
            settings.QuizPreferences[key] = preference ?? new QuizPreference();
            Save(settings);
        }
    }
}
