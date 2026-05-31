#nullable enable

using System;
using System.Drawing;
using System.Windows.Forms;

namespace TocflQuiz.Forms
{
    public sealed partial class CardFormWeb
    {
        private void ToggleFullScreen()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastFullScreenToggleUtc).TotalMilliseconds < 250)
                return;

            _lastFullScreenToggleUtc = now;

            if (_isFullScreen)
                ExitFullScreen();
            else
                EnterFullScreen();
        }

        private void EnterFullScreen()
        {
            if (_isFullScreen)
                return;

            _restoreBorderStyle = FormBorderStyle;
            _restoreWindowState = WindowState;
            _restoreBounds = Bounds;
            _isFullScreen = true;

            SuspendLayout();
            try
            {
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Normal;
                Bounds = Screen.FromControl(this).Bounds;
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        private void ExitFullScreen()
        {
            if (!_isFullScreen)
                return;

            _isFullScreen = false;

            SuspendLayout();
            try
            {
                FormBorderStyle = _restoreBorderStyle;
                WindowState = FormWindowState.Normal;
                Bounds = _restoreBounds == Rectangle.Empty ? Screen.FromControl(this).WorkingArea : _restoreBounds;
                WindowState = _restoreWindowState;
                ApplyDarkTitleBar();
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        private sealed class FullscreenKeyMessageFilter : IMessageFilter
        {
            private const int WmKeyDown = 0x0100;
            private const int WmKeyUp = 0x0101;
            private const int WmSysKeyDown = 0x0104;
            private const int WmSysKeyUp = 0x0105;

            private readonly WeakReference<CardFormWeb> _owner;
            private bool _f11Down;

            public FullscreenKeyMessageFilter(CardFormWeb owner)
            {
                _owner = new WeakReference<CardFormWeb>(owner);
            }

            public bool PreFilterMessage(ref Message m)
            {
                if (!_owner.TryGetTarget(out var owner) || owner.IsDisposed)
                    return false;

                if (m.Msg != WmKeyDown && m.Msg != WmSysKeyDown && m.Msg != WmKeyUp && m.Msg != WmSysKeyUp)
                    return false;

                if ((Keys)m.WParam.ToInt32() != Keys.F11)
                    return false;

                if (m.Msg == WmKeyUp || m.Msg == WmSysKeyUp)
                {
                    _f11Down = false;
                    return true;
                }

                if (_f11Down)
                    return true;

                _f11Down = true;

                if (owner.Visible && owner.WindowState != FormWindowState.Minimized)
                {
                    owner.ToggleFullScreen();
                    return true;
                }

                return false;
            }
        }
    }
}
