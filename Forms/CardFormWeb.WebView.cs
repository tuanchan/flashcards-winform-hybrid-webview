#nullable enable

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Forms;
using TocflQuiz.Models.WebViews;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    public sealed partial class CardFormWeb
    {
        private void InitializeWebView()
        {
            _webView = new WebView2
            {
                Dock = DockStyle.Fill
            };

            Controls.Add(_webView);
            _webView.BringToFront();
            InitializeAsync();
        }

        private bool _initStarted;

        private async void InitializeAsync()
        {
            if (_initStarted) return;
            _initStarted = true;

            try
            {
                string userData = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FlashCards",
                    "WebView2");
                System.IO.Directory.CreateDirectory(userData);
                var env = await CoreWebView2Environment.CreateAsync(null, userData);
                await _webView.EnsureCoreWebView2Async(env);

                WebViewAssetService.ConfigureLocalContent(_webView);

                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                WebViewAssetService.NavigateToPage(_webView, CardFormHomeViewPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Lỗi khởi tạo WebView2:\n{ex.Message}\n\nCần cài WebView2 Runtime từ Microsoft.",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.WebMessageAsJson;
                var msg = JsonSerializer.Deserialize<StringWebActionMessage>(json);
                if (msg == null) return;

                switch (msg.Action)
                {
                    case "ready":
                        _isWebReady = true;
                        SendCoursesToWeb();
                        ExecuteScript($"if(window.setNotifyState) window.setNotifyState({(_toastSettings.Enabled ? "true" : "false")});");
                        BeginPrewarmFeatureViews();
                        break;

                    case "selectCourse":
                        HandleSelectCourse(msg.Data);
                        break;

                    case "showFeature":
                        HandleShowFeature(msg.Data);
                        break;

                    case "searchCourses":
                        HandleSearchCourses(msg.Data);
                        break;

                    case "getDashboardStats":
                        SendDashboardStatsToWeb();
                        break;

                    case "startQuiz":
                        if (_selectedSet == null)
                        {
                            SendAlert("Bạn chưa chọn học phần.");
                            return;
                        }

                        ShowQuizWinForms();
                        break;

                    case "showDialogue":
                        ShowDialogueWinForms();
                        break;

                    case "createCourse":
                        ShowCreateCourseWinForms();
                        break;

                    case "showNotifications":
                        HandleNotifications();
                        break;

                    case "toggleTheme":
                        HandleToggleTheme(msg.Data);
                        break;

                    case "getGeminiSettings":
                        SendGeminiSettingsToWeb();
                        break;

                    case "saveGeminiSettings":
                        HandleSaveGeminiSettings(msg.Data);
                        break;

                    case "getWritingOptions":
                        SendWritingOptionsToWeb();
                        break;

                    case "generateWritingPractice":
                        if (!EnsureApiKeysOrPrompt(requireGemini: true, requirePixabay: false))
                            return;
                        _ = GenerateWritingPracticeAsync(msg.Data);
                        break;

                    case "hintWritingPractice":
                        if (!EnsureApiKeysOrPrompt(requireGemini: true, requirePixabay: false))
                            return;
                        _ = GenerateWritingHintAsync(msg.Data);
                        break;

                    case "gradeWritingPractice":
                        if (!EnsureApiKeysOrPrompt(requireGemini: true, requirePixabay: false))
                            return;
                        _ = GradeWritingPracticeAsync(msg.Data);
                        break;

                    case "backToHome":
                        BackToWebHome();
                        break;

                    case "deleteCourse":
                        HandleDeleteCourse(msg.Data);
                        break;

                    case "updateCourse":
                        _ = HandleUpdateCourseAsync(msg.Data);
                        break;

                    case "pickCourseCoverImage":
                        HandlePickCourseCoverImage();
                        break;

                    case "refreshSelectedCourseCover":
                        RefreshSelectedCourseCoverIfNeeded();
                        break;

                    case "openExternalUrl":
                        HandleOpenExternalUrl(msg.Data);
                        break;

                    case "toggleFullScreen":
                        ToggleFullScreen();
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebMessage error: {ex.Message}");
            }
        }

        private void ExecuteScript(string script)
        {
            if (!_isWebReady) return;
            if (_webView?.CoreWebView2 == null) return;

            try { _webView.CoreWebView2.ExecuteScriptAsync(script); }
            catch { }
        }

        private void SendAlert(string message)
        {
            var escaped = WebViewAssetService.EscapeJavaScriptString(message);
            ExecuteScript($"if(window.__unifiedToast){{window.__unifiedToast('{escaped}','warn');}}else if(window.showToast){{window.showToast('{escaped}','warn');}}else{{alert('{escaped}');}}");
        }

        private bool EnsureApiKeysOrPrompt(bool requireGemini, bool requirePixabay)
        {
            var missingGemini = requireGemini && !SettingsService.HasGeminiApiKey();
            var missingPixabay = requirePixabay && !SettingsService.HasPixabayApiKey();
            if (!missingGemini && !missingPixabay)
                return true;

            PromptForApiKeysFromFeature();
            return false;
        }

        public void PromptForApiKeysFromFeature()
        {
            try
            {
                var result = MessageBox.Show(
                    "Hiện tại chưa có key Gemini hoặc Pixabay, hoặc key không chính xác.\n\nBạn có muốn mở cài đặt API để lấy/dán key không?",
                    "Cần API key",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question);

                if (result != DialogResult.OK)
                    return;

                BackToWebHome();
                BeginInvoke(new Action(() =>
                {
                    ExecuteScript("if(window.openAppSettings) window.openAppSettings();");
                }));
            }
            catch { }
        }

        private void SendGeminiSettingsToWeb()
        {
            var settings = SettingsService.GetGeminiSettings();
            var pixabay = SettingsService.GetPixabaySettings();
            var json = JsonSerializer.Serialize(new
            {
                apiKey = settings.ApiKey ?? "",
                model = string.IsNullOrWhiteSpace(settings.Model)
                    ? SettingsService.DefaultGeminiModel
                    : settings.Model,
                pixabayApiKey = pixabay.ApiKey ?? ""
            });

            ExecuteScript($"if(window.applyGeminiSettings) window.applyGeminiSettings({json});");
        }

        private void HandleSaveGeminiSettings(string data)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                var apiKey = root.TryGetProperty("apiKey", out var keyProp) ? keyProp.GetString() : "";
                var model = root.TryGetProperty("model", out var modelProp) ? modelProp.GetString() : "";
                var pixabayApiKey = root.TryGetProperty("pixabayApiKey", out var pixabayKeyProp) ? pixabayKeyProp.GetString() : "";
                SettingsService.SaveGeminiSettings(apiKey, model, pixabayApiKey);
            }
            catch { }
        }

        private void HandleOpenExternalUrl(string data)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var url = doc.RootElement.TryGetProperty("url", out var prop) ? prop.GetString() : "";
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
