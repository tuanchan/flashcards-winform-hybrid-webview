#nullable enable

using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TocflQuiz.Models.WebViews;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class QuizFeatureControlWeb
    {
        private bool _initStarted;

        private async Task InitAsync()
        {
            if (_initStarted) return;
            _initStarted = true;

            string userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TocflQuiz",
                "WebView2");

            Directory.CreateDirectory(userData);

            var env = await CoreWebView2Environment.CreateAsync(null, userData);
            await _web.EnsureCoreWebView2Async(env);

            try
            {
                _web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            }
            catch { }

            _web.PreviewKeyDown -= Web_PreviewKeyDown;
            _web.PreviewKeyDown += Web_PreviewKeyDown;

            _web.KeyDown -= Web_KeyDown;
            _web.KeyDown += Web_KeyDown;

            _web.KeyUp -= Web_KeyUp;
            _web.KeyUp += Web_KeyUp;

            _web.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            _web.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            WebViewAssetService.ConfigureLocalContent(_web);
            WebViewAssetService.NavigateToPage(_web, QuizFeatureViewPath);
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json;
                try { json = e.TryGetWebMessageAsString(); }
                catch { json = e.WebMessageAsJson; }
                
                if (string.IsNullOrWhiteSpace(json)) json = e.WebMessageAsJson;

                var msg = JsonSerializer.Deserialize<JsonWebActionMessage>(json, _json);
                if (msg?.Action == null) return;

                switch (msg.Action)
                {
                    case "ready":
                        Post("init", new
                        {
                            dark = _isDarkMode,
                            title = _set?.Title ?? "(chưa chọn)",
                            max = _set?.Items?.Count ?? 0
                        });
                        PostSetupDefaults();
                        Post("resetToEmpty", new { });
                        break;

                    case "goHome":
                        ExitToCourseListRequested?.Invoke();
                        break;

                    case "closeSetup":
                        ExitToCourseListRequested?.Invoke();
                        break;

                    case "openSetup":
                        PostSetupDefaults();
                        break;

                    case "startFromSetup":
                        HandleStartFromSetup(msg.Data);
                        break;

                    case "pick":
                        HandlePick(msg.Data);
                        break;

                    case "dontKnow":
                        HandleDontKnow(msg.Data);
                        break;

                    case "sentenceAnswer":
                        HandleSentenceAnswer(msg.Data);
                        break;

                    case "submit":
                        HandleSubmit();
                        break;

                    case "sentenceGradeLocal":
                        if (_cfg.EnableSentenceWriting)
                            FinishSentenceWithLocalGrading();
                        break;

                    case "sentenceGradeGemini":
                        if (_cfg.EnableSentenceWriting)
                            _ = FinishSentenceWithGeminiGradingAsync();
                        break;

                    case "viewResult":
                        if (_cfg.EnableSentenceWriting)
                            SendSentenceReviewStateToWeb();
                        else
                            SendReviewStateToWeb();
                        Post("setFooterMode", new { mode = "goHome" });
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebMessageReceived error: {ex}");
                try
                {
                    Post("toast", new { type = "error", text = "Lỗi xử lý yêu cầu kiểm tra." });
                }
                catch { }
            }
        }

        private void Post(string action, object data)
        {
            try
            {
                var json = JsonSerializer.Serialize(new { action, data }, _json);
                _web.CoreWebView2?.PostWebMessageAsJson(json);
            }
            catch { }
        }

        private static int TryGetInt(JsonElement e, string name, int fallback)
        {
            try
            {
                if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p))
                {
                    if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v)) return v;
                    if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var s)) return s;
                }
            }
            catch { }

            return fallback;
        }

        private static bool TryGetBool(JsonElement e, string name, bool fallback)
        {
            try
            {
                if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p))
                {
                    if (p.ValueKind == JsonValueKind.True) return true;
                    if (p.ValueKind == JsonValueKind.False) return false;
                    if (p.ValueKind == JsonValueKind.String && bool.TryParse(p.GetString(), out var b)) return b;
                }
            }
            catch { }

            return fallback;
        }
    }
}
