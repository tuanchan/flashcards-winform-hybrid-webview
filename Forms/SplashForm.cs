using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    public partial class SplashForm : Form
    {
        private readonly WebView2 _web = new();
        private bool _isReady = false;

        // Windows API for rounded corners (Windows 11)
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        internal static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        // Windows API for acrylic blur
        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4, // Windows 10 RS4+
            ACCENT_INVALID_STATE = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor; // format: AABBGGRR
            public int AnimationId;
        }

        public SplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(800, 500);
            ShowInTaskbar = false;
            TopMost = true;

            // Set a dark background before WebView loads
            BackColor = Color.Black; 

            EnableBlur();
            EnableRoundedCorners();

            _web.Dock = DockStyle.Fill;
            _web.DefaultBackgroundColor = Color.Transparent;
            Controls.Add(_web);

            Load += SplashForm_Load;
        }

        private void EnableRoundedCorners()
        {
            try
            {
                int preference = DWMWCP_ROUND;
                DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            }
            catch { }
        }

        private void EnableBlur()
        {
            try
            {
                var accent = new AccentPolicy
                {
                    AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                    GradientColor = (200 << 24) | (0 << 16) | (0 << 8) | 0 // Mostly opaque black
                };

                int accentStructSize = Marshal.SizeOf(accent);
                IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    SizeOfData = accentStructSize,
                    Data = accentPtr
                };

                SetWindowCompositionAttribute(this.Handle, ref data);
                Marshal.FreeHGlobal(accentPtr);
            }
            catch { }
        }

        private async void SplashForm_Load(object? sender, EventArgs e)
        {
            try
            {
                string userData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FlashCards",
                    "WebView2_Splash"); // Use separate folder for splash to avoid locks
                Directory.CreateDirectory(userData);
                
                var env = await CoreWebView2Environment.CreateAsync(null, userData);
                await _web.EnsureCoreWebView2Async(env);
                
                _web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                
                WebViewAssetService.ConfigureLocalContent(_web);
                WebViewAssetService.NavigateToPage(_web, "Webviews/splash.html");

                _web.NavigationCompleted += (s, ev) => 
                {
                    _isReady = true;
                };
            }
            catch { }
        }

        public void UpdateStatus(string text)
        {
            if (!_isReady || _web.CoreWebView2 == null) return;
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(new { text });
                _web.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        }
    }
}
