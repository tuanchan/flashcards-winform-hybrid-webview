#nullable enable

using Microsoft.Web.WebView2.Core;
using System.Windows.Forms;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class QuizFeatureControlWeb
    {
        private void Web_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
        {
            e.IsInputKey = true;
        }

        private void Web_KeyDown(object? sender, KeyEventArgs e)
        {
            try
            {
                var f = FindForm();
                if (f == null) return;

                if (!Visible || f.WindowState == FormWindowState.Minimized || !ContainsFocus)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
            catch { }
        }

        private void Web_KeyUp(object? sender, KeyEventArgs e)
        {
            try
            {
                var f = FindForm();
                if (f == null) return;

                if (!Visible || f.WindowState == FormWindowState.Minimized || !ContainsFocus)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
            catch { }
        }

        private void CoreWebView2_AcceleratorKeyPressed(object? sender, CoreWebView2AcceleratorKeyPressedEventArgs e)
        {
            try
            {
                var f = FindForm();
                if (f == null) return;

                if (!Visible || f.WindowState == FormWindowState.Minimized || !ContainsFocus)
                {
                    e.Handled = true;
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
