#nullable enable

using System;
using System.IO;
using System.Text.Json;
using TocflQuiz.Models;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class FlashcardsFeatureControlWeb
    {
        private void ToggleSettingsOverlay()
        {
            ExecuteScript("toggleSettings();");
        }

        private void HideSettingsOverlay()
        {
            ExecuteScript("hideSettings();");
        }

        private void SaveSettings(JsonElement root)
        {
            try
            {
                if (root.TryGetProperty("progressTracking", out var pt))
                    SetProgressTracking(pt.GetBoolean());

                if (root.TryGetProperty("starredOnly", out var so))
                {
                    _starredOnly = so.GetBoolean();
                    RebuildOrder(true);
                }

                _ttsEnabled = false;
                _autoPronounce = false;

                if (root.TryGetProperty("frontSide", out var fs))
                {
                    var idx = fs.GetInt32();
                    if (idx >= 0 && idx <= 2)
                    {
                        _frontSide = (FrontSideOption)idx;
                        ShowCard();
                    }
                }

                if (root.TryGetProperty("cardZoom", out var cz))
                {
                    _cardZoomPercent = ClampCardZoom(cz.GetInt32());
                }

                if (root.TryGetProperty("cardWidth", out var cw))
                {
                    _cardWidthPercent = ClampCardDimension(cw.GetInt32());
                }

                if (root.TryGetProperty("cardHeight", out var ch))
                {
                    _cardHeightPercent = ClampCardDimension(ch.GetInt32());
                }

                if (root.TryGetProperty("cardCustomSize", out var customSize))
                {
                    _cardCustomSize = customSize.GetBoolean();
                }

                SavePersistedFlashcardSettings();
                _ = PushStateAsync();
                HideSettingsOverlay();
            }
            catch { }
        }

        private void LoadPersistedFlashcardSettings()
        {
            try
            {
                var path = GetFlashcardSettingsPath();
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<FlashcardUiSettings>(json);
                if (settings == null) return;

                _cardZoomPercent = ClampCardZoom(settings.CardZoom);
                _cardWidthPercent = ClampCardDimension(settings.CardWidth);
                _cardHeightPercent = ClampCardDimension(settings.CardHeight);
                _cardCustomSize = settings.CardCustomSize;
            }
            catch { }
        }

        private void SavePersistedFlashcardSettings()
        {
            try
            {
                var path = GetFlashcardSettingsPath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

                var settings = new FlashcardUiSettings
                {
                    CardZoom = _cardZoomPercent,
                    CardWidth = _cardWidthPercent,
                    CardHeight = _cardHeightPercent,
                    CardCustomSize = _cardCustomSize
                };

                File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            }
            catch { }
        }

        private static string GetFlashcardSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TocflQuiz",
                "flashcards-settings.json");
        }

        private static int ClampCardZoom(int value)
        {
            return Math.Min(140, Math.Max(80, value));
        }

        private static int ClampCardDimension(int value)
        {
            return Math.Min(120, Math.Max(70, value <= 0 ? 100 : value));
        }

        private sealed class FlashcardUiSettings
        {
            public int CardZoom { get; set; } = 100;
            public int CardWidth { get; set; } = 100;
            public int CardHeight { get; set; } = 100;
            public bool CardCustomSize { get; set; }
        }

        private string GetFrontText(CardItem item)
        {
            return _frontSide switch
            {
                FrontSideOption.Definition => item.Definition ?? "",
                FrontSideOption.Pinyin => item.Pinyin ?? "",
                _ => item.Term ?? ""
            };
        }

        private string GetBackText(CardItem item)
        {
            return _frontSide switch
            {
                FrontSideOption.Definition => item.Term ?? "",
                FrontSideOption.Pinyin => item.Definition ?? "",
                _ => item.Definition ?? ""
            };
        }

        private string GetSubText(CardItem item)
        {
            return _frontSide == FrontSideOption.Pinyin ? (item.Term ?? "") : (item.Pinyin ?? "");
        }
    }
}
