#nullable enable

using System;
using System.Linq;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class QuizFeatureControlWeb
    {
        private void SendResultOverlayToWeb()
        {
            int total = _questions.Count;
            int correct = 0;

            foreach (var q in _questions)
            {
                if (IsCorrect(q.Index)) correct++;
            }

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

        private void SendReviewStateToWeb()
        {
            var review = _questions.Select(q =>
            {
                int? picked = _pickedIndexByQ.TryGetValue(q.Index, out var v) ? v : null;
                bool dk = _dontKnow.Contains(q.Index);
                int correctIndex = -1;
                var correctText = (q.CorrectAnswer ?? "").Trim();

                for (int i = 0; i < q.Choices.Count; i++)
                {
                    if (string.Equals((q.Choices[i] ?? "").Trim(), correctText, StringComparison.Ordinal))
                    {
                        correctIndex = i;
                        break;
                    }
                }

                return new
                {
                    qIndex = q.Index,
                    pickedIndex = picked,
                    dontKnow = dk,
                    correctIndex
                };
            }).ToList();

            Post("applyReview", new { items = review });
        }
    }
}
