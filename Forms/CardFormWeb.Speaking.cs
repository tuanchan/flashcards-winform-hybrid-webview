#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    public sealed partial class CardFormWeb
    {
        private CancellationTokenSource? _speakingAudioCts;

        private void SendSpeakingOptionsToWeb()
        {
            var defaultVoice = EdgeTtsRunner.ResolveVoiceByLanguageCode(_selectedSet?.LanguageCode);
            PostSpeaking("speakingOptions", new
            {
                courses = _allSets.Select(ToWritingCourseDto).ToList(),
                selectedCourseId = _selectedSet?.Id ?? "",
                selectedLanguage = VoiceToLanguageKey(defaultVoice),
                defaultVoice,
                voices = EdgeTtsRunner.GetSupportedVoices().Select(v => new
                {
                    voice = v.Voice,
                    label = v.Label,
                    languageKey = v.LanguageKey,
                    languageName = v.LanguageName,
                    languageCode = v.LanguageCode,
                    country = v.Country,
                    gender = v.Gender
                }).ToList()
            });
        }

        private async Task GenerateSpeakingPracticeAsync(string data)
        {
            try
            {
                var root = ParsePayload(data);
                var mode = GetString(root, "mode");
                var courseId = GetString(root, "courseId");
                var topic = GetString(root, "topic");
                var count = Math.Max(4, Math.Min(16, GetInt(root, "count", 6)));
                var languageKey = GetString(root, "targetLanguageKey");
                var languageName = GetString(root, "targetLanguageName");
                var languageCode = GetString(root, "targetLanguageCode");
                var voice = GetString(root, "voice");

                CardSet? sourceSet = null;
                List<CardItem> vocabulary = new();
                if (string.Equals(mode, "course", StringComparison.OrdinalIgnoreCase))
                {
                    sourceSet = _allSets.FirstOrDefault(s => string.Equals(s.Id ?? "", courseId, StringComparison.Ordinal));
                    if (sourceSet == null)
                        throw new InvalidOperationException("Chọn học phần trước khi tạo đối thoại luyện nói.");
                    vocabulary = LoadWritingVocabularyItems(sourceSet);
                }
                else if (string.IsNullOrWhiteSpace(topic))
                {
                    throw new InvalidOperationException("Nhập chủ đề trước khi tạo đối thoại luyện nói.");
                }

                PostSpeaking("speakingBusy", new { busy = true });
                var generated = await GeminiService.GenerateDialogueAsync(
                    sourceSet,
                    vocabulary,
                    topic,
                    count,
                    languageName,
                    languageCode,
                    includeVietnameseAids: true);

                PostSpeaking("speakingPractice", new
                {
                    title = generated.Title,
                    languageKey,
                    languageCode = string.IsNullOrWhiteSpace(languageCode) ? languageKey : languageCode,
                    voice = string.IsNullOrWhiteSpace(voice)
                        ? EdgeTtsRunner.ResolveVoiceByLanguageCode(languageCode)
                        : voice,
                    messages = generated.Messages.Select(x => new
                    {
                        text = x.Text,
                        vietnamese = x.Vietnamese,
                        vietnamesePronunciation = x.VietnamesePronunciation
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                if (SettingsService.IsLikelyApiKeyError(ex.Message))
                    PromptForApiKeysFromFeature();
                PostSpeaking("speakingToast", new { type = "warn", text = ex.Message });
            }
            finally
            {
                PostSpeaking("speakingBusy", new { busy = false });
            }
        }

        private async Task SynthesizeSpeakingLineAsync(string data)
        {
            try
            {
                var root = ParsePayload(data);
                var text = GetString(root, "text");
                var voice = GetString(root, "voice");
                if (string.IsNullOrWhiteSpace(text))
                    return;

                var bytes = await EdgeTtsRunner.SynthesizeMp3Async(text, voice);
                PostSpeaking("speakingAudio", new
                {
                    sessionId = GetString(root, "sessionId"),
                    index = GetInt(root, "index", -1),
                    base64 = Convert.ToBase64String(bytes)
                });
            }
            catch (Exception ex)
            {
                PostSpeaking("speakingToast", new { type = "warn", text = ex.Message });
            }
        }

        private async Task PrepareSpeakingAudioAsync(string data)
        {
            CancelSpeakingAudioPreparation();
            var batchCts = new CancellationTokenSource();
            _speakingAudioCts = batchCts;
            var token = batchCts.Token;

            try
            {
                var root = ParsePayload(data);
                var sessionId = GetString(root, "sessionId");
                var voice = GetString(root, "voice");
                if (!root.TryGetProperty("messages", out var messageList) ||
                    messageList.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                var lines = messageList.EnumerateArray()
                    .Select(item => new
                    {
                        Index = GetInt(item, "index", -1),
                        Text = GetString(item, "text")
                    })
                    .Where(item => item.Index >= 0 && !string.IsNullOrWhiteSpace(item.Text))
                    .ToList();

                foreach (var line in lines)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var bytes = await EdgeTtsRunner.SynthesizeMp3Async(line.Text, voice, token);
                        token.ThrowIfCancellationRequested();
                        PostSpeaking("speakingAudioReady", new
                        {
                            sessionId,
                            index = line.Index,
                            base64 = Convert.ToBase64String(bytes)
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        PostSpeaking("speakingAudioFailed", new
                        {
                            sessionId,
                            index = line.Index,
                            text = ex.Message
                        });
                    }
                }

                PostSpeaking("speakingAudioBatchDone", new { sessionId });
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                try { batchCts.Dispose(); } catch { }
                if (ReferenceEquals(_speakingAudioCts, batchCts))
                    _speakingAudioCts = null;
            }
        }

        private void CancelSpeakingAudioPreparation()
        {
            try { _speakingAudioCts?.Cancel(); } catch { }
            _speakingAudioCts = null;
        }

        private void PostSpeaking(string action, object data)
        {
            var json = JsonSerializer.Serialize(new { action, data }, WritingJsonOptions);
            ExecuteScript($"if(window.handleSpeakingHostMessage) window.handleSpeakingHostMessage({json});");
        }
    }
}
