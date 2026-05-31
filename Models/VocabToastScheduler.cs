using System;
using System.Linq;
using System.Threading;
using TocflQuiz.Forms;
using TocflQuiz.Models;

namespace TocflQuiz.Services
{
    public sealed class VocabToastScheduler : IDisposable
    {
        private readonly Func<CardSet?> _getSelectedSet;

        private SynchronizationContext? _ui;

        // ✅ dùng rõ System.Threading.Timer để khỏi ambiguous
        private System.Threading.Timer? _timer;

        private VocabToastSettings _settings = new VocabToastSettings();
        private readonly Random _rng = new Random();

        private CardSet? _cachedSet;
        private (string key, string han, string pinyin, string meaning)[] _cachedItems
            = Array.Empty<(string, string, string, string)>();

        private int _seqIndex = 0;

        private readonly VocabToastProgressStore _store = new VocabToastProgressStore();
        private readonly object _learnLock = new object();
        private System.Collections.Generic.HashSet<string> _learned;

        public VocabToastScheduler(Func<CardSet?> getSelectedSet)
        {
            _getSelectedSet = getSelectedSet ?? throw new ArgumentNullException(nameof(getSelectedSet));
            _learned = _store.Load();
        }

        public void AttachUiContext()
        {
            _ui = SynchronizationContext.Current;
        }

        public void ApplySettings(VocabToastSettings settings)
        {
            _settings = settings?.Clone() ?? new VocabToastSettings();
        }

        public void Restart()
        {
            Stop();
            if (!_settings.Enabled) return;

            var intervalMs = (int)Math.Max(1000, _settings.EveryMinutes * 60_000.0);

            // ✅ rõ System.Threading.Timer
            _timer = new System.Threading.Timer(_ => Tick(), null, intervalMs, intervalMs);
        }

        public void Stop()
        {
            try { _timer?.Dispose(); } catch { }
            _timer = null;
        }

        public void NotifySelectedSetChanged()
        {
            _cachedSet = null;
            _cachedItems = Array.Empty<(string, string, string, string)>();
            _seqIndex = 0;
        }

        public void ShowOneNow()
        {
            Tick(force: true);
        }

        private void Tick(bool force = false)
        {
            if (!_settings.Enabled && !force) return;
            if (_ui == null) return;

            var item = PickOne();
            if (string.IsNullOrWhiteSpace(item.han)) return;

            _ui.Post(_ =>
            {
                var toast = new VocabToastForm(
                    key: item.key,
                    han: item.han,
                    pinyin: item.pinyin,
                    meaning: item.meaning,
                    showSeconds: _settings.ShowSeconds,
                    onAction: OnToastAction);

                toast.Show();
            }, null);
        }

        private void OnToastAction(string key, ToastAction action)
        {
            if (action != ToastAction.Learned) return;

            key = key ?? string.Empty;
            if (key.Length == 0) return;

            lock (_learnLock)
            {
                if (_learned.Add(key))
                    _store.Save(_learned);
            }
        }

        private (string key, string han, string pinyin, string meaning) PickOne()
        {
            var set = _getSelectedSet();
            if (set == null) return default;

            EnsureCache(set);
            if (_cachedItems.Length == 0) return default;

            var candidates = _cachedItems;

            if (_settings.SkipLearned)
            {
                lock (_learnLock)
                {
                    candidates = candidates.Where(x => !_learned.Contains(x.key)).ToArray();
                }
            }

            if (candidates.Length == 0) return default;

            if (_settings.RandomFromSet)
            {
                return candidates[_rng.Next(candidates.Length)];
            }

            // sequential: tìm phần tử tiếp theo (skip learned)
            for (int i = 0; i < _cachedItems.Length; i++)
            {
                var idx = (_seqIndex + i) % _cachedItems.Length;
                var it = _cachedItems[idx];

                if (_settings.SkipLearned)
                {
                    bool learned;
                    lock (_learnLock) learned = _learned.Contains(it.key);
                    if (learned) continue;
                }

                _seqIndex = (idx + 1) % _cachedItems.Length;
                return it;
            }

            return candidates[0];
        }

        private void EnsureCache(CardSet set)
        {
            if (ReferenceEquals(set, _cachedSet) && _cachedItems.Length > 0)
                return;

            _cachedSet = set;
            _seqIndex = 0;

            // ✅ null-guard mạnh để khỏi warning null reference
            var items = set.Items ?? new System.Collections.Generic.List<CardItem>();

            _cachedItems = items
                .Where(i => i != null)
                .Select(i =>
                {
                    var han = (i.Term ?? "").Trim();
                    var meaning = (i.Definition ?? "").Trim();
                    var pinyin = (i.Pinyin ?? "").Trim();

                    var key = $"{han}||{pinyin}".Trim();
                    return (key, han, pinyin, meaning);
                })
                .Where(x => x.han.Length > 0 && (x.meaning.Length > 0 || x.pinyin.Length > 0))
                .ToArray();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
