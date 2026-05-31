using System;
using System.IO;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace TocflQuiz.Services
{
    public static class WebViewAssetService
    {
        public const string LocalHostName = "app";
        public const string DatasetHostName = "appdata";

        public static void ConfigureLocalContent(WebView2 webView)
        {
            if (webView.CoreWebView2 == null)
                throw new InvalidOperationException("WebView2 must be initialized before mapping local content.");

            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                LocalHostName,
                AppDomain.CurrentDomain.BaseDirectory,
                CoreWebView2HostResourceAccessKind.Allow);

            TryMapFolder(webView, DatasetHostName, CardSetStorage.BaseDir);
        }

        public static void NavigateToPage(WebView2 webView, string relativePath)
        {
            EnsureAssetExists(relativePath);

            var uri = GetAssetUri(relativePath);
            if (webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.Navigate(uri);
                return;
            }

            webView.Source = new Uri(uri);
        }

        public static string GetAssetUri(string relativePath)
        {
            var normalized = NormalizeRelativePath(relativePath);
            return $"https://{LocalHostName}/{normalized}";
        }

        public static string GetDatasetAssetUri(string relativePath)
        {
            var normalized = NormalizeRelativePath(relativePath);
            return $"https://{DatasetHostName}/{normalized}";
        }

        public static string GetLocalFileAssetUri(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
                return "";

            var appRelative = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, absolutePath);
            if (!IsOutsideBase(appRelative))
                return GetAssetUri(appRelative.Replace('\\', '/'));

            var datasetRelative = Path.GetRelativePath(CardSetStorage.BaseDir, absolutePath);
            if (!IsOutsideBase(datasetRelative))
                return GetDatasetAssetUri(datasetRelative.Replace('\\', '/'));

            return new Uri(absolutePath).AbsoluteUri;
        }

        public static bool IsLocalContentUri(string? uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return false;

            return uri.StartsWith($"https://{LocalHostName}/", StringComparison.OrdinalIgnoreCase) ||
                   uri.StartsWith($"https://{DatasetHostName}/", StringComparison.OrdinalIgnoreCase);
        }

        public static string EscapeJavaScriptString(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            return text.Replace("\\", "\\\\")
                       .Replace("'", "\\'")
                       .Replace("\"", "\\\"")
                       .Replace("\n", "\\n")
                       .Replace("\r", "\\r");
        }

        private static void EnsureAssetExists(string relativePath)
        {
            var normalized = NormalizeRelativePath(relativePath);
            var filePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                normalized.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Missing webview asset.", filePath);
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("Asset path is required.", nameof(relativePath));

            return relativePath.Replace('\\', '/').TrimStart('/');
        }

        private static void TryMapFolder(WebView2 webView, string hostName, string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);

                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    hostName,
                    folder,
                    CoreWebView2HostResourceAccessKind.Allow);
            }
            catch
            {
            }
        }

        private static bool IsOutsideBase(string relativePath)
        {
            return string.IsNullOrWhiteSpace(relativePath) ||
                   relativePath.Equals("..", StringComparison.Ordinal) ||
                   relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                   relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal) ||
                   Path.IsPathRooted(relativePath);
        }
    }
}
