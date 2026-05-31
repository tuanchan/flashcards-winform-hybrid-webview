// WebCardImportControl.cs
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using TocflQuiz.Forms;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Controls
{
    public sealed class WebCardImportControl : UserControl
    {
        private const string CardImportViewPath = "Webviews/card-import.html";

        private readonly WebView2 _wv = new WebView2();
        private bool _ready;
        private bool _darkWanted = false;
        private bool _pendingApplyTheme = false;

        public event EventHandler? ImportCompleted;
        public DialogResult DialogResult { get; private set; } = DialogResult.Cancel;
        public void SetDarkMode(bool dark)
        {
            _darkWanted = dark;
            BackColor = dark ? Color.FromArgb(30, 30, 40) : Color.FromArgb(246, 247, 251);
            _wv.BackColor = BackColor;

            // Nếu web chưa ready thì đánh dấu, ready rồi mới apply
            if (!_ready || _wv.CoreWebView2 == null)
            {
                _pendingApplyTheme = true;
                return;
            }

            ApplyThemeToWeb();
        }
        private void ApplyThemeToWeb()
        {
            try
            {
                // dùng window.setDarkMode nếu có, fallback toggle class
                var js = $"(function(){{" +
                         $" if(window.setDarkMode) window.setDarkMode({(_darkWanted ? "true" : "false")});" +
                         $" else document.body.classList.toggle('dark-mode', {(_darkWanted ? "true" : "false")});" +
                         $"}})();";

                _wv.CoreWebView2.ExecuteScriptAsync(js);
            }
            catch { }
        }

        public WebCardImportControl()
        {
            Dock = DockStyle.Fill;
            TabStop = true;
            BackColor = Color.FromArgb(246, 247, 251);

            _wv.Dock = DockStyle.Fill;
            _wv.TabStop = true;
            _wv.BackColor = BackColor;

            Controls.Add(_wv);

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Enter += (_, __) => FocusEditor();
            HandleCreated += async (_, __) => await EnsureInitAsync();
        }

        public void FocusEditor()
        {
            if (IsDisposed) return;

            try
            {
                _wv.Select();
                _wv.Focus();
            }
            catch { }
        }

        private async Task EnsureInitAsync()
        {
            if (_ready) return;

            try
            {
                string userData = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TocflQuiz",
                    "WebView2");
                System.IO.Directory.CreateDirectory(userData);
                var env = await CoreWebView2Environment.CreateAsync(null, userData);
                await _wv.EnsureCoreWebView2Async(env);

                _wv.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _wv.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _wv.CoreWebView2.Settings.IsZoomControlEnabled = true;
                _wv.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;

                WebViewAssetService.ConfigureLocalContent(_wv);

                _wv.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                _wv.NavigationCompleted += (_, __) =>
                {
                    try
                    {
                        _wv.BringToFront();
                        if (Visible && IsHandleCreated)
                            BeginInvoke(new Action(FocusEditor));
                    }
                    catch { }
                };

                // ✅ CHỐNG CHỚP: inject theme từ lúc document vừa tạo (trước khi render)
                try
                {
                    var js =
                        "(function(){" +
                        $" const dark = {(_darkWanted ? "true" : "false")};" +
                        " document.documentElement.style.backgroundColor = dark ? '#0b0f1a' : '#f6f7fb';" +
                        " document.addEventListener('DOMContentLoaded', function(){" +
                        "   try{" +
                        "     if(window.setDarkMode) window.setDarkMode(dark);" +
                        "     else document.body.classList.toggle('dark-mode', dark);" +
                        "   }catch(e){}" +
                        " });" +
                        "})();";

                    await _wv.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(js);
                }
                catch { }

                // Load HTML
                WebViewAssetService.NavigateToPage(_wv, CardImportViewPath);

                await Task.Delay(60);
                _ready = true;

                // Nếu trước đó có SetDarkMode khi chưa ready -> apply lại
                if (_pendingApplyTheme)
                {
                    _pendingApplyTheme = false;
                    ApplyThemeToWeb();
                }
                else
                {
                    ApplyThemeToWeb();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.TryGetWebMessageAsString();
                if (string.IsNullOrWhiteSpace(json)) return;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : "";

                switch (type)
                {
                    case "save":
                        _ = HandleSaveAsync(root.Clone());
                        break;

                    case "close":
                        HandleClose(root);
                        break;

                    case "pickCoverImage":
                        HandlePickCoverImage();
                        break;
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Error: {ex.Message}", "error");
            }
        }

        private void ShowToast(string message, string type = "warn")
        {
            try
            {
                if (_wv.CoreWebView2 == null) return;

                var escapedMessage = WebViewAssetService.EscapeJavaScriptString(message);
                var escapedType = WebViewAssetService.EscapeJavaScriptString(type);
                _wv.CoreWebView2.ExecuteScriptAsync(
                    $"if(window.__unifiedToast){{window.__unifiedToast('{escapedMessage}','{escapedType}');}}else{{console.warn('{escapedMessage}');}}");
            }
            catch { }
        }

        private async Task HandleSaveAsync(JsonElement root)
        {
            try
            {
                var autoGenerate = root.TryGetProperty("autoGenerateExamples", out var autoProp) && autoProp.GetBoolean();
                if (autoGenerate && (!SettingsService.HasGeminiApiKey() || !SettingsService.HasPixabayApiKey()))
                {
                    PromptForApiKeys();
                    UncheckAutoGemini();
                    return;
                }

                await CardImportSubmissionService.SaveFromWebPayloadAsync(root);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                if (SettingsService.IsLikelyApiKeyError(ex.Message))
                {
                    PromptForApiKeys();
                    UncheckAutoGemini();
                    return;
                }

                ShowToast($"Save failed: {ex.Message}", "error");
            }
        }

        private void PromptForApiKeys()
        {
            if (FindForm() is CardFormWeb form)
                form.PromptForApiKeysFromFeature();
        }

        private void UncheckAutoGemini()
        {
            try
            {
                _wv.CoreWebView2?.ExecuteScriptAsync(
                    "const chk=document.getElementById('chkAutoGemini'); if(chk){chk.checked=false; chk.dispatchEvent(new Event('change'));}");
            }
            catch { }
        }

        private void HandleClose(JsonElement root)
        {
            var result = root.TryGetProperty("dialogResult", out var dr) && dr.GetString() == "OK"
                ? DialogResult.OK
                : DialogResult.Cancel;

            DialogResult = result;
            ImportCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void HandlePickCoverImage()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Chọn ảnh nền học phần",
                Filter = "Image files|*.jpg;*.jpeg;*.png;*.webp;*.bmp|All files|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                return;

            var path = WebViewAssetService.EscapeJavaScriptString(dialog.FileName);
            _wv.CoreWebView2?.ExecuteScriptAsync(
                $"if(window.handleCoverImagePicked) window.handleCoverImagePicked('{path}');");
        }
    }
}
