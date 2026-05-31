#nullable enable

using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TocflQuiz.Controls.Features;
using TocflQuiz.Controls.Features.Quiz;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    /// <summary>
    /// CardFormWeb:
    /// - WebView2 làm trang chủ + chọn học phần
    /// - Khi mở feature -> hiển thị UserControl thật (Flashcards/Quiz/CreateCourse) ngay trong CardFormWeb
    /// - KHÔNG tạo form mới (không FeatureHostForm, không CreateCourseDialog)
    /// </summary>
    public sealed partial class CardFormWeb : Form
    {
        public static event Action? AppReady;

        private const string CardFormHomeViewPath = "Webviews/card-form-home.html";

        // Web
        private WebView2 _webView = null!;
        private bool _isWebReady = false;
        private UserControl? _currentWinView;

        // Data
        private CardSet? _selectedSet;
        private readonly List<CardSet> _allSets = new();
        private readonly List<Form> _openedFeatureForms = new();

        // Theme
        private bool _isDarkMode = true;

        // Dependencies (giữ giống CardForm)
        private readonly AppConfig? _cfg;
        private readonly List<QuestionGroup>? _groups;
        private readonly Dictionary<string, ProgressRecord>? _progressMap;
        private readonly ProgressStoreJson? _store;
        private readonly SpacedRepetition? _sr;

        // Toast (giống CardForm)
        private VocabToastScheduler? _toastScheduler;
        private VocabToastSettings _toastSettings = new VocabToastSettings();

        // ===== WinForms host overlay (hiển thị control thật) =====
        private readonly Panel _winHost = new();
        private readonly Panel _winTopBar = new();
        private readonly Button _btnBack = new();
        private readonly Panel _winContent = new();

        // ===== Các view thật (control sẵn có) =====
        private FlashcardsFeatureControlWeb? _flashcardsView;
        private QuizFeatureControlWeb? _quizView;
        private QuizEssayControlWeb? _essayView;
        private DialogueFeatureControlWeb? _dialogueView;
        private CreateCourseFeatureControl? _createCourseView;
        private bool _prewarmStarted;
        private FullscreenKeyMessageFilter? _fullscreenKeyFilter;
        private bool _isFullScreen;
        private FormBorderStyle _restoreBorderStyle = FormBorderStyle.Sizable;
        private FormWindowState _restoreWindowState = FormWindowState.Normal;
        private Rectangle _restoreBounds;
        private DateTime _lastFullScreenToggleUtc = DateTime.MinValue;

        public CardFormWeb() : this(null, null, null, null, null) { }

        public CardFormWeb(
            AppConfig? cfg,
            List<QuestionGroup>? groups,
            Dictionary<string, ProgressRecord>? progressMap,
            ProgressStoreJson? store,
            SpacedRepetition? sr)
        {
            _cfg = cfg;
            CardSetStorage.ConfigureDatasetRoot(_cfg?.DatasetRoot);

            _groups = groups;
            _progressMap = progressMap;
            _store = store;
            _sr = sr;

            Text = "";
            ShowIcon = false;
            KeyPreview = true;
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1200, 700);
            _restoreBounds = Bounds;

            _fullscreenKeyFilter = new FullscreenKeyMessageFilter(this);
            Application.AddMessageFilter(_fullscreenKeyFilter);

            BuildWinHostOverlay();
            InitializeWebView();
            LoadAllSets();

            _toastScheduler = new VocabToastScheduler(() => _selectedSet);

            FormClosed += (_, __) =>
            {
                if (_fullscreenKeyFilter != null)
                {
                    Application.RemoveMessageFilter(_fullscreenKeyFilter);
                    _fullscreenKeyFilter = null;
                }

                try { _toastScheduler?.Dispose(); } catch { }
                _toastScheduler = null;
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _toastScheduler?.AttachUiContext();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F11)
            {
                ToggleFullScreen();
                return true;
            }

            if (!Visible || WindowState == FormWindowState.Minimized || !ContainsFocus)
                return base.ProcessCmdKey(ref msg, keyData);

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
