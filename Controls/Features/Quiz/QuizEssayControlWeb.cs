#nullable enable

using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features.Quiz
{
    /// <summary>
    /// Hybrid control: C# giữ nguyên logic QuizEssayControl, UI bằng HTML/JS/CSS chạy trong WebView2.
    /// </summary>
    public sealed partial class QuizEssayControlWeb : UserControl
    {
        private const string QuizEssayViewPath = "Webviews/quiz-essay.html";

        // ========= Public events =========
        public event Action? ExitRequested;
        public event Action<int, int>? ProgressChanged;

        // ========= Theme =========
        private bool _isDarkMode;

        // ========= State =========
        private CardSet? _set;
        private AnswerMode _mode;
        private string? _dayTitle;
        private readonly Random _rng = new();

        private readonly List<QuizQuestion> _questions = new();
        private readonly List<QuizAnswerState> _states = new();
        private int _currentIndex;
        private DateTime _startedAt;
        private bool _submitted;
        private bool _awaitingGradeChoice;
        private bool _srsApplied;

        // ========= token pools =========
        private readonly List<string> _tokensZh = new();
        private readonly List<string> _tokensVi = new();

        // ========= WebView2 =========
        private readonly WebView2 _web = new();
        private bool _webReady;

        // ========= Fonts (Chinese-friendly) =========
        private static readonly string[] TcFontFamilies =
        {
            "DFKai-SB", "BiauKai", "KaiTi", "STKaiti",
            "Microsoft JhengHei UI", "Microsoft JhengHei",
            "PMingLiU", "MingLiU", "Microsoft YaHei UI", "Microsoft YaHei"
        };

        private static readonly string TcPrimaryFontName = PickInstalledFont(TcFontFamilies) ?? "Microsoft JhengHei";

        // ========= Strings =========
        private const string PH_ZH = "Nhập Tiếng Trung (Phồn thể)";
        private const string PH_VI = "Nhập Tiếng Việt";
        private const string BTN_NEXT = "Tiếp";
        private const string BTN_SUBMIT = "Gửi bài kiểm tra";

        public QuizEssayControlWeb()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            DoubleBuffered = true;

            _web.Dock = DockStyle.Fill;
            Controls.Add(_web);

            Load += async (_, __) =>
            {
                await EnsureWebAsync();
                ShowEmptyState();
                await PushThemeAsync();
            };

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
                        _web.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                }
                catch { }

                try { _web.Dispose(); } catch { }
            };
        }

        // ===================== Public API =====================
        public void BindSelectedSet(CardSet? set, AnswerMode mode, int count, string? dayTitle = null)
        {
            _set = set;
            _mode = mode;
            _dayTitle = dayTitle;
            _submitted = false;

            _questions.Clear();
            _states.Clear();
            _currentIndex = 0;
            _startedAt = DateTime.Now;
            _awaitingGradeChoice = false;
            _srsApplied = false;

            if (set?.Items == null || set.Items.Count == 0)
            {
                ShowEmptyState("(Chưa có từ vựng trong học phần)");
                return;
            }

            var max = Math.Max(0, Math.Min(count, set.Items.Count));
            if (max == 0)
            {
                ShowEmptyState("(Số câu hỏi = 0)");
                return;
            }

            var config = new QuizConfig
            {
                Count = max,
                AnswerMode = mode,
                EnableMultipleChoice = false
            };

            _questions.Clear();
            var questions = QuizEngine.BuildQuestions(set, config);

            foreach (var q in questions)
            {
                _questions.Add(new QuizQuestion
                {
                    SmallLabel = q.SmallLabel,
                    QuestionText = q.QuestionText,
                    CorrectAnswer = q.CorrectAnswer,
                    CardKey = q.CardKey,
                    Index = q.Index,
                    Total = q.Total,
                    UseChineseFontForQuestion = q.UseChineseFontForQuestion,
                    UseChineseFontForChoices = q.UseChineseFontForChoices
                });
            }

            for (int i = 0; i < _questions.Count; i++)
                _states.Add(new QuizAnswerState());

            RebuildTokenPoolsFromSet(set);

            _submitted = false;
            _srsApplied = false;
            _ = RenderQuestionAsync(0);
        }

        public void SetDarkMode(bool isDark)
        {
            _isDarkMode = isDark;
            _ = PushThemeAsync();
        }

        // ===================== Data types =====================
        private sealed class QuizAnswerState
        {
            public string? UserAnswer;
            public bool Skipped;
            public bool IsCorrect;
            public string? AcceptedAnswer;
            public string? GeminiExplanation;
        }

        private sealed class UiToHostMessage
        {
            public string? Type { get; set; }
            public string? Text { get; set; }
        }

        private sealed class HostToUiMessage
        {
            public string? Type { get; set; }
            public UiTheme? Theme { get; set; }
            public UiQuestionState? Question { get; set; }
            public UiResultState? Result { get; set; }
            public UiReviewState? Review { get; set; }
            public UiEmptyState? Empty { get; set; }
        }

        private sealed class UiTheme
        {
            public bool IsDark { get; set; }
            public string? TcFont { get; set; }
        }

        private sealed class UiQuestionState
        {
            public string? TopProgress { get; set; }
            public string? DayTitle { get; set; }
            public string? SmallLabel { get; set; }
            public string? QNum { get; set; }
            public string? Prompt { get; set; }
            public bool PromptIsChinese { get; set; }
            public bool AnswerIsChinese { get; set; }
            public string? Placeholder { get; set; }
            public string? UserAnswer { get; set; }
            public bool CanPrevious { get; set; }
            public string? ButtonNextText { get; set; }
            public List<string>? Tokens { get; set; }
        }

        private sealed class UiResultState
        {
            public string? SetTitle { get; set; }
            public string? CorrectText { get; set; }
            public string? WrongText { get; set; }
            public string? TimeText { get; set; }
            public int Percent { get; set; }
        }

        private sealed class UiReviewState
        {
            public string? Small { get; set; }
            public string? QNum { get; set; }
            public string? Prompt { get; set; }
            public bool PromptIsChinese { get; set; }
            public bool ShowTryLater { get; set; }
            public string? TryLaterText { get; set; }
            public bool TryLaterIsChinese { get; set; }
            public bool ShowYour { get; set; }
            public bool YourOk { get; set; }
            public string? YourText { get; set; }
            public bool YourIsChinese { get; set; }
            public string? CorrectText { get; set; }
            public bool CorrectIsChinese { get; set; }
            public string? GeminiExplanation { get; set; }
            public bool CanPrev { get; set; }
            public bool CanNext { get; set; }
        }

        private sealed class UiEmptyState
        {
            public string? DayTitle { get; set; }
            public string? Message { get; set; }
        }
    }
}
