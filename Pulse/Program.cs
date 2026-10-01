using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace TaskManagerPro
{
    /// <summary>
    /// نقطه‌ی ورود برنامه با «محافظت از اجرای چندباره» (Launch protection).
    ///
    /// اگر Pulse از قبل باز باشد، اجرای دوم هیچ پنجره‌ی جدیدی نمی‌سازد: فعال‌سازی به
    /// نمونه‌ی اول هدایت می‌شود، پنجره‌ی موجود جلو می‌آید و این پروسه بلافاصله بسته می‌شود.
    /// (رجیستر کردن کلید نمونه در سطح ویندوز اتمیک است، پس حتی دو کلیک پشت‌سرهم هم
    /// دو نسخه باز نمی‌کند.)
    /// </summary>
    public static class Program
    {
        /// <summary>true یعنی ویندوز برنامه را هنگام بوت (StartupTask) اجرا کرده است</summary>
        public static bool LaunchedAtStartup { get; private set; }

        [STAThread]
        private static int Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            if (RedirectToRunningInstance()) return 0;

            Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });

            return 0;
        }

        /// <summary>اگر نمونه‌ی دیگری در حال اجراست، فعال‌سازی را به آن بفرست و true برگردان</summary>
        private static bool RedirectToRunningInstance()
        {
            try
            {
                var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
                LaunchedAtStartup = activation?.Kind == ExtendedActivationKind.StartupTask;

                const string key = "PulseMainInstance";
                var main = AppInstance.FindOrRegisterForKey(key);

                // اجرای دوباره (مثلاً با دسترسی ادمین): تا بسته شدن نمونه‌ی قبلی صبر کن
                if (!main.IsCurrent && Array.IndexOf(Environment.GetCommandLineArgs(), "--relaunch") >= 0)
                {
                    for (int i = 0; i < 25 && !main.IsCurrent; i++)
                    {
                        Thread.Sleep(200);
                        main = AppInstance.FindOrRegisterForKey(key);
                    }
                }
                if (main.IsCurrent)
                {
                    // اجراهای بعدی به این نمونه هدایت می‌شوند
                    main.Activated += OnActivatedFromOtherInstance;
                    return false;
                }

                // هدایت باید خارج از ترد UI انجام شود تا قفل نکند
                var done = new ManualResetEvent(false);
                var thread = new Thread(() =>
                {
                    try { main.RedirectActivationToAsync(activation).AsTask().GetAwaiter().GetResult(); }
                    catch { }
                    finally { done.Set(); }
                });
                thread.IsBackground = true;
                thread.Start();
                done.WaitOne(TimeSpan.FromSeconds(5));
                return true;
            }
            catch
            {
                // اگر AppLifecycle در دسترس نبود، برنامه مثل قبل عادی بالا می‌آید
                return false;
            }
        }

        private static void OnActivatedFromOtherInstance(object? sender, AppActivationArguments e)
        {
            var window = App.MainAppWindow;
            window?.DispatcherQueue.TryEnqueue(() =>
            {
                try { Helpers.TrayManager.ShowWindowNow(); } catch { }
            });
        }
    }
}
