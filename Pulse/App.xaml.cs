using System;
﻿using Microsoft.UI.Windowing;
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

        /// <summary>رویداد تغییر وضعیت دیده‌شدن پنجره اصلی (برای خاموش کردن رندر گراف‌ها و تایمرها در پس‌زمینه)</summary>
        public static event Action<bool>? WindowVisibilityChanged;
        public static bool IsWindowVisible { get; private set; } = true;

        public static void SetWindowVisibility(bool visible)
        {
            if (IsWindowVisible == visible) return;
            IsWindowVisible = visible;
            try { WindowVisibilityChanged?.Invoke(visible); } catch { }
        }

        /// <summary>مدیر آیکون System Tray و هات‌کی سراسری</summary>
        public static TrayManager? Tray { get; private set; }

        /// <summary>وقتی true شود یعنی کاربر واقعاً خروج زده (نه مخفی شدن در Tray)</summary>
        public static bool IsExiting;

        public App()
        {
            this.InitializeComponent();

            // ثبت خطاهای پیش‌بینی‌نشده‌ی UI در فایل لاگ (برای عیب‌یابی)
            UnhandledException += (_, e) =>
            {
                try
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Pulse-crash.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}{Environment.NewLine}{Environment.NewLine}");
                }
                catch { }
            };

            // خواندن تنظیمات ذخیره‌شده (تم، رنگ گراف، سرعت رفرش، Tray و ...)
            AppSettings.Load();
        }

        /// <summary>ویجت شناور دسکتاپ (اگر فعال باشد)</summary>
        private static Views.WidgetWindow? _widget;

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // ظاهر رابط: جدید (مدرن) یا کلاسیک — از تنظیمات
            AppSettings.ModernUi = AppSettings.UiStyle == 0;
            if (AppSettings.ModernUi)
            {
                // گوشه‌های گردتر فقط در ظاهر جدید
                Resources["ControlCornerRadius"] = new CornerRadius(10);
                Resources["OverlayCornerRadius"] = new CornerRadius(16);
                Resources["ListViewItemMinHeight"] = 44.0;
                MainAppWindow = new MainWindow();
            }
            else
            {
                MainAppWindow = new Classic.ClassicMainWindow();
            }
            MainAppWindow.Activate();

            // اگر ویندوز برنامه را هنگام بوت اجرا کرده و Tray فعال است،
            // بی‌سروصدا در Tray بماند و پنجره جلوی چشم کاربر باز نشود.
            if (Program.LaunchedAtStartup && AppSettings.TrayEnabled)
            {
                try { MainAppWindow.AppWindow.Hide(); SetWindowVisibility(false); } catch { }
            }

            // اعمال تم و تنظیمات ذخیره‌شده
            ThemeManager.Apply(MainAppWindow);
            ThemeManager.ApplyAlwaysOnTop(MainAppWindow);
            L10n.ApplyDirection(MainAppWindow);
            AppSettings.LanguageChanged += () => L10n.ApplyDirection();

            // ساخت شمارنده‌های سیستم از همین ابتدا در پس‌زمینه — تا هیچ صفحه‌ای مجبور
            // نشود آن‌ها را روی ترد UI بسازد (دلیل فریز شدن پنجره هنگام باز شدن)
            _ = Monitoring.MonitorWarmup.StartAsync();

            // نمونه‌برداری سراسری تاریخچه (گراف ۱۰ دقیقه/۱ ساعت + آلارم مصرف + mini-گراف Tray)
            Monitoring.HistoryStore.Start(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            Services.AutoOptimizer.Start();

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
                    SetWindowVisibility(false);
                }
            };

            // Minimize: اگر Tray فعال باشد، به جای تسک‌بار داخل Tray می‌رود
            aw.Changed += (s, e) =>
            {
                try
                {
                    if (s.Presenter is OverlappedPresenter p &&
                        p.State == OverlappedPresenterState.Minimized)
                    {
                        if (AppSettings.TrayEnabled) s.Hide();
                        SetWindowVisibility(false);
                    }
                    else if (e.DidVisibilityChange)
                    {
                        SetWindowVisibility(s.IsVisible);
                    }
                }
                catch { }
            };
        }

        /// <summary>
        /// چک خودکار آپدیت هنگام باز شدن برنامه — بی‌صدا در پس‌زمینه.
        /// هیچ فایلی بدون اجازه‌ی کاربر دانلود نمی‌شود: اگر نسخه‌ی جدیدی بود فقط یک
        /// پنجره‌ی اطلاع‌رسانی نشان داده می‌شود و دانلود تنها با زدن دکمه‌ی «دانلود»
        /// شروع می‌شود (با نمایش درصد پیشرفت). هر خطایی (آفلاین، سرور خراب) بی‌سروصدا
        /// نادیده گرفته می‌شود.
        /// </summary>
        private static async System.Threading.Tasks.Task AutoCheckUpdatesAsync()
        {
            try
            {
                // چند ثانیه صبر تا استارت برنامه سبک بماند
                await System.Threading.Tasks.Task.Delay(5000);

                var info = await UpdateChecker.CheckAsync();
                if (info is not { UpdateAvailable: true }) return;

                MainAppWindow?.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        if (MainAppWindow?.Content is FrameworkElement root)
                        {
                            if (AppSettings.ModernUi) await UpdateDialog.ShowAsync(info, root.XamlRoot);
                            else await ShowUpdatePromptAsync(info, root);
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        /// <summary>
        /// پنجره‌ی «نسخه‌ی جدید موجود است» — تا وقتی کاربر دکمه‌ی دانلود را نزند
        /// هیچ چیزی دانلود نمی‌شود. بعد از زدن دکمه، نوار پیشرفت و درصد دانلود
        /// در همین پنجره نشان داده می‌شود و در پایان دکمه به «نصب» تبدیل می‌شود.
        /// </summary>
        private static async System.Threading.Tasks.Task ShowUpdatePromptAsync(
            Helpers.UpdateInfo info, FrameworkElement root)
        {
            var message = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = string.Format(
                    L10n.T("Version {0} is available (you have {1}). Download it now?"),
                    info.LatestVersion, UpdateChecker.CurrentVersion),
                TextWrapping = TextWrapping.Wrap,
            };

            var bar = new Microsoft.UI.Xaml.Controls.ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Margin = new Thickness(0, 12, 0, 0),
                Visibility = Visibility.Collapsed,
            };

            var percent = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Margin = new Thickness(0, 6, 0, 0),
                Opacity = 0.8,
                Visibility = Visibility.Collapsed,
            };

            var panel = new Microsoft.UI.Xaml.Controls.StackPanel { MinWidth = 320 };
            panel.Children.Add(message);
            panel.Children.Add(bar);
            panel.Children.Add(percent);

            var dlg = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = L10n.T("Update available"),
                Content = panel,
                PrimaryButtonText = L10n.T("Download"),
                CloseButtonText = L10n.T("Later"),
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
                XamlRoot = root.XamlRoot,
                FlowDirection = L10n.Direction,
            };

            string? downloadedPath = null;
            bool busy = false;

            dlg.PrimaryButtonClick += async (sender, args) =>
            {
                // فایل آماده است — این کلیک یعنی «نصب کن»، پنجره بسته شود
                if (downloadedPath != null) return;

                // در حین دانلود پنجره نباید بسته شود
                args.Cancel = true;
                if (busy) return;
                busy = true;

                var deferral = args.GetDeferral();
                try
                {
                    sender.IsPrimaryButtonEnabled = false;
                    message.Text = L10n.T("Downloading update...");
                    bar.Visibility = Visibility.Visible;
                    bar.Value = 0;
                    percent.Visibility = Visibility.Visible;
                    percent.Text = "0%";

                    var progress = new System.Progress<double>(p =>
                    {
                        bar.Value = p;
                        percent.Text = $"{p:0}%";
                    });

                    string? path = await UpdateChecker.DownloadAsync(info, progress);

                    if (path == null)
                    {
                        message.Text = L10n.T("Download failed. Try again later.");
                        bar.Visibility = Visibility.Collapsed;
                        percent.Visibility = Visibility.Collapsed;
                        sender.PrimaryButtonText = L10n.T("Download");
                        sender.IsPrimaryButtonEnabled = true;
                        return;
                    }

                    downloadedPath = path;
                    message.Text = string.Format(
                        L10n.T("Version {0} has been downloaded. Install it now to update Pulse."),
                        info.LatestVersion);
                    bar.Value = 100;
                    percent.Text = "100%";
                    sender.PrimaryButtonText = L10n.T("Install");
                    sender.IsPrimaryButtonEnabled = true;
                }
                catch
                {
                    sender.IsPrimaryButtonEnabled = true;
                }
                finally
                {
                    busy = false;
                    deferral.Complete();
                }
            };

            var result = await dlg.ShowAsync();

            // فقط وقتی فایل دانلود شده و کاربر «نصب» را زده باشد نصاب اجرا می‌شود
            if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary && downloadedPath != null)
            {
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(downloadedPath) { UseShellExecute = true });
                }
                catch { }
            }
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
