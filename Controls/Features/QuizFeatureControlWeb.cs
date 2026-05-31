#nullable enable

using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using TocflQuiz.Controls.Features.Quiz;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class QuizFeatureControlWeb : UserControl
    {
        private const string QuizFeatureViewPath = "Webviews/quiz-feature.html";

        // ====== Public events ======
        public event Action? ExitToCourseListRequested;
        public event Action<CardSet, QuizConfig>? EssayModeRequested;

        // ====== state ======
        private readonly WebView2 _web = new();
        private CardSet? _set;
        private bool _isDarkMode;
        private QuizConfig _cfg = new();
        private List<QuizQuestion> _questions = new();
        private Dictionary<int, int?> _pickedIndexByQ = new();
        private HashSet<int> _dontKnow = new();
        private List<GeminiSentenceQuestion> _sentenceQuestions = new();
        private Dictionary<int, string> _sentenceAnswers = new();
        private Dictionary<int, GeminiEssayGradeItem> _sentenceGeminiGrades = new();
        private bool _sentenceUseGeminiGrades;
        private bool _submitted;
        private bool _srsApplied;
        private DateTime _startedAt;
        private TimeSpan _elapsed;

        private readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public QuizFeatureControlWeb()
        {
            Dock = DockStyle.Fill;

            Controls.Add(_web);
            _web.Dock = DockStyle.Fill;

            Load += async (_, __) => await InitAsync();
            Disposed += (_, __) =>
            {
                try
                {
                    _web.PreviewKeyDown -= Web_PreviewKeyDown;
                    _web.KeyDown -= Web_KeyDown;
                    _web.KeyUp -= Web_KeyUp;
                }
                catch { }

                try
                {
                    if (_web.CoreWebView2 != null)
                        _web.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                }
                catch { }

                try { _web.Dispose(); } catch { }
            };
        }

        public void BindSelectedSet(CardSet? set)
        {
            _set = set;

            Post("bindSet", new
            {
                title = _set?.Title ?? "(chưa chọn)",
                max = _set?.Items?.Count ?? 0,
                sourceLanguage = GetSourceLanguageLabel()
            });

            ResetQuizState();
            Post("resetToEmpty", new { });
            PostSetupDefaults();
        }

        public void SetDarkMode(bool isDark)
        {
            _isDarkMode = isDark;
            Post("theme", new { dark = isDark });
        }
    }
}
