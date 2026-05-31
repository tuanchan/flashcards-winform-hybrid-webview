using System;
using System.Collections.Generic;
using System.Windows.Forms;
using TocflQuiz.Models;
using TocflQuiz.Services;
using TocflQuiz.Forms;
using System.IO;
using System.Linq; // cần cho .Take(5)

namespace TocflQuiz
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            var context = new ApplicationContext();
            var splash = new SplashForm();
            splash.Show();

            splash.Shown += async (s, e) =>
            {
                var startTime = DateTime.Now;

                // 1. Load config in background
                var cfg = await System.Threading.Tasks.Task.Run(() =>
                {
                    var c = AppConfig.LoadFromAppsettings();
                    c.EnsureAppDataDir();
                    var userSettings = SettingsService.Load();
                    if (!string.IsNullOrWhiteSpace(userSettings.DatasetRoot))
                    {
                        c.DatasetRoot = userSettings.DatasetRoot;
                    }
                    return c;
                });

                splash.UpdateStatus("Đang tải dữ liệu khóa học...");

                // 2. Scan content in background
                var groups = await System.Threading.Tasks.Task.Run(() =>
                {
                    var scanner = new ContentScanner();
                    return scanner.ScanAll(cfg);
                });

                splash.UpdateStatus("Đang đồng bộ tiến độ...");

                // 3. Load progress in background
                var store = new ProgressStoreJson(cfg);
                var progressMap = await System.Threading.Tasks.Task.Run(() => store.Load());
                var sr = new SpacedRepetition(cfg.ReviewIntervalsDays);

                splash.UpdateStatus("Đang khởi tạo giao diện chính...");

                // Create main form but keep it invisible
                var mainForm = new CardFormWeb(cfg, groups, progressMap, store, sr);
                mainForm.Opacity = 0;
                mainForm.ShowInTaskbar = false;
                
                // Hook up the close event to exit the app
                mainForm.FormClosed += (_, __) => context.ExitThread();

                // When AppReady fires, wait for 5s total, then show mainForm
                CardFormWeb.AppReady += async () =>
                {
                    var elapsed = DateTime.Now - startTime;
                    if (elapsed.TotalSeconds < 5)
                    {
                        await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5) - elapsed);
                    }

                    // Give WebView2 a tiny extra moment to render its first frame
                    await System.Threading.Tasks.Task.Delay(200);

                    mainForm.Opacity = 1;
                    mainForm.ShowInTaskbar = true;
                    mainForm.Activate();

                    splash.Close();
                };

                // Show mainForm invisibly to trigger its Load event and WebView2 initialization
                mainForm.Show();
            };

            Application.Run(context);

        }
    }
}
