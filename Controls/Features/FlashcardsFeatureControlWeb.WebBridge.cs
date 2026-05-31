#nullable enable

using Microsoft.Web.WebView2.Core;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using TocflQuiz.Forms;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class FlashcardsFeatureControlWeb
    {
        private bool _initStarted;

        private async Task InitializeAsync()
        {
            if (_initStarted) return;
            _initStarted = true;

            try
            {
                string userData = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TocflQuiz",
                    "WebView2");
                System.IO.Directory.CreateDirectory(userData);
                var env = await CoreWebView2Environment.CreateAsync(null, userData);
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                WebViewAssetService.ConfigureLocalContent(_webView);

                _ready = true;
                WebViewAssetService.NavigateToPage(_webView, FlashcardsViewPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 initialization failed: {ex.Message}", "Error");
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.TryGetWebMessageAsString();
                if (string.IsNullOrWhiteSpace(json)) return;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var action = root.TryGetProperty("action", out var a) ? a.GetString() : "";

                switch (action)
                {
                    case "exit":
                        if (ParentForm is CardFormWeb form) {
                            form.BackToWebHome();
                        }
                        break;

                    case "prev":
                        HandlePrevAction();
                        break;

                    case "next":
                        HandleNextAction();
                        break;

                    case "shuffle":
                        ToggleShuffle();
                        break;

                    case "settings":
                        ToggleSettingsOverlay();
                        break;

                    case "toggleProgress":
                        var enabled = root.TryGetProperty("enabled", out var e1) && e1.GetBoolean();
                        SetProgressTracking(enabled);
                        break;

                    case "star":
                        ToggleStar();
                        break;

                    case "edit":
                        OpenEditModalForCurrentCard();
                        break;

                    case "saveEdit":
                        SaveEditFromWeb(root);
                        break;

                    case "sound":
                        _ = PlaySoundAsync();
                        break;

                    case "geminiExamples":
                        _ = ShowGeminiExamplesAsync(false);
                        break;

                    case "regenerateGeminiExamples":
                        _ = ShowGeminiExamplesAsync(true);
                        break;

                    case "generateGeminiImageOnly":
                        _ = ShowGeminiImageOnlyAsync();
                        break;

                    case "flip":
                        _ = HandleAutoPronounceFlipAsync();
                        break;

                    case "reviewLearning":
                        ReviewLearning();
                        break;

                    case "resetProgress":
                        ResetProgress();
                        break;

                    case "dismissCompletion":
                        HideCompletionOverlay();
                        break;

                    case "saveSettings":
                        SaveSettings(root);
                        break;

                    case "closeSettings":
                        HideSettingsOverlay();
                        break;
                }
            }
            catch { }
        }

        private async Task PushStateAsync()
        {
            if (!_ready) return;

            var state = new
            {
                hasSet = _set != null && _set.Items != null && _set.Items.Count > 0,
                hasCards = _order.Count > 0,
                index = _index + 1,
                total = _order.Count,
                front = _order.Count > 0 && _set?.Items != null ? GetFrontText(_set.Items[_order[_index]]) : "",
                back = _order.Count > 0 && _set?.Items != null ? GetBackText(_set.Items[_order[_index]]) : "",
                sub = _order.Count > 0 && _set?.Items != null ? GetSubText(_set.Items[_order[_index]]) : "",
                starred = _order.Count > 0 && _set?.Items != null && _set.Items[_order[_index]].IsStarred,
                progressTracking = _progressTracking,
                shuffleEnabled = _shuffleEnabled,
                dark = _isDarkMode,
                canPrev = _progressTracking ? _order.Count > 0 : _index > 0,
                canNext = _progressTracking ? _order.Count > 0 : _index < _order.Count - 1,
                showCompletion = _completionShown,
                completion = GetCompletionData(),
                srs = _order.Count > 0 && _set?.Items != null
                    ? SpacedRepetitionService.BuildStatus(_set.Items[_order[_index]])
                    : SpacedRepetitionService.BuildStatus(null),
                settings = new
                {
                    progressTracking = _progressTracking,
                    starredOnly = _starredOnly,
                    ttsEnabled = _ttsEnabled,
                    autoPronounce = _autoPronounce,
                    frontSide = (int)_frontSide,
                    cardZoom = _cardZoomPercent,
                    languageCode = _set?.LanguageCode ?? "",
                    sourceLanguage = string.IsNullOrWhiteSpace(_set?.Language) ? "Ngôn ngữ gốc" : _set!.Language,
                    frontSideOptions = new[]
                    {
                        new { value = 0, text = string.IsNullOrWhiteSpace(_set?.Language) ? "Ngôn ngữ gốc" : _set!.Language },
                        new { value = 1, text = "Tiếng Việt" },
                        new { value = 2, text = "Phiên âm" }
                    }
                }
            };

            var json = JsonSerializer.Serialize(state);
            await ExecAsync($"if(window.updateState) window.updateState({json});");
        }

        private async Task ExecAsync(string js)
        {
            try
            {
                if (!_ready || _webView.CoreWebView2 == null) return;
                await _webView.ExecuteScriptAsync(js);
            }
            catch { }
        }

        private void ExecuteScript(string js)
        {
            _ = ExecAsync(js);
        }
    }
}
