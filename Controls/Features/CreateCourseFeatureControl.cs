using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class CreateCourseFeatureControl : UserControl
    {
        private WebCardImportControl? _webImport;

        public event Action<CardSet?>? ImportCompleted;

        // Theme
        private static readonly Color Bg = Color.FromArgb(246, 247, 251);

        public CreateCourseFeatureControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Bg;
            Font = new Font("Segoe UI", 9F);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            UpdateStyles();

            // ✅ Mở luôn WebCardImportControl khi khởi tạo
            ShowImportForm();
        }

        private void ShowImportForm()
        {
            // Dọn dẹp nếu có
            if (_webImport != null)
            {
                try
                {
                    _webImport.ImportCompleted -= OnImportCompleted;
                    if (Controls.Contains(_webImport))
                        Controls.Remove(_webImport);
                    _webImport.Dispose();
                }
                catch { }
                _webImport = null;
            }

            // Tạo mới WebCardImportControl
            _webImport = new WebCardImportControl
            {
                Dock = DockStyle.Fill
            };

            _webImport.ImportCompleted += OnImportCompleted;

            Controls.Clear();
            Controls.Add(_webImport);
            _webImport.BringToFront();
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            _webImport?.FocusEditor();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);

            if (Visible && IsHandleCreated)
                BeginInvoke(new Action(() => _webImport?.FocusEditor()));
        }

        private void OnImportCompleted(object? sender, EventArgs e)
        {
            if (_webImport == null) return;

            var ok = _webImport.DialogResult == DialogResult.OK;

            // Lấy set mới nhất nếu import thành công
            CardSet? newest = null;
            if (ok)
            {
                newest = CardSetStorage.LoadAllSetsSafe().FirstOrDefault();
            }

            // Thông báo cho CardForm
            ImportCompleted?.Invoke(newest);
        }
        public void SetDarkMode(bool dark)
        {
            // đổi nền control host (optional)
            BackColor = dark ? Color.FromArgb(30, 30, 40) : Bg;

            // forward xuống WebCardImportControl
            _webImport?.SetDarkMode(dark);
        }

    }
}
