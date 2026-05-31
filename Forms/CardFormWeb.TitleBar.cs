#nullable enable

using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TocflQuiz.Forms
{
    public sealed partial class CardFormWeb
    {
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
        private const int DwmwaCaptionColor = 35;
        private const int DwmwaTextColor = 36;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDarkTitleBar();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            ApplyDarkTitleBar();
        }

        private void ApplyDarkTitleBar()
        {
            if (!OperatingSystem.IsWindows())
                return;

            try
            {
                var enabled = 1;
                _ = DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
                _ = DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));

                var captionColor = ColorTranslator.ToWin32(Color.Black);
                _ = DwmSetWindowAttribute(Handle, DwmwaCaptionColor, ref captionColor, sizeof(int));

                var textColor = ColorTranslator.ToWin32(Color.White);
                _ = DwmSetWindowAttribute(Handle, DwmwaTextColor, ref textColor, sizeof(int));
            }
            catch
            {
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref int pvAttribute,
            int cbAttribute);
    }
}
