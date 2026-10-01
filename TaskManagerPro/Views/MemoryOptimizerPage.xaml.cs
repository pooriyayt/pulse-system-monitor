using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TaskManagerPro.Helpers;
using TaskManagerPro.Services;

namespace TaskManagerPro.Views
{
    /// <summary>
    /// بهینه‌ساز حافظه: گیج زنده، ترکیب حافظه (استفاده / کش / آزاد)، آزادسازی واقعی رم
    /// با گزارش صادقانه‌ی «آزاد قبل ← آزاد بعد»، بهینه‌سازی خودکار و پرمصرف‌ترین برنامه‌ها.
    /// </summary>
    public sealed partial class MemoryOptimizerPage : Page
    {
        private DispatcherTimer? _timer;
        private bool _busy;
        private bool _sampling;
        private int _tick;
        // تا پایان ساخت صفحه رویدادهای کنترل‌ها نادیده گرفته شوند
        private bool _init = true;

        private readonly List<(TextBlock Name, TextBlock Value, ProgressBar Bar)> _top = new();

        public MemoryOptimizerPage()
        {
            this.InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _init = true;
            AutoToggle.IsOn = AppSettings.AutoOptimize;
            AutoSlider.Value = AppSettings.AutoOptimizeLimit;
            _init = false;

            ApplyL10n();
            AppSettings.LanguageChanged += ApplyL10n;
            AutoOptimizer.Ran += OnAutoRan;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _timer.Tick += async (_, _) => await SampleAsync();
            _timer.Start();
            _ = SampleAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            _timer = null;
            AppSettings.LanguageChanged -= ApplyL10n;
            AutoOptimizer.Ran -= OnAutoRan;
        }

        private void ApplyL10n()
        {
            PageTitle.Text = L10n.T("Memory Optimizer");
            PageSubtitle.Text = L10n.T("Free up RAM that apps are holding but not using");
            UsageLabel.Text = L10n.T("In use");
            InUseLabel.Text = L10n.T("In use");
            AvailLabel.Text = L10n.T("Available");
            StandbyLabel.Text = L10n.T("Standby cache");
            TotalLabel.Text = L10n.T("Total");
            LegendUsed.Text = L10n.T("In use");
            LegendCache.Text = L10n.T("Standby cache");
            LegendFree.Text = L10n.T("Free");
            SessionLabel.Text = L10n.T("Freed this session");
            TrimTitle.Text = L10n.T("Trim working sets");
            TrimDesc.Text = L10n.T("Asks every process to release memory it is not actively using.");
            PurgeTitle.Text = L10n.T("Clear standby & file cache");
            PurgeDesc.Text = L10n.T("Writes modified pages to disk, then frees cached file data. Requires administrator.");
            AutoTitle.Text = L10n.T("Automatic cleanup");
            OptionsHeader.Text = L10n.T("Cleanup options");
            TopHeader.Text = L10n.T("Biggest memory users");
            OptimizeLabel.Text = L10n.T("Free up memory");
            BeforeLabel.Text = L10n.T("Free before");
            AfterLabel.Text = L10n.T("Free after");
            AdminTitle.Text = L10n.T("Unlock full cleanup");
            AdminDesc.Text = L10n.T("Clearing the standby cache and file cache frees much more memory, but needs administrator rights.");
            ElevateLabel.Text = L10n.T("Restart as administrator");
            UpdateAutoText();

            bool admin = MemoryOptimizerService.CanPurgeStandby;
            AdminCard.Visibility = admin ? Visibility.Collapsed : Visibility.Visible;
            if (!admin)
            {
                PurgeToggle.IsOn = false;
                PurgeToggle.IsEnabled = false;
            }
            SessionValue.Text = FormatBytes(MemoryOptimizerService.SessionFreedBytes);
        }

        private void UpdateAutoText()
        {
            AutoDesc.Text = string.Format(L10n.T("Frees memory in the background when RAM usage goes above {0}% (at most every 5 minutes)."), AppSettings.AutoOptimizeLimit);
            AutoSlider.Visibility = AppSettings.AutoOptimize ? Visibility.Visible : Visibility.Collapsed;
            if (AutoOptimizer.LastResult is MemoryOptimizeResult r)
            {
                AutoLast.Visibility = Visibility.Visible;
                AutoLast.Text = string.Format(L10n.T("Last automatic cleanup: {0} · freed {1}"),
                    AutoOptimizer.LastRun.ToLocalTime().ToString("HH:mm"), FormatBytes(r.RealFreedBytes));
            }
            else AutoLast.Visibility = Visibility.Collapsed;
        }

        private void Auto_Toggled(object sender, RoutedEventArgs e)
        {
            if (_init) return;
            AppSettings.AutoOptimize = AutoToggle.IsOn;
            UpdateAutoText();
        }

