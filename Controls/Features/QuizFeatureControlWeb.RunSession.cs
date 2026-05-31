#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using TocflQuiz.Controls.Features.Quiz;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class QuizFeatureControlWeb
    {
        private void StartQuiz()
        {
            if (_set == null) return;

            var questions = QuizEngine.BuildQuestions(_set, _cfg);
            if (questions.Count < 1)
            {
                Post("toast", new { type = "info", text = "Học phần cần ít nhất 4 thẻ để tạo trắc nghiệm 4 đáp án." });
                return;
            }

            ResetQuizState();

            _questions = questions;
            _pickedIndexByQ = new Dictionary<int, int?>();
            _dontKnow = new HashSet<int>();
            _submitted = false;
            _srsApplied = false;
            _startedAt = DateTime.Now;
            _elapsed = TimeSpan.Zero;

            var payload = new
            {
                setTitle = _set.Title,
                total = _questions.Count,
                questions = _questions.Select(q => new
                {
                    index = q.Index,
                    total = q.Total,
                    smallLabel = q.SmallLabel,
                    questionText = q.QuestionText,
                    useChineseFontForQuestion = q.UseChineseFontForQuestion,
                    useChineseFontForChoices = q.UseChineseFontForChoices,
                    choices = q.Choices
                }).ToList()
            };

            Post("renderQuiz", payload);
            PostProgress();
            Post("setFooterMode", new { mode = "hidden" });
        }

        private void HandlePick(System.Text.Json.JsonElement data)
        {
            if (_submitted) return;

            int qIndex = TryGetInt(data, "qIndex", 0);
            int choiceIndex = TryGetInt(data, "choiceIndex", -1);

            if (qIndex <= 0) return;
            if (choiceIndex < 0 || choiceIndex > 3) return;

            _pickedIndexByQ[qIndex] = choiceIndex;
            _dontKnow.Remove(qIndex);

            PostProgress();

            if (AnsweredCount() >= _questions.Count)
                Post("setFooterMode", new { mode = "submit" });
            else
                Post("setFooterMode", new { mode = "hidden" });

            int next = FindNextUnanswered(qIndex);
            Post("focusNext", new { from = qIndex, next });
        }

        private void HandleDontKnow(System.Text.Json.JsonElement data)
        {
            if (_submitted) return;

            int qIndex = TryGetInt(data, "qIndex", 0);
            if (qIndex <= 0) return;

            _pickedIndexByQ[qIndex] = null;
            _dontKnow.Add(qIndex);

            PostProgress();

            if (AnsweredCount() >= _questions.Count)
                Post("setFooterMode", new { mode = "submit" });
            else
                Post("setFooterMode", new { mode = "hidden" });

            int next = FindNextUnanswered(qIndex);
            Post("focusNext", new { from = qIndex, next });
        }

        private void HandleSubmit()
        {
            if (_cfg.EnableSentenceWriting)
            {
                HandleSentenceSubmit();
                return;
            }

            if (_submitted)
            {
                SendResultOverlayToWeb();
                return;
            }

            if (_questions.Count == 0)
            {
                Post("toast", new { type = "info", text = "Chưa có bài kiểm tra để nộp." });
                return;
            }

            if (AnsweredCount() < _questions.Count)
            {
                Post("toast", new { type = "warn", text = "Bạn chưa trả lời hết. Hãy trả lời đủ trước khi nộp." });
                Post("setFooterMode", new { mode = "submit" });
                return;
            }

            _submitted = true;
            _elapsed = DateTime.Now - _startedAt;
            ApplySrsFromMultipleChoiceResult();
            SendResultOverlayToWeb();
        }

        private void ResetQuizState()
        {
            _questions = new List<QuizQuestion>();
            _pickedIndexByQ = new Dictionary<int, int?>();
            _dontKnow = new HashSet<int>();
            _sentenceQuestions = new List<GeminiSentenceQuestion>();
            _sentenceAnswers = new Dictionary<int, string>();
            _sentenceGeminiGrades = new Dictionary<int, GeminiEssayGradeItem>();
            _sentenceUseGeminiGrades = false;
            _submitted = false;
            _srsApplied = false;
            _elapsed = TimeSpan.Zero;
        }

        private int AnsweredCount()
        {
            if (_cfg.EnableSentenceWriting)
                return _sentenceQuestions.Count(q => _sentenceAnswers.TryGetValue(q.Index, out var text) && !string.IsNullOrWhiteSpace(text));

            int pickedAnswered = _pickedIndexByQ.Count(kv => kv.Value.HasValue);
            int dk = _dontKnow.Count;
            return pickedAnswered + dk;
        }

        private void PostProgress()
        {
            int total = _cfg.EnableSentenceWriting ? _sentenceQuestions.Count : _questions.Count;
            int answered = AnsweredCount();
            Post("progress", new { answered, total, text = $"{answered} / {total}" });
        }

        private int FindNextUnanswered(int fromQIndex)
        {
            for (int i = fromQIndex; i <= _questions.Count; i++)
            {
                if (!IsAnswered(i)) return i;
            }

            for (int i = 1; i < fromQIndex; i++)
            {
                if (!IsAnswered(i)) return i;
            }

            return 0;
        }

        private bool IsAnswered(int qIndex)
        {
            if (_dontKnow.Contains(qIndex)) return true;
            if (_pickedIndexByQ.TryGetValue(qIndex, out var v) && v.HasValue) return true;
            return false;
        }

        private bool IsCorrect(int qIndex)
        {
            if (_dontKnow.Contains(qIndex)) return false;
            if (!_pickedIndexByQ.TryGetValue(qIndex, out var picked) || !picked.HasValue) return false;

            var q = _questions.FirstOrDefault(x => x.Index == qIndex);
            if (q == null) return false;

            string correct = (q.CorrectAnswer ?? "").Trim();
            string pickedText = (q.Choices[picked.Value] ?? "").Trim();
            return string.Equals(pickedText, correct, StringComparison.Ordinal);
        }

        private void ApplySrsFromMultipleChoiceResult()
        {
            if (_srsApplied || _set == null)
                return;

            foreach (var q in _questions)
            {
                Services.SpacedRepetitionService.ApplyReview(_set, q.CardKey, IsCorrect(q.Index));
            }

            _srsApplied = true;
        }
    }
}
