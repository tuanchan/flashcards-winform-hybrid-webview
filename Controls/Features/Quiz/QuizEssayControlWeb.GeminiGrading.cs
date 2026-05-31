#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using TocflQuiz.Forms;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features.Quiz
{
    public sealed partial class QuizEssayControlWeb
    {
        private Task ShowGradeChoiceAsync()
        {
            return PostToWebAsync(new HostToUiMessage
            {
                Type = "gradeChoice",
                Theme = new UiTheme { IsDark = _isDarkMode, TcFont = TcPrimaryFontName }
            });
        }

        private async Task FinishWithLocalGradingAsync()
        {
            _awaitingGradeChoice = false;

            for (int i = 0; i < _states.Count; i++)
            {
                var state = _states[i];
                var q = _questions[i];
                state.IsCorrect = !state.Skipped && IsAnswerCorrect(q, state.UserAnswer ?? "");
                state.AcceptedAnswer = q.CorrectAnswer;
                state.GeminiExplanation = "";
            }

            await ShowEssayResultAsync();
        }

        private async Task FinishWithGeminiGradingAsync()
        {
            if (!SettingsService.HasGeminiApiKey())
            {
                PromptApiKeys();
                await FinishWithLocalGradingAsync();
                return;
            }

            _awaitingGradeChoice = false;

            await PostToWebAsync(new HostToUiMessage
            {
                Type = "grading",
                Theme = new UiTheme { IsDark = _isDarkMode, TcFont = TcPrimaryFontName }
            });

            try
            {
                var script = _questions.Select((q, i) => new GeminiEssayAnswerScript
                {
                    Index = i + 1,
                    Prompt = q.UseChineseFontForQuestion
                        ? (q.QuestionText ?? "")
                        : StripDefinitionForAnswer(q.QuestionText, null),
                    CorrectAnswer = q.CorrectAnswer ?? "",
                    UserAnswer = _states[i].UserAnswer ?? "",
                    Skipped = _states[i].Skipped,
                    AnswerIsChinese = q.UseChineseFontForChoices
                }).ToList();

                var result = await GeminiService.GradeEssayAsync(_set, script);
                var grades = (result.Items ?? new()).ToDictionary(x => x.Index);

                for (int i = 0; i < _states.Count; i++)
                {
                    var state = _states[i];
                    var q = _questions[i];
                    var localCorrect = !state.Skipped && IsAnswerCorrect(q, state.UserAnswer ?? "");

                    if (grades.TryGetValue(i + 1, out var grade))
                    {
                        state.IsCorrect = !state.Skipped && grade.IsCorrect;
                        state.AcceptedAnswer = string.IsNullOrWhiteSpace(grade.AcceptedAnswer)
                            ? q.CorrectAnswer
                            : grade.AcceptedAnswer;
                        state.GeminiExplanation = grade.Explanation;
                    }
                    else
                    {
                        state.IsCorrect = localCorrect;
                        state.AcceptedAnswer = q.CorrectAnswer;
                        state.GeminiExplanation = "Gemini không trả kết quả cho câu này, hệ thống đã chấm cục bộ.";
                    }
                }
            }
            catch (Exception ex)
            {
                if (SettingsService.IsLikelyApiKeyError(ex.Message))
                    PromptApiKeys();

                for (int i = 0; i < _states.Count; i++)
                {
                    var state = _states[i];
                    var q = _questions[i];
                    state.IsCorrect = !state.Skipped && IsAnswerCorrect(q, state.UserAnswer ?? "");
                    state.AcceptedAnswer = q.CorrectAnswer;
                    state.GeminiExplanation = "Không gọi được Gemini, đã chấm cục bộ. " + ex.Message;
                }
            }

            await ShowEssayResultAsync();
        }

        private void PromptApiKeys()
        {
            if (FindForm() is CardFormWeb form)
                form.PromptForApiKeysFromFeature();
        }

        private Task ShowEssayResultAsync()
        {
            ApplySrsFromEssayResult();

            var elapsed = DateTime.Now - _startedAt;
            int total = _questions.Count;
            int correct = _states.Count(x => x.IsCorrect);
            int wrong = total - correct;
            int percent = total == 0 ? 0 : (int)Math.Round(100.0 * correct / total);

            var dto = new HostToUiMessage
            {
                Type = "result",
                Theme = new UiTheme { IsDark = _isDarkMode, TcFont = TcPrimaryFontName },
                Result = new UiResultState
                {
                    SetTitle = _set?.Title ?? "",
                    CorrectText = $"Đúng: {correct}",
                    WrongText = $"Sai: {wrong}",
                    TimeText = $"Thời gian: {FormatTime(elapsed)}",
                    Percent = percent
                }
            };

            return PostToWebAsync(dto);
        }

        private void ApplySrsFromEssayResult()
        {
            if (_srsApplied || _set == null)
                return;

            for (int i = 0; i < _questions.Count && i < _states.Count; i++)
            {
                Services.SpacedRepetitionService.ApplyReview(_set, _questions[i].CardKey, _states[i].IsCorrect);
            }

            _srsApplied = true;
        }
    }
}
