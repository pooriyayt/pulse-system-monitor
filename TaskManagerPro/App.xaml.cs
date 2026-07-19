using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TaskManagerPro.Helpers;
using TaskManagerPro.Monitoring;

namespace TaskManagerPro
{
    /// <summary>
    /// نقطه‌ی شروع برنامه. اولین کدی که اجرا می‌شود همین‌جاست.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>دسترسی سراسری به پنجره‌ی اصلی (برای تعویض تم و ...)</summary>
        public static Window? MainAppWindow { get; private set; }

        /// <summary>مدیر آیکون System Tray و هات‌کی سراسری</summary>
        public static TrayManager? Tray { get; private set; }

        /// <summary>وقتی true شود یعنی کاربر واقعاً خروج زده (نه مخفی شدن در Tray)</summary>
        public static bool IsExiting;

        public App()
        {
            this.InitializeComponent();

            // خواندن تنظیمات ذخیره‌شده (تم، رنگ گراف، سرعت رفرش، Tray و ...)
            AppSettings.Load();
        }

        /// <summary>ویجت شناور دسکتاپ (اگر فعال باشد)</summary>
        private static Views.WidgetWindow? _widget;

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainAppWindow = new MainWindow();
            MainAppWindow.Activate();

            // اعمال تم و تنظیمات ذخیره‌شده
            ThemeManager.Apply(MainAppWindow);
            ThemeManager.ApplyAlwaysOnTop(MainAppWindow);
            L10n.ApplyDirection(MainAppWindow);
            AppSettings.LanguageChanged += () => L10n.ApplyDirection();

            // نمونه‌برداری سراسری تاریخچه (گراف ۱۰ دقیقه/۱ ساعت + آلارم مصرف + mini-گراف Tray)
            Monitoring.HistoryStore.Start(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());

            // شروع زودهنگام سنسورهای سخت‌افزار تا دمای CPU از همان ابتدا نمایش داده شود
            _ = System.Threading.Tasks.Task.Run(SensorMonitor.Start);

            // ویجت شناور دسکتاپ
            ApplyWidget();
            AppSettings.WidgetChanged += ApplyWidget;

            // مانیتور مصرف شبکهی هر پردازه (فقط با Run as administrator فعال می‌شود)
            try { Monitoring.NetworkMonitor.Instance.Start(); } catch { }

            // چک خودکار آپدیت — کاملاً در پس‌زمینه؛ نبود اینترنت/سرور هیچ اثری ندارد
            _ = AutoCheckUpdatesAsync();

            // ==== System Tray + هات‌کی سراسری ====
            Tray = new TrayManager();
            Tray.ApplySettings();
            AppSettings.TraySettingsChanged += () => Tray?.ApplySettings();

            var aw = MainAppWindow.AppWindow;

            // دکمه‌ی Close: اگر Tray فعال باشد، به جای بسته شدن فقط مخفی می‌شود
            aw.Closing += (s, e) =>
            {
                if (AppSettings.TrayEnabled && !IsExiting)
                {
                    e.Cancel = true;
                    s.Hide();
                }
            };

            // Minimize: اگر Tray فعال باشد، به جای تسک‌بار داخل Tray می‌رود
            aw.Changed += (s, e) =>
            {
                try
                {
                    if (AppSettings.TrayEnabled &&
                        s.Presenter is OverlappedPresenter p &&
                        p.State == OverlappedPresenterState.Minimized)
                    {
                        s.Hide();
                    }
                }
                catch { }
            };
        }

        /// <summary>
        /// چک خودکار آپدیت هنگام باز شدن برنامه — بی‌صدا در پس‌زمینه.
        /// اگر نسخه‌ی جدید بود دانلود می‌کند و از کاربر می‌خواهد نصب کند؛
        /// هر خطایی (آفلاین، سرور خراب) بی‌سروصدا نادیده گرفته می‌شود.
        /// </summary>
        private static async System.Threading.Tasks.Task AutoCheckUpdatesAsync()
        {
            try
            {
                // چند ثانیه صبر تا استارت برنامه سبک بماند
                await System.Threading.Tasks.Task.Delay(5000);

                var info = await UpdateChecker.CheckAsync();
                if (info is not { UpdateAvailable: true }) return;

                string? path = await UpdateChecker.DownloadAsync(info);
                if (path == null) return;

                MainAppWindow?.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        if (MainAppWindow?.Content is not FrameworkElement root) return;
                        var dlg = new Microsoft.UI.Xaml.Controls.ContentDialog
                        {
                            Title = L10n.T("Update ready"),
                            Content = string.Format(
                                L10n.T("Version {0} has been downloaded. Install it now to update Pulse."),
                                info.LatestVersion),
                            PrimaryButtonText = L10n.T("Install"),
                            CloseButtonText = L10n.T("Later"),
                            DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
                            XamlRoot = root.XamlRoot,
                        };
                        if (await dlg.ShowAsync() == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                        {
                            System.Diagnostics.Process.Start(
                                new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        private static void ApplyWidget()
        {
            if (AppSettings.WidgetEnabled)
            {
                if (_widget == null)
                {
                    _widget = new Views.WidgetWindow();
                    _widget.Closed += (s, e) => _widget = null;
                }
                _widget.Activate();
            }
            else
            {
                try { _widget?.Close(); } catch { }
                _widget = null;
            }
        }
    }
}
