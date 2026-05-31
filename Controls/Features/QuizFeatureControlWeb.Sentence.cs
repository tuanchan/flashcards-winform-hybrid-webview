#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TocflQuiz.Controls.Features.Quiz;
using TocflQuiz.Forms;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class QuizFeatureControlWeb
    {
        private async Task StartGeminiSentenceQuizAsync()
        {
            if (_set == null) return;

            try
            {
                if (!EnsureGeminiKeyOrPrompt())
                    return;

                ResetQuizState();
                _submitted = false;
                _startedAt = DateTime.Now;
                _elapsed = TimeSpan.Zero;

                Post("renderSentenceLoading", new
                {
                    setTitle = _set.Title,
                    count = _cfg.Count
                });

                var cached = GeminiSentenceStore.TryGet(_set, _cfg.Count);
                var generated = cached;
                var fromCache = cached != null;

                if (generated == null || generated.Questions == null || generated.Questions.Count == 0 || NeedsVietnameseSentenceRefresh(generated))
                {
                    Post("toast", new { type = "info", text = "Gemini đang tạo câu giao tiếp..." });
                    generated = await GeminiService.GenerateSentenceQuizAsync(_set, _cfg.Count);
                    GeminiSentenceStore.Save(_set, _cfg.Count, generated);
                    fromCache = false;
                }

                if (generated.Questions == null || generated.Questions.Count == 0)
                {
                    Post("toast", new { type = "warn", text = "Gemini chưa tạo được câu giao tiếp." });
                    PostSetupDefaults();
                    return;
                }

                _sentenceQuestions = generated.Questions;
                _sentenceAnswers = new();
                _sentenceGeminiGrades = new();
                _sentenceUseGeminiGrades = false;
                _submitted = false;

                var payload = new
                {
                    setTitle = _set.Title,
                    total = _sentenceQuestions.Count,
                    fromCache,
                    hint = GetSentenceAnswerHint(),
                    questions = _sentenceQuestions.Select(q => new
                    {
                        index = q.Index,
                        total = _sentenceQuestions.Count,
                        prompt = q.Prompt,
                        placeholder = GetSentenceAnswerPlaceholder()
                    }).ToList()
                };

                Post("renderSentenceQuiz", payload);
                PostProgress();
                Post("setFooterMode", new { mode = "hidden" });
            }
            catch (Exception ex)
            {
                if (SettingsService.IsLikelyApiKeyError(ex.Message))
                    PromptApiKeys();
                Post("toast", new { type = "warn", text = ex.Message });
                PostSetupDefaults();
            }
        }

        private bool NeedsVietnameseSentenceRefresh(GeminiSentenceQuizPayload payload)
        {
            if (_cfg.AnswerMode != AnswerMode.Vietnamese && _cfg.AnswerMode != AnswerMode.Both)
                return false;

            return payload.Questions.Any(q =>
            {
                var vietnamese = (q.Vietnamese ?? "").Trim();
                if (string.IsNullOrWhiteSpace(vietnamese))
                    return true;

                var normalizedVietnamese = NormalizeSentence(vietnamese);
                return !string.IsNullOrWhiteSpace(normalizedVietnamese) &&
                       (string.Equals(normalizedVietnamese, NormalizeSentence(q.Prompt), StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalizedVietnamese, NormalizeSentence(q.EnglishMeaning), StringComparison.OrdinalIgnoreCase));
            });
        }

        private void HandleSentenceAnswer(JsonElement data)
        {
            if (_submitted || !_cfg.EnableSentenceWriting) return;

            int qIndex = TryGetInt(data, "qIndex", 0);
            string text = "";

            try
            {
                if (data.ValueKind == JsonValueKind.Object &&
                    data.TryGetProperty("text", out var textProp))
                {
                    text = textProp.GetString() ?? "";
                }
            }
            catch { }

            if (qIndex <= 0) return;

            _sentenceAnswers[qIndex] = text.Trim();
            PostProgress();

            if (AnsweredCount() >= _sentenceQuestions.Count)
                Post("setFooterMode", new { mode = "submit" });
            else
                Post("setFooterMode", new { mode = "hidden" });
        }

        private void HandleSentenceSubmit()
        {
            if (_submitted)
            {
                SendSentenceResultOverlayToWeb();
                return;
            }

            if (_sentenceQuestions.Count == 0)
            {
                Post("toast", new { type = "info", text = "Chưa có bài đặt câu để nộp." });
                return;
            }

            if (AnsweredCount() < _sentenceQuestions.Count)
            {
                Post("toast", new { type = "warn", text = "Bạn chưa trả lời hết. Hãy trả lời đủ trước khi nộp." });
                Post("setFooterMode", new { mode = "submit" });
                return;
            }

            Post("showSentenceGradeChoice", new
            {
                setTitle = _set?.Title ?? "(chưa chọn)",
                geminiReady = GeminiService.IsConfigured()
            });
        }

        private void FinishSentenceWithLocalGrading()
        {
            _sentenceUseGeminiGrades = false;
            _sentenceGeminiGrades.Clear();
            _submitted = true;
            _elapsed = DateTime.Now - _startedAt;

            Post("hideSentenceGradeChoice", new { });
            SendSentenceResultOverlayToWeb();
        }

        private async Task FinishSentenceWithGeminiGradingAsync()
        {
            if (!EnsureGeminiKeyOrPrompt())
                return;

            _submitted = true;
            _elapsed = DateTime.Now - _startedAt;
            _sentenceUseGeminiGrades = true;
            _sentenceGeminiGrades.Clear();

            Post("hideSentenceGradeChoice", new { });
            Post("toast", new { type = "info", text = "Gemini đang chấm bài đặt câu..." });

            try
            {
                var script = BuildSentenceGradeScripts();
                var result = await GeminiService.GradeEssayAsync(_set, script);
                _sentenceGeminiGrades = (result.Items ?? new List<GeminiEssayGradeItem>())
                    .GroupBy(x => x.Index)
                    .ToDictionary(g => g.Key, g => g.First());
            }
            catch (Exception ex)
            {
                if (SettingsService.IsLikelyApiKeyError(ex.Message))
                    PromptApiKeys();
                _sentenceUseGeminiGrades = false;
                _sentenceGeminiGrades.Clear();
                Post("toast", new { type = "warn", text = "Không gọi được Gemini, đã chấm theo logic app. " + ex.Message });
            }

            SendSentenceResultOverlayToWeb();
        }

        private bool EnsureGeminiKeyOrPrompt()
        {
            if (SettingsService.HasGeminiApiKey())
                return true;

            PromptApiKeys();
            PostSetupDefaults();
            return false;
        }

        private void PromptApiKeys()
        {
            if (ParentForm is CardFormWeb form)
                form.PromptForApiKeysFromFeature();
        }

        private List<GeminiEssayAnswerScript> BuildSentenceGradeScripts()
        {
            return _sentenceQuestions.Select(q =>
            {
                var userAnswer = _sentenceAnswers.TryGetValue(q.Index, out var answer) ? answer : "";
                return new GeminiEssayAnswerScript
                {
                    Index = q.Index,
                    Prompt = $"Câu giao tiếp: {q.Prompt}",
                    CorrectAnswer = GetSentenceExpectedAnswer(q),
                    UserAnswer = userAnswer,
                    Skipped = string.IsNullOrWhiteSpace(userAnswer),
                    AnswerIsChinese = false
                };
            }).ToList();
        }

        private void SendSentenceResultOverlayToWeb()
        {
            int total = _sentenceQuestions.Count;
            int correct = _sentenceQuestions.Count(q => IsSentenceCorrect(q));
            int wrong = Math.Max(0, total - correct);
            int percent = total > 0 ? (int)Math.Round((correct * 100.0) / total) : 0;

            Post("showResult", new
            {
                setTitle = _set?.Title ?? "(chưa chọn)",
                correct,
                wrong,
                total,
                percent = Math.Max(0, Math.Min(100, percent)),
                elapsed = (_elapsed.TotalSeconds <= 0.5) ? null : _elapsed.ToString(@"mm\:ss"),
            });
        }

        private void SendSentenceReviewStateToWeb()
        {
            var items = _sentenceQuestions.Select(q =>
            {
                var userAnswer = _sentenceAnswers.TryGetValue(q.Index, out var answer) ? answer : "";
                var hasAiGrade = false;
                GeminiEssayGradeItem? aiGrade = null;

                if (_sentenceUseGeminiGrades &&
                    _sentenceGeminiGrades.TryGetValue(q.Index, out var foundGrade))
                {
                    hasAiGrade = true;
                    aiGrade = foundGrade;
                }

                return new
                {
                    qIndex = q.Index,
                    sourceSentence = q.Prompt,
                    userAnswer,
                    correct = IsSentenceCorrect(q),
                    expectedAnswer = hasAiGrade && !string.IsNullOrWhiteSpace(aiGrade?.AcceptedAnswer)
                        ? aiGrade.AcceptedAnswer
                        : GetSentenceExpectedAnswer(q),
                    englishMeaning = q.EnglishMeaning,
                    vietnamese = q.Vietnamese,
                    explanation = hasAiGrade && !string.IsNullOrWhiteSpace(aiGrade?.Explanation)
                        ? aiGrade.Explanation
                        : q.Explanation
                };
            }).ToList();

            Post("applySentenceReview", new { items });
        }

        private bool IsSentenceCorrect(GeminiSentenceQuestion question)
        {
            if (!_sentenceAnswers.TryGetValue(question.Index, out var user) || string.IsNullOrWhiteSpace(user))
                return false;

            if (_sentenceUseGeminiGrades &&
                _sentenceGeminiGrades.TryGetValue(question.Index, out var grade))
            {
                return grade.IsCorrect;
            }

            var normalizedUser = NormalizeSentence(user);
            if (string.IsNullOrWhiteSpace(normalizedUser))
                return false;

            foreach (var expected in GetSentenceExpectedCandidates(question))
            {
                var normalizedExpected = NormalizeSentence(expected);
                if (string.IsNullOrWhiteSpace(normalizedExpected))
                    continue;

                if (string.Equals(normalizedUser, normalizedExpected, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (normalizedExpected.Length >= 8 &&
                    (normalizedUser.Contains(normalizedExpected, StringComparison.OrdinalIgnoreCase) ||
                     normalizedExpected.Contains(normalizedUser, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetSentenceAnswerHint()
        {
            return _cfg.AnswerMode switch
            {
                AnswerMode.Vietnamese => "Nhập nghĩa tiếng Việt",
                AnswerMode.Both => $"Nhập nghĩa bằng {GetSourceLanguageLabel()} hoặc tiếng Việt",
                _ => $"Nhập nghĩa bằng {GetSourceLanguageLabel()}"
            };
        }

        private string GetSentenceAnswerPlaceholder()
        {
            return _cfg.AnswerMode switch
            {
                AnswerMode.Vietnamese => "Nhập nghĩa tiếng Việt...",
                AnswerMode.Both => $"Nhập nghĩa bằng {GetSourceLanguageLabel()} hoặc tiếng Việt...",
                _ => $"Nhập nghĩa bằng {GetSourceLanguageLabel()}..."
            };
        }

        private string GetSentenceExpectedAnswer(GeminiSentenceQuestion question)
        {
            var candidates = GetSentenceExpectedCandidates(question)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return candidates.Count == 0 ? "" : string.Join(" / ", candidates);
        }

        private IEnumerable<string> GetSentenceExpectedCandidates(GeminiSentenceQuestion question)
        {
            var sourceMeaning = FirstNonEmpty(question.EnglishMeaning);
            var vietnamese = FirstNonEmpty(question.Vietnamese);

            if (_cfg.AnswerMode == AnswerMode.Vietnamese)
            {
                if (!string.IsNullOrWhiteSpace(vietnamese)) yield return vietnamese;
                yield break;
            }

            if (_cfg.AnswerMode == AnswerMode.Both)
            {
                if (!string.IsNullOrWhiteSpace(sourceMeaning)) yield return sourceMeaning;
                if (!string.IsNullOrWhiteSpace(vietnamese)) yield return vietnamese;
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(sourceMeaning)) yield return sourceMeaning;
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return "";
        }

        private static string NormalizeSentence(string value)
        {
            var noMarks = RemoveDiacritics(value ?? "").ToLowerInvariant();
            var chars = noMarks
                .Where(char.IsLetterOrDigit)
                .ToArray();

            return new string(chars);
        }

        private static string RemoveDiacritics(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                if (ch == 'đ')
                {
                    sb.Append('d');
                    continue;
                }

                if (ch == 'Đ')
                {
                    sb.Append('D');
                    continue;
                }

                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
