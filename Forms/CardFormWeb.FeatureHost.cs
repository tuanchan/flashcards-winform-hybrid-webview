#nullable enable

using System;
using System.Drawing;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using TocflQuiz.Controls.Features;
using TocflQuiz.Controls.Features.Quiz;
using TocflQuiz.Models;

namespace TocflQuiz.Forms
{
    public sealed partial class CardFormWeb
    {
        private void BuildWinHostOverlay()
        {
            _winTopBar.Dock = DockStyle.Top;
            _winTopBar.Height = 68;
            _winTopBar.BackColor = Color.FromArgb(40, 40, 50);

            _btnBack.Text = "← Trang chủ";
            _btnBack.Width = 140;
            _btnBack.Height = 36;
            _btnBack.Left = 14;
            _btnBack.Top = 16;
            _btnBack.FlatStyle = FlatStyle.Flat;
            _btnBack.FlatAppearance.BorderSize = 0;
            _btnBack.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnBack.FlatAppearance.MouseDownBackColor = Color.Transparent;
            _btnBack.BackColor = Color.Transparent;
            _btnBack.ForeColor = Color.White;
            _btnBack.Cursor = Cursors.Hand;
            _btnBack.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            _btnBack.Click += (_, __) => BackToWebHome();

            _winTopBar.Controls.Clear();
            _winTopBar.Controls.Add(_btnBack);

            _winHost.Dock = DockStyle.Fill;
            _winHost.BackColor = Color.FromArgb(30, 30, 40);
            _winHost.Visible = true; // Always visible, managed by z-order
            _winHost.SendToBack();

            _winContent.Dock = DockStyle.Fill;
            _winContent.BackColor = Color.Transparent;

            _winHost.Controls.Clear();
            _winHost.Controls.Add(_winContent);

            if (!Controls.Contains(_winHost))
                Controls.Add(_winHost);
        }

        private void CloseWinHostViewsKeepToast()
        {
            // We keep views visible to avoid WebView2 black flash, 
            // but we hide the entire host
            _currentWinView = null;
        }

        private void ShowWinHost(UserControl view)
        {
            _currentWinView = view;

            _winContent.SuspendLayout();
            try
            {
                if (!_winContent.Controls.Contains(view))
                {
                    view.Dock = DockStyle.Fill;
                    _winContent.Controls.Add(view);
                }

                view.BringToFront();
            }
            finally
            {
                _winContent.ResumeLayout(true);
            }

            _winHost.BringToFront();

            BeginInvoke(new Action(() =>
            {
                try
                {
                    ActiveControl = view;
                    view.Select();
                    view.Focus();
                }
                catch { }
            }));
        }

        private void ShowFlashcardsWinForms()
        {
            if (_selectedSet == null)
            {
                SendAlert("Bạn chưa chọn học phần.");
                return;
            }

            if (_flashcardsView == null)
                _flashcardsView = new FlashcardsFeatureControlWeb();

            _flashcardsView.LoadSet(_selectedSet);
            TryCallSetDarkMode(_flashcardsView, _isDarkMode);
            ShowWinHost(_flashcardsView);
        }

        private void ShowQuizWinForms()
        {
            if (_selectedSet == null)
            {
                SendAlert("Bạn chưa chọn học phần.");
                return;
            }

            if (_quizView == null)
            {
                _quizView = new QuizFeatureControlWeb();
                _quizView.ExitToCourseListRequested += () => BackToWebHome();
                _quizView.EssayModeRequested += (set, cfg) => ShowEssayQuizWinForms(set, cfg);
            }

            _quizView.BindSelectedSet(_selectedSet);
            _quizView.SetDarkMode(_isDarkMode);
            ShowWinHost(_quizView);
        }

        private void ShowEssayQuizWinForms(CardSet set, QuizConfig cfg)
        {
            if (_essayView == null)
            {
                _essayView = new QuizEssayControlWeb();
                _essayView.ExitRequested += () => BackToWebHome();
            }

            _essayView.SetDarkMode(_isDarkMode);
            _essayView.BindSelectedSet(set, cfg.AnswerMode, cfg.Count, set.Title);
            ShowWinHost(_essayView);
        }

        private void ShowDialogueWinForms()
        {
            if (_dialogueView == null)
            {
                _dialogueView = new DialogueFeatureControlWeb();
                _dialogueView.ExitRequested += () => BackToWebHome();
            }

            _dialogueView.BindCourses(_allSets, _selectedSet);
            _dialogueView.SetDarkMode(_isDarkMode);
            ShowWinHost(_dialogueView);
        }

