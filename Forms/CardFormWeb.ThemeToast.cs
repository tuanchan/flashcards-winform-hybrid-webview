#nullable enable

using System;
using System.Text.Json;

namespace TocflQuiz.Forms
{
    public sealed partial class CardFormWeb
    {
        private void HandleToggleTheme(string data)
        {
            try
            {
                var doc = JsonDocument.Parse(data);
                if (doc.RootElement.TryGetProperty("dark", out var darkProp))
                    _isDarkMode = darkProp.GetBoolean();
            }
            catch { }
        }

        private void HandleNotifications()
        {
            try
            {
                using var dlg = new VocabToastSettingsForm(_toastSettings);
                if (dlg.ShowDialog(this) != System.Windows.Forms.DialogResult.OK) return;

                _toastSettings = dlg.Result.Clone();
                _toastScheduler?.ApplySettings(_toastSettings);
                _toastScheduler?.Restart();
                _toastScheduler?.ShowOneNow();

                ExecuteScript($"if(window.setNotifyState) window.setNotifyState({(_toastSettings.Enabled ? "true" : "false")});");
            }
            catch (Exception ex)
            {
                SendAlert($"Lỗi mở Nhắc từ: {ex.Message}");
            }
        }
    }
}
