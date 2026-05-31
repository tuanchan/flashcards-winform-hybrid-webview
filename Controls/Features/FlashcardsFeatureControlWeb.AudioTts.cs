#nullable enable

using NAudio.Wave;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class FlashcardsFeatureControlWeb
    {
        private async Task PlaySoundAsync()
        {
            if (!_ttsEnabled) return;
            if (_set?.Items == null || _order.Count == 0) return;

            var itemIndex = _order[_index];
            var item = _set.Items[itemIndex];
            var text = item.Term ?? "";

            await PlayTextAsync(text);
        }

        private async Task PlayChineseTermAsync()
        {
            if (!_ttsEnabled) return;
            if (_set?.Items == null || _order.Count == 0) return;

            var itemIndex = _order[_index];
            var item = _set.Items[itemIndex];
            var text = item.Term ?? "";

            await PlayTextAsync(text);
        }

        private async Task PlayTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            _ttsCts?.Cancel();
            _ttsCts = new CancellationTokenSource();

            var token = _ttsCts.Token;

            try
            {
                var audioMp3 = await GetOrCreateAudioAsync(text, token);
                if (token.IsCancellationRequested) return;

                BeginInvoke(new Action(() =>
                {
                    if (token.IsCancellationRequested) return;

                    try
                    {
                        _waveOut?.Stop();
                        _waveOut?.Dispose();
                        _waveOut = null;

                        _waveReader?.Dispose();
                        _waveReader = null;

                        _currentSoundStream?.Dispose();
                        _currentSoundStream = null;

                        _currentSoundStream = new MemoryStream(audioMp3);
                        _waveReader = new Mp3FileReader(_currentSoundStream);
                        _waveOut = new WaveOutEvent();
                        _waveOut.Init(_waveReader);
                        _waveOut.Play();
                    }
                    catch { }
                }));
            }
            catch (OperationCanceledException) { }
        }

        private async Task HandleAutoPronounceFlipAsync()
        {
            if (!_autoPronounce) return;
            await PlayChineseTermAsync();
        }

        private async Task<byte[]> GetOrCreateAudioAsync(string text, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<byte>();

            var voice = EdgeTtsRunner.ResolveVoiceByLanguageCode(_set?.LanguageCode);
            var cacheKey = $"{voice}|{text}";

            if (_ttsCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var mp3 = await CourseAudioService.GetOrCreateAudioAsync(_sourceSet ?? _set, text, token);
            _ttsCache[cacheKey] = mp3;
            return mp3;
        }
    }
}
