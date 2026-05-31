#nullable enable

using System;
using System.Text.Json;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    public sealed partial class CardFormWeb
    {
        public void BackToWebHome()
        {
            CloseWinHostViewsKeepToast();
            RefreshSelectedCourseCoverIfNeeded();

            _webView.BringToFront();

            BeginInvoke(new Action(() =>
            {
                try
                {
                    ActiveControl = _webView;
                    _webView.Select();
                    _webView.Focus();
                }
                catch { }
            }));
        }

        private void HandleShowFeature(string data)
        {
            if (_selectedSet == null)
            {
                SendAlert("Bạn chưa chọn học phần.");
                return;
            }

            try
            {
                var doc = JsonDocument.Parse(data);
                if (!doc.RootElement.TryGetProperty("feature", out var featureProp))
                    return;

                var feature = featureProp.GetString() ?? "";

                switch (feature)
                {
                    case "flashcards":
                        ShowFlashcardsWinForms();
                        break;

                    case "quiz":
                        ShowQuizWinForms();
                        break;

                    case "course":
                    case "blocks":
                    case "blast":
                    case "merge":
                    default:
                        ExecuteScript(
                            $"if(window.showFeatureStub) window.showFeatureStub('{WebViewAssetService.EscapeJavaScriptString(feature)}', '{WebViewAssetService.EscapeJavaScriptString(_selectedSet?.Title)}');");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Show feature error: {ex.Message}");
            }
        }
    }
}
