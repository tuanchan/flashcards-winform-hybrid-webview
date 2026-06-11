#nullable enable

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Diagnostics;
using System.IO;
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
                        SendTopicsToWeb();
                        SendCoursesToWeb();
                        ExecuteScript($"if(window.setNotifyState) window.setNotifyState({(_toastSettings.Enabled ? "true" : "false")});");
                        BeginPrewarmFeatureViews();
                        break;

                    case "getTopics":
                        SendTopicsToWeb();
                        break;

                    case "createTopic":
                        HandleCreateTopic(msg.Data);
                        break;

                    case "updateTopic":
                        HandleUpdateTopic(msg.Data);
                        break;

                    case "deleteTopic":
                        HandleDeleteTopic(msg.Data);
                        break;

                    case "pickTopicCoverImage":
                        HandlePickTopicCoverImage();
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
                        {
                            string? topicId = null;
                            if (!string.IsNullOrWhiteSpace(msg.Data))
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(msg.Data);
                                    if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("topicId", out var tIdProp))
                                    {
                                        topicId = tIdProp.GetString();
                                    }
                                }
                                catch { }
                            }
                            ShowCreateCourseWinForms(topicId);
                        }
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

                    case "exportDatasetBackup":
                        HandleExportDatasetBackup();
                        break;

                    case "importDatasetBackup":
                        HandleImportDatasetBackup();
                        break;

                    case "importDatasetFolder":
                        HandleImportDatasetFolder();
                        break;

                    case "openDatasetFolder":
                        HandleOpenDatasetFolder();
                        break;

                    case "shareDatasetBackup":
                        HandleShareDatasetBackup(msg.Data);
                        break;

                    case "getWritingOptions":
                        SendWritingOptionsToWeb();
                        break;

                    case "getSpeakingOptions":
                        SendSpeakingOptionsToWeb();
                        break;

                    case "generateSpeakingPractice":
                        if (!EnsureApiKeysOrPrompt(requireGemini: true, requirePixabay: false))
                            return;
                        _ = GenerateSpeakingPracticeAsync(msg.Data);
                        break;

                    case "synthesizeSpeakingLine":
                        _ = SynthesizeSpeakingLineAsync(msg.Data);
                        break;

                    case "prepareSpeakingAudio":
                        _ = PrepareSpeakingAudioAsync(msg.Data);
                        break;

                    case "cancelSpeakingAudio":
                        CancelSpeakingAudioPreparation();
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

        private void HandleExportDatasetBackup()
        {
            var result = DatasetBackupService.ExportDataset();
            SendDatasetBackupResultToWeb("export", result);
        }

        private void HandleImportDatasetBackup()
        {
            try
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "Chon file Dataset.zip",
                    Filter = "Dataset backup (*.zip)|*.zip|All files (*.*)|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    SendDatasetBackupResultToWeb("import", new DatasetBackupResult
                    {
                        Success = false,
                        Message = "Da huy import dataset."
                    });
                    return;
                }

                var result = DatasetBackupService.ImportDataset(dialog.FileName);
                RefreshCoursesAfterDatasetImport(result);

                SendDatasetBackupResultToWeb("import", result);
            }
            catch (Exception ex)
            {
                SendDatasetBackupResultToWeb("import", new DatasetBackupResult
                {
                    Success = false,
                    Message = "Khong import duoc dataset: " + ex.Message
                });
            }
        }

        private void HandleImportDatasetFolder()
        {
            try
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = "Chon thu muc Dataset can import",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = false
                };

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    SendDatasetBackupResultToWeb("import", new DatasetBackupResult
                    {
                        Success = false,
                        Message = "Da huy import dataset."
                    });
                    return;
                }

                var result = DatasetBackupService.ImportDatasetFolder(dialog.SelectedPath);
                RefreshCoursesAfterDatasetImport(result);
                SendDatasetBackupResultToWeb("import", result);
            }
            catch (Exception ex)
            {
                SendDatasetBackupResultToWeb("import", new DatasetBackupResult
                {
                    Success = false,
                    Message = "Khong import duoc dataset: " + ex.Message
                });
            }
        }

        private void RefreshCoursesAfterDatasetImport(DatasetBackupResult result)
        {
            if (!result.Success)
                return;

            DatasetBackupService.NormalizeDialogueFolders();
            DatasetBackupService.EnsureUniqueCourseTitles();
            LoadAllSets();
            if (_selectedSet != null)
            {
                _selectedSet = _allSets.Find(s =>
                    string.Equals(s.Id, _selectedSet.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.FolderName, _selectedSet.FolderName, StringComparison.OrdinalIgnoreCase));
                _toastScheduler?.NotifySelectedSetChanged();
            }

            SendCoursesToWeb();
            SendDashboardStatsToWeb();
        }

        private void SendDatasetBackupResultToWeb(string operation, DatasetBackupResult result)
        {
            var payload = JsonSerializer.Serialize(new
            {
                operation,
                success = result.Success,
                message = result.Message,
                filePath = result.FilePath,
                folderPath = result.FolderPath
            });

            ExecuteScript($"if(window.applyDatasetBackupResult) window.applyDatasetBackupResult({payload});");
        }

        private void HandleShareDatasetBackup(string data)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                var platform = root.TryGetProperty("platform", out var platformProp)
                    ? platformProp.GetString() ?? ""
                    : "";
                var filePath = root.TryGetProperty("filePath", out var fileProp)
                    ? fileProp.GetString() ?? ""
                    : "";

                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                    Clipboard.SetText(filePath);

                OpenFileInExplorer(filePath);
            }
            catch { }
        }

        private void HandleOpenDatasetFolder()
        {
            try
            {
                var folder = CardSetStorage.EnsureDir();
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private static void OpenFileInExplorer(string filePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{filePath}\"",
                        UseShellExecute = true
                    });
                    return;
                }

                var folder = DatasetBackupService.BackupFolderPath;
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
