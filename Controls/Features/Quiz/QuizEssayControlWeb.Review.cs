#nullable enable

using System;
using System.Threading.Tasks;

namespace TocflQuiz.Controls.Features.Quiz
{
    public sealed partial class QuizEssayControlWeb
    {
        private int _reviewIndex;

        private Task ShowReviewAsync(int index)
        {
            if (_questions.Count == 0) return Task.CompletedTask;

            index = Math.Max(0, Math.Min(index, _questions.Count - 1));
            _reviewIndex = index;

            var q = _questions[index];
            var st = _states[index];

            bool wrongOrSkip = st.Skipped || !st.IsCorrect;
            bool showYour = !string.IsNullOrWhiteSpace(st.UserAnswer) || st.Skipped;

            var dto = new HostToUiMessage
            {
                Type = "review",
                Theme = new UiTheme { IsDark = _isDarkMode, TcFont = TcPrimaryFontName },
                Review = new UiReviewState
                {
                    Small = q.SmallLabel ?? "",
                    QNum = $"{q.Index} of {q.Total}",
                    Prompt = q.UseChineseFontForQuestion
                        ? (q.QuestionText ?? "")
                        : StripDefinitionForAnswer(q.QuestionText, null),
                    PromptIsChinese = q.UseChineseFontForQuestion,
                    ShowTryLater = wrongOrSkip,
                    TryLaterText = st.Skipped ? "Bỏ qua" : (st.UserAnswer ?? ""),
                    TryLaterIsChinese = q.UseChineseFontForChoices,
                    ShowYour = showYour,
                    YourOk = st.IsCorrect,
                    YourText = st.Skipped ? "Bỏ qua" : (st.UserAnswer ?? ""),
                    YourIsChinese = q.UseChineseFontForChoices,
                    CorrectText = string.IsNullOrWhiteSpace(st.AcceptedAnswer) ? (q.CorrectAnswer ?? "") : st.AcceptedAnswer,
                    CorrectIsChinese = q.UseChineseFontForChoices,
                    GeminiExplanation = st.GeminiExplanation,
                    CanPrev = index > 0,
                    CanNext = index < _questions.Count - 1
                }
            };

            return PostToWebAsync(dto);
        }
    }
}
