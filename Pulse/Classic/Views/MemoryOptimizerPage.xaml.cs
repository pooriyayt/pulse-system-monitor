using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskManagerPro.Helpers;
using TaskManagerPro.Services;

namespace TaskManagerPro.Classic.Views
{
    /// <summary>
    /// صفحه‌ی بهینه‌ساز حافظه: گیج زنده‌ی مصرف RAM، حجم Standby،
    /// و خالی کردن Working Set / پاک کردن Standby List
    /// </summary>
    public sealed partial class MemoryOptimizerPage : Page
    {
        private DispatcherTimer? _timer;
        private bool _busy;
        private bool _sampling;
        private long _sessionFreed;

        public MemoryOptimizerPage()
        {
            this.InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyL10n();
            AppSettings.LanguageChanged += ApplyL10n;

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
        }

        private void ApplyL10n()
        {
            PageTitle.Text = L10n.T("Memory Optimizer");
            UsageLabel.Text = L10n.T("In use");
            InUseLabel.Text = L10n.T("In use");
            AvailLabel.Text = L10n.T("Available");
            StandbyLabel.Text = L10n.T("Standby cache");
            TotalLabel.Text = L10n.T("Total");
            SessionLabel.Text = L10n.T("Freed this session");
            TrimTitle.Text = L10n.T("Trim working sets");
            TrimDesc.Text = L10n.T("Asks every process to release memory it is not actively using.");
            PurgeTitle.Text = L10n.T("Clear standby list");
            PurgeDesc.Text = L10n.T("Frees cached file data so it becomes free memory. Requires administrator.");
            OptimizeLabel.Text = L10n.T("Optimize memory");

            if (!MemoryOptimizerService.CanPurgeStandby)
            {
                AdminBar.Title = L10n.T("Note");
                AdminBar.Message = L10n.T("Run as administrator to also clear the standby list.");
                AdminBar.IsOpen = true;
                PurgeToggle.IsOn = false;
                PurgeToggle.IsEnabled = false;
            }
            SessionValue.Text = FormatBytes(_sessionFreed);
        }

        // ---- آمار زنده ----

        private async Task SampleAsync()
        {
            if (_sampling) return;
            _sampling = true;
            try
            {
                var (total, avail, standby) = await Task.Run(() =>
                {
                    var (t, a) = MemoryOptimizerService.GetPhysicalMemory();
                    long s = MemoryOptimizerService.CanPurgeStandby ? MemoryOptimizerService.GetStandbyBytes() : -1;
                    return (t, a, s);
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
            }
            catch { }
            finally
            {
                _sampling = false;
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
            BusyRing.IsActive = true;
            ResultBar.IsOpen = false;

            try
            {
                var r = await Task.Run(() => MemoryOptimizerService.Optimize(trim, purge));

                long freed = r.WorkingSetTrimmedBytes + Math.Max(0, r.StandbyFreedBytes);
                _sessionFreed += freed;
                SessionValue.Text = FormatBytes(_sessionFreed);

                string trimmed = trim
                    ? string.Format(L10n.T("Trimmed {0} MB from {1} processes."),
                        r.WorkingSetTrimmedBytes / (1024 * 1024), r.ProcessesTrimmed)
                    : "";

                if (r.StandbyPurged)
                {
                    ResultBar.Severity = InfoBarSeverity.Success;
                    ResultBar.Title = r.StandbyFreedBytes >= 0
                        ? string.Format(L10n.T("Freed {0} MB of standby memory"), r.StandbyFreedBytes / (1024 * 1024))
                        : L10n.T("Standby list cleared");
                    ResultBar.Message = trimmed;
                }
                else if (purge)
                {
                    ResultBar.Severity = InfoBarSeverity.Warning;
                    ResultBar.Title = trimmed;
                    ResultBar.Message = L10n.T("Windows did not allow clearing the standby list.");
                }
                else
                {
                    ResultBar.Severity = InfoBarSeverity.Success;
                    ResultBar.Title = trimmed;
                    ResultBar.Message = "";
                }
            }
            catch (Exception ex)
            {
                ResultBar.Severity = InfoBarSeverity.Error;
                ResultBar.Title = L10n.T("Error");
                ResultBar.Message = ex.Message;
            }
            finally
            {
                ResultBar.IsOpen = true;
                BusyRing.IsActive = false;
                OptimizeBtn.IsEnabled = true;
                _busy = false;
                await SampleAsync();
            }
        }

        private static string FormatBytes(long bytes)
        {
            double mb = bytes / (1024.0 * 1024);
            return mb >= 1024 ? $"{mb / 1024:F1} GB" : $"{mb:F0} MB";
        }
    }
}
