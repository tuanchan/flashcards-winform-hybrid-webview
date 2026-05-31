#nullable enable

using Microsoft.Web.WebView2.WinForms;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TocflQuiz.Models;

namespace TocflQuiz.Controls.Features
{
    /// <summary>
    /// Web-based Flashcards với Settings HTML overlay + keyboard shortcuts + Edit modal HTML
    /// </summary>
    public sealed partial class FlashcardsFeatureControlWeb : UserControl
    {
        private const string FlashcardsViewPath = "Webviews/flashcards-feature.html";

        // Core state
        private CardSet? _set;
        private CardSet? _sourceSet;
        private readonly HashSet<string> _sessionPendingKnownKeys = new(StringComparer.Ordinal);
        private bool _sessionNotYetFlushed;

        private int _index;
        private int _lastCardIndex = -1;

        private List<int> _order = new();
        private List<int> _filteredOrder = new();
        private List<int> _shuffleOrder = new();

        private bool _shuffleEnabled;
        private bool _progressTracking;
        private bool _starredOnly;
        private bool _ttsEnabled = true;
        private bool _autoPronounce;
        private bool _isDarkMode;
        private bool _completionShown;
        private bool _seenLastCardInNonProgress;
        private int _cardZoomPercent = 100;

        private FrontSideOption _frontSide = FrontSideOption.Term;

        private readonly HashSet<int> _visitedNonProgress = new();
        private int _lastVisitedItemIndex = -1;

        private readonly Dictionary<int, CardProgressState> _progressMap = new();
        private readonly Stack<ProgressAction> _undoStack = new();

        // Learning review state
        private bool _inLearningReview;
        private List<int> _orderBeforeReview = new();
        private int _indexBeforeReview;
        private readonly HashSet<int> _learningReviewTouched = new();

        // TTS
        private readonly Dictionary<string, byte[]> _ttsCache = new(StringComparer.Ordinal);
        private CancellationTokenSource? _ttsCts;
        private IWavePlayer? _waveOut;
        private WaveStream? _waveReader;
        private MemoryStream? _currentSoundStream;

        // WebView2
        private readonly WebView2 _webView = new();
        private bool _ready;

        private enum CardProgressState
        {
            None,
            Learning,
            Known
        }

        private enum FrontSideOption
        {
            Term,
            Definition,
            Pinyin
        }

        private sealed class ProgressAction
        {
            public int CardIndex { get; set; }
            public CardProgressState PreviousState { get; set; }
        }

        public FlashcardsFeatureControlWeb()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            LoadPersistedFlashcardSettings();

            _webView.Dock = DockStyle.Fill;
            Controls.Add(_webView);

            _webView.NavigationCompleted += async (_, __) =>
            {
                if (_ready)
                    await PushStateAsync();
            };

            HandleCreated += async (_, __) => await InitializeAsync();
        }

        // ===== Public API =====
        public void LoadSet(CardSet set)
        {
            _sourceSet = set;
            _sourceSet.Items = Services.CardSetStorage.LoadVocabularyItems(set);

            _set = new CardSet
            {
                Id = set.Id,
                Title = set.Title,
                Description = set.Description,
                CreatedAt = set.CreatedAt,
                Language = set.Language,
                LanguageCode = set.LanguageCode,
                FolderName = set.FolderName,
                BaseFolder = set.BaseFolder,
                VocabsFilePath = set.VocabsFilePath,
                NotYetFilePath = set.NotYetFilePath,
                ConfigFilePath = set.ConfigFilePath,
                VocabCount = set.VocabCount,
                Items = Services.CardSetStorage.LoadStudyItems(set)
            };

            _index = 0;
            _lastCardIndex = -1;
            _progressMap.Clear();
            _undoStack.Clear();
            _shuffleOrder.Clear();
            _completionShown = false;
            _seenLastCardInNonProgress = false;
            _visitedNonProgress.Clear();
            _lastVisitedItemIndex = -1;
            _inLearningReview = false;
            _orderBeforeReview.Clear();
            _indexBeforeReview = 0;
            _learningReviewTouched.Clear();
            _sessionPendingKnownKeys.Clear();
            _sessionNotYetFlushed = false;

            ApplyLegacyStarred();
            RebuildOrder(false);

            _ttsCache.Clear();
            _ = Task.Run(() => Services.CourseAudioService.GenerateMissingAudioAsync(_sourceSet));
            _ = PushStateAsync();
        }

        public void SetDarkMode(bool isDark)
        {
            _isDarkMode = isDark;
            _ = PushStateAsync();
        }
    }
}
