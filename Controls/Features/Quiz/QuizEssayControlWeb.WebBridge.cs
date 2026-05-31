#nullable enable

using Microsoft.Web.WebView2.Core;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TocflQuiz.Controls.Features.Quiz
{
    public sealed partial class QuizEssayControlWeb
    {
        private bool _initStarted;

        private async Task EnsureWebAsync()
        {
            if (_initStarted) return;
            _initStarted = true;

            try
            {
                string userData = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    "TocflQuiz",
                    "WebView2");
                System.IO.Directory.CreateDirectory(userData);
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

                _web.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                _web.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                Services.WebViewAssetService.ConfigureLocalContent(_web);
                Services.WebViewAssetService.NavigateToPage(_web, QuizEssayViewPath);

                _web.CoreWebView2.NavigationCompleted += async (_, __) =>
                {
                    _webReady = true;
                    await PushThemeAsync();

                    if (_questions.Count > 0) await RenderQuestionAsync(_currentIndex);
                    else await PushEmptyStateAsync(_dayTitle ?? "", "(Chọn học phần và bấm Bắt đầu)");
                };
            }
            catch
            {
                Controls.Clear();
                Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = "WebView2 chưa sẵn sàng. Hãy cài WebView2 Runtime + NuGet Microsoft.Web.WebView2.",
                    Font = new Font("Segoe UI", 12f, FontStyle.Bold)
                });
            }
        }

        private void Web_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
        {
            e.IsInputKey = true;
        }

        private void Web_KeyDown(object? sender, KeyEventArgs e)
        {
            try
            {
                var f = FindForm();
                if (f == null) return;

                if (!Visible || f.WindowState == FormWindowState.Minimized || !ContainsFocus)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
            catch { }
        }

        private void Web_KeyUp(object? sender, KeyEventArgs e)
        {
            try
            {
                var f = FindForm();
                if (f == null) return;

                if (!Visible || f.WindowState == FormWindowState.Minimized || !ContainsFocus)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
            catch { }
        }

        private void CoreWebView2_AcceleratorKeyPressed(object? sender, CoreWebView2AcceleratorKeyPressedEventArgs e)
        {
            try
            {
                var f = FindForm();
                if (f == null) return;

                if (!Visible || f.WindowState == FormWindowState.Minimized || !ContainsFocus)
                {
                    e.Handled = true;
                }
            }
            catch
            {
                // ignore
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.WebMessageAsJson;
                var msg = System.Text.Json.JsonSerializer.Deserialize<UiToHostMessage>(json, JsonOpt());
                if (msg == null) return;

                switch (msg.Type)
                {
                    case "ready":
                        _webReady = true;
                        _ = PushThemeAsync();
                        if (_questions.Count > 0) _ = RenderQuestionAsync(_currentIndex);
                        else _ = PushEmptyStateAsync(_dayTitle ?? "", "(Chọn học phần và bấm Bắt đầu)");
                        break;

                    case "skip":
                        SkipCurrent();
                        break;

                    case "previous":
                        PreviousQuestion(msg.Text);
                        break;

                    case "submit":
                        SubmitCurrent(msg.Text ?? "");
                        break;

                    case "gradeLocal":
                        if (_awaitingGradeChoice)
                            _ = FinishWithLocalGradingAsync();
                        break;

                    case "gradeGemini":
                        if (_awaitingGradeChoice)
                            _ = FinishWithGeminiGradingAsync();
                        break;

                    case "exit":
                        ExitRequested?.Invoke();
                        break;

                    case "viewResult":
                        _ = ShowReviewAsync(0);
                        break;

                    case "reviewPrev":
                        _ = ShowReviewAsync(_reviewIndex - 1);
                        break;

                    case "reviewNext":
                        _ = ShowReviewAsync(_reviewIndex + 1);
                        break;

                    case "closeOverlayAndExit":
                        ExitRequested?.Invoke();
                        break;
                }
            }
            catch
            {
                // ignore malformed messages
            }
        }

        private async Task PostToWebAsync(object payload)
        {
            if (!_webReady || _web.CoreWebView2 == null) return;

            var json = System.Text.Json.JsonSerializer.Serialize(payload, JsonOpt());
            var script = $"window.__hostReceive({System.Text.Json.JsonSerializer.Serialize(json)});";
            try { await _web.ExecuteScriptAsync(script); } catch { }
        }

        private Task PushThemeAsync()
        {
            var payload = new HostToUiMessage
            {
                Type = "theme",
                Theme = new UiTheme
                {
                    IsDark = _isDarkMode,
                    TcFont = TcPrimaryFontName
                }
            };

            return PostToWebAsync(payload);
        }

        private void ShowEmptyState(string? message = null)
        {
            _ = PushEmptyStateAsync(_dayTitle ?? "", message ?? "(Chọn học phần và bấm Bắt đầu)");
        }

        private Task PushEmptyStateAsync(string dayTitle, string message)
        {
            var dto = new HostToUiMessage
            {
                Type = "empty",
                Theme = new UiTheme { IsDark = _isDarkMode, TcFont = TcPrimaryFontName },
                Empty = new UiEmptyState
                {
                    DayTitle = dayTitle,
                    Message = message
                }
            };

            return PostToWebAsync(dto);
        }
    }
}
