#nullable enable

using System;
using System.Text.Json;
using TocflQuiz.Controls.Features.Quiz;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class QuizFeatureControlWeb
    {
        private string GetSourceLanguageLabel()
        {
            var lang = (_set?.Language ?? "").Trim();
            return string.IsNullOrWhiteSpace(lang) ? "Ngôn ngữ gốc" : lang;
        }

        private QuizPreference GetCurrentQuizPreference()
        {
            return SettingsService.GetQuizPreference(_set?.Id, _set?.FolderName, _set?.Title);
        }

        private void SaveCurrentQuizPreference(int count, int answerMode, bool multi, bool essay, bool sentence)
        {
            if (_set == null) return;

            SettingsService.SaveQuizPreference(
                _set.Id,
                _set.FolderName,
                _set.Title,
                new QuizPreference
                {
                    Count = count,
                    AnswerMode = answerMode,
                    Multi = multi,
                    Essay = essay,
                    Sentence = sentence
                });
        }

        private void HandleStartFromSetup(JsonElement data)
        {
            if (_set?.Items == null || _set.Items.Count == 0)
            {
                Post("toast", new { type = "warn", text = "Chưa có thẻ trong học phần. Hãy tạo học phần trước." });
                return;
            }

            int count = TryGetInt(data, "count", _set.Items.Count);
            int answerMode = TryGetInt(data, "answerMode", 0);
            bool multi = TryGetBool(data, "multi", true);
            bool essay = TryGetBool(data, "essay", false);
            bool sentence = TryGetBool(data, "sentence", false);

            if (sentence)
            {
                multi = false;
                essay = false;
            }
            else if (essay)
            {
                multi = false;
            }

            count = Math.Max(1, Math.Min(count, _set.Items.Count));
            answerMode = Math.Max(0, Math.Min(answerMode, 2));

            SaveCurrentQuizPreference(count, answerMode, multi, essay, sentence);

            _cfg = new QuizConfig
            {
                Count = count,
                AnswerMode = (AnswerMode)answerMode,
                EnableMultipleChoice = multi,
                EnableSentenceWriting = sentence
            };

            if (_cfg.EnableSentenceWriting)
            {
                _ = StartGeminiSentenceQuizAsync();
                return;
            }

            if (!_cfg.EnableMultipleChoice)
            {
                EssayModeRequested?.Invoke(_set, _cfg);
                return;
            }

            StartQuiz();
        }

        private void PostSetupDefaults()
        {
            int max = _set?.Items?.Count ?? 0;
            var pref = GetCurrentQuizPreference();

            int count = pref.Count > 0 ? pref.Count : max;
            count = Math.Max(0, Math.Min(count, max));

            int answerMode = Math.Max(0, Math.Min(pref.AnswerMode, 2));
            bool multi = pref.Multi;
            bool essay = pref.Essay;
            bool sentence = pref.Sentence;

            if (sentence)
            {
                multi = false;
                essay = false;
            }

            if (!multi && !essay && !sentence)
                multi = true;

            Post("setupDefaults", new
            {
                max,
                count = max > 0 ? count : 0,
                answerMode,
                multi,
                essay,
                sentence,
                sourceLanguage = GetSourceLanguageLabel(),
                answerModeOptions = new[]
                {
                    new { value = 0, text = GetSourceLanguageLabel() },
                    new { value = 1, text = "Tiếng Việt" },
                    new { value = 2, text = "Cả hai" }
                }
            });
        }
    }
}