        private void AutoSlider_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_init) return;
            AppSettings.AutoOptimizeLimit = (int)e.NewValue;
            UpdateAutoText();
        }

        private void OnAutoRan() => DispatcherQueue.TryEnqueue(() =>
        {
            SessionValue.Text = FormatBytes(MemoryOptimizerService.SessionFreedBytes);
            UpdateAutoText();
        });

        private void Elevate_Click(object sender, RoutedEventArgs e)
        {
            if (!AppRestart.RestartAsAdmin())
            {
                AdminBar.Severity = InfoBarSeverity.Warning;
                AdminBar.Title = L10n.T("Note");
                AdminBar.Message = L10n.T("Administrator access was not granted.");
                AdminBar.IsOpen = true;
            }
        }

        // ---- آمار زنده ----

        private async Task SampleAsync()
        {
            if (_sampling) return;
            _sampling = true;
            try
            {
                bool refreshTop = _tick++ % 4 == 0;
                var (total, avail, standby, top) = await Task.Run(() =>
                {
                    var (t, a) = MemoryOptimizerService.GetPhysicalMemory();
                    long s = MemoryOptimizerService.CanPurgeStandby ? MemoryOptimizerService.GetStandbyBytes() : -1;
                    var tp = refreshTop ? MemoryOptimizerService.TopConsumers(6) : null;
                    return (t, a, s, tp);
                });
                if (total == 0) return;

                ulong used = total - avail;
                double pct = used * 100.0 / total;
                UsageRing.Value = pct;
                UsagePercent.Text = $"{pct:F0}%";
                InUseValue.Text = FormatBytes((long)used);
                AvailValue.Text = FormatBytes((long)avail);
                TotalValue.Text = FormatBytes((long)total);
                StandbyValue.Text = standby >= 0 ? FormatBytes(standby) : "—";
                SessionValue.Text = FormatBytes(MemoryOptimizerService.SessionFreedBytes);

                // نوار ترکیب: کش جزو «در دسترس» است، پس از آن کم می‌شود
                double cache = standby > 0 ? Math.Min(standby, (long)avail) : 0;
                UsedCol.Width = new GridLength(used, GridUnitType.Star);
                CacheCol.Width = new GridLength(cache, GridUnitType.Star);
                FreeCol.Width = new GridLength(Math.Max(0, avail - cache), GridUnitType.Star);

                if (top != null) ShowTop(top, (long)total);
            }
            catch { }
            finally
            {
                _sampling = false;
            }
        }

        private void ShowTop(List<(string Name, long Bytes)> top, long total)
        {
            while (_top.Count < top.Count)
            {
                var name = new TextBlock { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis };
                var val = new TextBlock { FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
                var bar = new ProgressBar { Maximum = 100, Margin = new Thickness(0, 6, 0, 0) };
                var head = new Grid();
                head.Children.Add(name);
                head.Children.Add(val);
                var row = new StackPanel();
                row.Children.Add(head);
                row.Children.Add(bar);
                TopPanel.Children.Add(row);
                _top.Add((name, val, bar));
            }
            long max = top.Count > 0 ? Math.Max(1, top[0].Bytes) : 1;
            for (int i = 0; i < _top.Count; i++)
            {
                bool used = i < top.Count;
                ((FrameworkElement)TopPanel.Children[i]).Visibility = used ? Visibility.Visible : Visibility.Collapsed;
                if (!used) continue;
                _top[i].Name.Text = top[i].Name;
                _top[i].Value.Text = FormatBytes(top[i].Bytes);
                _top[i].Bar.Value = top[i].Bytes * 100.0 / max;
            }
        }

        // ---- بهینه‌سازی ----

        private async void Optimize_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            bool trim = TrimToggle.IsOn;
            bool purge = PurgeToggle.IsOn && PurgeToggle.IsEnabled;
            if (!trim && !purge) return;

            _busy = true;
            OptimizeBtn.IsEnabled = false;
            OptimizeContent.Opacity = 0;
            BusyRing.IsActive = true;
            ResultBar.IsOpen = false;

            try
            {
                var r = await Task.Run(() => MemoryOptimizerService.Optimize(trim, purge));

                long freed = r.RealFreedBytes;
                SessionValue.Text = FormatBytes(MemoryOptimizerService.SessionFreedBytes);

                ResultCard.Visibility = Visibility.Visible;
                BeforeValue.Text = FormatBytes(r.AvailableBefore);
                AfterValue.Text = FormatBytes(r.AvailableAfter);

                if (freed >= 20L * 1024 * 1024)
                {
                    ResultIcon.Glyph = "";
                    ResultIcon.Tint = "#30D158";
                    await CountUpAsync(freed);
                }
                else
                {
                    ResultIcon.Glyph = "";
                    ResultIcon.Tint = "#0A84FF";
                    ResultTitle.Text = L10n.T("Memory is already in good shape");
                }

                var parts = new List<string>();
                if (trim) parts.Add(string.Format(L10n.T("{0} processes trimmed"), r.ProcessesTrimmed));
                if (r.StandbyPurged) parts.Add(L10n.T("standby & file cache cleared"));
                else if (purge) parts.Add(L10n.T("Windows did not allow clearing the standby list."));
                ResultDetail.Text = string.Join("  ·  ", parts);
            }
            catch (Exception ex)
            {
                ResultBar.Severity = InfoBarSeverity.Error;
                ResultBar.Title = L10n.T("Error");
                ResultBar.Message = ex.Message;
                ResultBar.IsOpen = true;
            }
            finally
            {
                BusyRing.IsActive = false;
                OptimizeContent.Opacity = 1;
                OptimizeBtn.IsEnabled = true;
                _busy = false;
                _tick = 0;
                await SampleAsync();
            }
        }

        /// <summary>شمارش متحرک عدد آزادشده (حس زنده و رضایت‌بخش)</summary>
        private async Task CountUpAsync(long bytes)
        {
            const int steps = 30;
            string fmt = L10n.T("Freed {0}");
            for (int i = 1; i <= steps; i++)
            {
                double t = i / (double)steps;
                double k = 1 - Math.Pow(1 - t, 3);
                ResultTitle.Text = string.Format(fmt, FormatBytes((long)(bytes * k)));
                await Task.Delay(16);
            }
        }

        private static string FormatBytes(long bytes)
        {
            double mb = bytes / (1024.0 * 1024);
            return mb >= 1024 ? $"{mb / 1024:F2} GB" : $"{mb:F0} MB";
        }
    }
}
