#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TocflQuiz.Models;

namespace TocflQuiz.Controls.Features.Quiz
{
    public sealed partial class QuizEssayControlWeb
    {
        private async Task RenderQuestionAsync(int index)
        {
            if (_questions.Count == 0)
            {
                await PushEmptyStateAsync(_dayTitle ?? "", "(Chọn học phần và bấm Bắt đầu)");
                return;
            }

            index = Math.Max(0, Math.Min(index, _questions.Count - 1));
            _currentIndex = index;

            var q = _questions[index];
            var state = _states[index];
            bool answerIsChinese = q.UseChineseFontForChoices;

            ProgressChanged?.Invoke(index + 1, _questions.Count);

            var dto = new HostToUiMessage
            {
                Type = "question",
                Theme = new UiTheme { IsDark = _isDarkMode, TcFont = TcPrimaryFontName },
                Question = new UiQuestionState
                {
                    TopProgress = $"{index + 1} / {_questions.Count}",
                    DayTitle = _dayTitle ?? (_set?.Title ?? ""),
                    SmallLabel = q.SmallLabel ?? "",
                    QNum = $"{q.Index}/{q.Total}",
                    Prompt = q.UseChineseFontForQuestion
                        ? (q.QuestionText ?? "")
                        : StripDefinitionForAnswer(q.QuestionText, null),
                    PromptIsChinese = q.UseChineseFontForQuestion,
                    AnswerIsChinese = answerIsChinese,
                    Placeholder = answerIsChinese ? PH_ZH : PH_VI,
                    UserAnswer = state.UserAnswer ?? "",
                    CanPrevious = index > 0,
                    ButtonNextText = (index == _questions.Count - 1) ? BTN_SUBMIT : BTN_NEXT,
                    Tokens = answerIsChinese ? _tokensZh : _tokensVi
                }
            };

            await PostToWebAsync(dto);
        }

        private void SkipCurrent()
        {
            if (_submitted) return;

            var st = _states[_currentIndex];
            st.Skipped = true;
            st.UserAnswer = null;
            st.IsCorrect = false;

            GoNextOrSubmit();
        }

        private void PreviousQuestion(string? currentText)
        {
            if (_submitted || _currentIndex <= 0) return;

            var st = _states[_currentIndex];
            var draft = (currentText ?? "").Trim();
            st.UserAnswer = string.IsNullOrWhiteSpace(draft) ? null : draft;
            st.Skipped = string.IsNullOrWhiteSpace(draft);

            _ = RenderQuestionAsync(_currentIndex - 1);
        }

        private void SubmitCurrent(string userText)
        {
            if (_submitted) return;

            var q = _questions[_currentIndex];
            var st = _states[_currentIndex];

            var user = (userText ?? "").Trim();
            if (string.IsNullOrWhiteSpace(user))
            {
                st.Skipped = true;
                st.UserAnswer = null;
                st.IsCorrect = false;
            }
            else
            {
                st.Skipped = false;
                st.UserAnswer = user;
                st.IsCorrect = IsAnswerCorrect(q, user);
            }

            GoNextOrSubmit();
        }

        private void GoNextOrSubmit()
        {
            if (_currentIndex < _questions.Count - 1)
            {
                _ = RenderQuestionAsync(_currentIndex + 1);
                return;
            }

            _submitted = true;
            _awaitingGradeChoice = true;
            _ = ShowGradeChoiceAsync();
        }

        private void RebuildTokenPoolsFromSet(CardSet set)
        {
            _tokensZh.Clear();
            _tokensVi.Clear();

            var zh = new HashSet<string>(StringComparer.Ordinal);
            var vi = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

            if (set.Items != null)
            {
                foreach (var it in set.Items)
                {
                    var term = (it?.Term ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(term))
                    {
                        foreach (var ch in term)
                        {
                            if (!char.IsWhiteSpace(ch))
                                zh.Add(ch.ToString());
                        }
                    }

                    var def = StripDefinitionForAnswer(it?.Definition, it?.Pinyin);
                    if (!string.IsNullOrWhiteSpace(def))
                    {
                        foreach (var w in def.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var word = w.Trim();
                            if (word.Length > 0) vi.Add(word);
                        }
                    }
                }
            }

            _tokensZh.AddRange(zh);
            _tokensVi.AddRange(vi);

            Shuffle(_tokensZh);
            Shuffle(_tokensVi);
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