        private void ShowCreateCourseWinForms(string? topicId = null)
        {
            if (_createCourseView == null)
            {
                _createCourseView = new CreateCourseFeatureControl();
                _createCourseView.ImportCompleted += newest =>
                {
                    ReloadAndSendCourses();
                    BackToWebHome();

                    if (newest != null)
                    {
                        _selectedSet = newest;
                        _toastScheduler?.NotifySelectedSetChanged();

                        var info = new
                        {
                            id = newest.Id ?? "",
                            title = newest.Title ?? "",
                            count = newest.VocabCount > 0 ? newest.VocabCount : (newest.Items?.Count ?? 0)
                        };

                        ExecuteScript($"if(window.autoSelectCourse) window.autoSelectCourse({JsonSerializer.Serialize(info)});");
                    }
                };
            }

            _createCourseView.SetDefaultTopicId(topicId);
            TryCallSetDarkMode(_createCourseView, _isDarkMode);
            ShowWinHost(_createCourseView);
        }

        private async void BeginPrewarmFeatureViews()
        {
            if (_prewarmStarted || IsDisposed) return;
            _prewarmStarted = true;

            try
            {
                await Task.Delay(450);
                if (IsDisposed) return;

                if (_flashcardsView == null)
                    _flashcardsView = new FlashcardsFeatureControlWeb();

                if (_quizView == null)
                {
                    _quizView = new QuizFeatureControlWeb();
                    _quizView.ExitToCourseListRequested += () => BackToWebHome();
                    _quizView.EssayModeRequested += (set, cfg) => ShowEssayQuizWinForms(set, cfg);
                }

                if (_essayView == null)
                {
                    _essayView = new QuizEssayControlWeb();
                    _essayView.ExitRequested += () => BackToWebHome();
                }

                if (_dialogueView == null)
                {
                    _dialogueView = new DialogueFeatureControlWeb();
                    _dialogueView.ExitRequested += () => BackToWebHome();
                }

                if (_createCourseView == null)
                {
                    _createCourseView = new CreateCourseFeatureControl();
                    _createCourseView.ImportCompleted += newest =>
                    {
                        ReloadAndSendCourses();
                        BackToWebHome();

                        if (newest != null)
                        {
                            _selectedSet = newest;
                            _toastScheduler?.NotifySelectedSetChanged();

                            var info = new
                            {
                                id = newest.Id ?? "",
                                title = newest.Title ?? "",
                                count = newest.VocabCount > 0 ? newest.VocabCount : (newest.Items?.Count ?? 0)
                            };

                            ExecuteScript($"if(window.autoSelectCourse) window.autoSelectCourse({JsonSerializer.Serialize(info)});");
                        }
                    };
                }

                var views = new UserControl[]
                {
                    _flashcardsView,
                    _quizView,
                    _essayView,
                    _dialogueView,
                    _createCourseView
                };

                foreach (var view in views)
                {
                    if (IsDisposed) return;
                    if (view.Parent != null) continue;

                    view.Dock = DockStyle.Fill;
                    _winContent.Controls.Add(view);
                    view.SendToBack(); // push to back instead of hiding
                    view.CreateControl();
                    await Task.Delay(50); // Small delay to avoid blocking UI thread too much
                }

                AppReady?.Invoke();
            }
            catch
            {
                // Prewarm is only a performance hint; never block the app if it fails.
            }
        }

        private static void TryCallSetDarkMode(Control ctrl, bool dark)
        {
            try
            {
                var mi = ctrl.GetType().GetMethod(
                    "SetDarkMode",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (mi != null && mi.GetParameters().Length == 1)
                    mi.Invoke(ctrl, new object[] { dark });
            }
            catch { }
        }

        private void RegisterFeatureForm(Form f)
        {
            _openedFeatureForms.Add(f);

            f.FormClosed += (_, __) =>
            {
                _openedFeatureForms.Remove(f);
            };
        }

        private void CloseAllFeatureFormsKeepToast()
        {
            for (int i = _openedFeatureForms.Count - 1; i >= 0; i--)
            {
                try
                {
                    _openedFeatureForms[i].Close();
                }
                catch { }
            }

            _openedFeatureForms.Clear();
        }

        private Panel? GetWinHostContentPanel()
        {
            foreach (Control c in _winHost.Controls)
            {
                if (c is Panel p && p.Dock == DockStyle.Fill)
                    return p;
            }

            return null;
        }
    }
}
