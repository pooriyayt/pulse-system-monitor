using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TaskManagerPro.Controls;
using TaskManagerPro.Helpers;
using TaskManagerPro.Monitoring;

namespace TaskManagerPro.Views
{
    /// <summary>
    /// داشبورد زنده: CPU / RAM / GPU / Disk / Network + مشخصات سخت‌افزار
    /// </summary>
    public sealed partial class OverviewPage : Page
    {
        private DispatcherTimer? _timer;
        private bool _busy;
        private bool _firstSnapshotShown;
        private readonly List<ProgressBar> _coreBars = new();
        private readonly List<TextBlock> _coreLabels = new();

        public OverviewPage()
        {
            this.InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void ApplyL10n()
        {
            OverviewTitle.Text = L10n.T("Overview");
            CpuHeader.Text = "CPU";
            MemHeader.Text = L10n.T("Memory");
            GpuHeader.Text = "GPU";
            DiskHeader.Text = L10n.T("Disk");
            NetHeader.Text = L10n.T("Network");
            NetDownLabel.Text = L10n.T("Download");
            NetUpLabel.Text = L10n.T("Upload");
            HardwareHeader.Text = L10n.T("Hardware");
            CpuTileLabel.Text = "CPU";
            MemTileLabel.Text = L10n.T("Memory").ToUpperInvariant();
            GpuTileLabel.Text = "GPU";
            DiskTileLabel.Text = L10n.T("Disk").ToUpperInvariant();
            GreetingText.Text = L10n.T("Your system at a glance");
            LoadingText.Text = L10n.T("Reading system counters...");
        }

        /// <summary>لودینگ اولیه: محتوا محو، حلقه‌ی چرخان وسط صفحه</summary>
        private void ShowLoading(bool show)
        {
            LoadingPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ContentScroller.Opacity = show ? 0.25 : 1.0;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            AppSettings.LanguageChanged += ApplyL10n;
            ApplyL10n();

            // تا رسیدن اولین داده، لودینگ نشان بده (پنجره از همان اول باز و قابل استفاده است)
            if (!_firstSnapshotShown) ShowLoading(true);

            // ساخت شمارنده‌ها چند ثانیه طول می‌کشد — کاملاً در پس‌زمینه
            await Monitoring.MonitorWarmup.StartAsync();

            // مشخصات سخت‌افزار را در پس‌زمینه بخوان تا UI قفل نشود
            CpuModelText.Text = await Task.Run(HardwareInfo.GetCpuName);
            GpuModelText.Text = await Task.Run(HardwareInfo.GetGpuName);
            HardwareText.Text = await Task.Run(HardwareInfo.GetSummary);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(AppSettings.RefreshIntervalMs)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            AppSettings.RefreshIntervalChanged += OnIntervalChanged;

            // اولین آپدیت بدون انتظار
            Timer_Tick(this, new object());
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            _timer = null;
            AppSettings.RefreshIntervalChanged -= OnIntervalChanged;
            AppSettings.LanguageChanged -= ApplyL10n;
        }

        private void OnIntervalChanged()
        {
            if (_timer != null)
                _timer.Interval = TimeSpan.FromMilliseconds(AppSettings.RefreshIntervalMs);
        }

        private async void Timer_Tick(object? sender, object e)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                // خواندن شمارنده‌ها در Thread جدا تا UI روان بماند.
                // نکته: حتماً به شکل lambda — با Task.Run(SystemMonitor.Instance.Read)
                // خودِ Instance روی ترد UI ساخته می‌شد و پنجره فریز می‌کرد.
                var s = await Task.Run(() => SystemMonitor.Instance.Read());
                UpdateUi(s);
            }
            catch
            {
                // خطاهای موقتی شمارنده‌ها مهم نیستند؛ در آپدیت بعدی جبران می‌شود
            }
            finally
            {
                _busy = false;

                // بعد از اولین تلاش (حتی اگر بعضی شمارنده‌ها خطا دادند) لودینگ برداشته می‌شود
                if (!_firstSnapshotShown)
                {
                    _firstSnapshotShown = true;
                    ShowLoading(false);
                }
            }
        }

        private void UpdateUi(SystemSnapshot s)
        {
            UpdateTiles(s);

            // CPU (درصد + سرعت لحظه‌ای + دما اگر در دسترس باشد)
            CpuGraph.AddValue(s.CpuTotal);
            var cpuLine = $"{s.CpuTotal:F0}%";
            if (s.CpuMhz > 0) cpuLine += $"   ·   {s.CpuMhz / 1000.0:F2} GHz";
            if (s.CpuTempC > 0) cpuLine += $"   ·   {s.CpuTempC:F0} °C";
            CpuText.Text = cpuLine;

            if (_coreBars.Count == 0 && s.CpuCores.Length > 0)
                BuildCoreBars(s.CpuCores.Length);

            for (int i = 0; i < _coreBars.Count && i < s.CpuCores.Length; i++)
            {
                _coreBars[i].Value = s.CpuCores[i];
                _coreLabels[i].Text = $"{L10n.T("Core")} {i}: {s.CpuCores[i]:F0}%";
            }

            // Memory + Page File
            MemGraph.AddValue(s.MemPercent);
            MemText.Text = $"{s.MemUsedGB:F1} / {s.MemTotalGB:F1} GB  ({s.MemPercent:F0}%)";
            PageFileText.Text = string.Format(L10n.T("Page File: {0}% used"), (int)s.PageFilePercent);
            double used = Math.Clamp(s.MemPercent, 0, 100);
            MemUsedCol.Width = new GridLength(used, GridUnitType.Star);
            MemFreeCol.Width = new GridLength(100 - used, GridUnitType.Star);
            MemUsedLegend.Text = $"{L10n.T("In use")}  {s.MemUsedGB:F1} GB";
            MemFreeLegend.Text = $"{L10n.T("Available")}  {s.MemAvailableGB:F1} GB";
            UpdateRamDetails();

            // GPU ها — همه‌ی کارت‌ها به تفکیک (شامل GPU داخلی Intel)
            if (s.Gpus.Count > 0)
            {
                GpuGraph.AddValue(Math.Max(s.GpuPercent, 0));

                var lines = new List<string>();
                foreach (var g in s.Gpus)
                {
                    string temp = s.GpuTempC > 0 ? $"   ·   {s.GpuTempC:F0} °C" : "";
                    lines.Add($"{g.Name}:  {g.UsagePercent:F0}%   ·   VRAM: {FormatMB(g.DedicatedMB)}{temp}");
                }
                GpuText.Text = string.Join("\n", lines);
            }
            else
            {
                GpuText.Text = L10n.T("GPU usage is not available on this system");
            }

            // Disk
            DiskGraph.AddValue(s.DiskPercent);
            DiskText.Text = $"{s.DiskPercent:F0}%   ·   {L10n.T("Read")}: {s.DiskReadMBs:F1} MB/s   ·   {L10n.T("Write")}: {s.DiskWriteMBs:F1} MB/s";

            // Network
            NetDownGraph.AddValue(s.NetRecvKBs);
            NetUpGraph.AddValue(s.NetSentKBs);
            NetDownText.Text = FormatSpeed(s.NetRecvKBs);
            NetUpText.Text = FormatSpeed(s.NetSentKBs);
        }

        private static string FormatSpeed(double kbs) =>
            kbs >= 1024 ? $"{kbs / 1024.0:F1} MB/s" : $"{kbs:F0} KB/s";

        private static string FormatMB(double mb) =>
            mb >= 1024 ? $"{mb / 1024.0:F1} GB" : $"{mb:F0} MB";

        private Monitoring.RamInfo? _ramInfo;
        private bool _ramLoading;
        private long _compressed = -1;
        private int _ramTick;

        /// <summary>جزئیات رم: مشخصات ثابت (یک بار، پس‌زمینه) + آمار زنده‌ی Commit / کش / فشرده</summary>
        private void UpdateRamDetails()
        {
            if (_ramInfo == null && !_ramLoading)
            {
                _ramLoading = true;
                _ = Task.Run(Monitoring.MemoryHardware.GetInfo).ContinueWith(t =>
                    DispatcherQueue.TryEnqueue(() => { _ramInfo = t.Result; ShowRamStatic(); }),
                    TaskScheduler.Default);
            }

            RamSpeedLabel.Text = L10n.T("Speed");
            RamSlotsLabel.Text = L10n.T("Slots used");
            RamFormLabel.Text = L10n.T("Form factor");
            RamCommitLabel.Text = L10n.T("Committed");
            RamCachedLabel.Text = L10n.T("Cached");
            RamCompressedLabel.Text = L10n.T("Compressed");

            var mc = Monitoring.MemoryHardware.GetCounters();
            if (mc is { } c)
            {
                RamCommitValue.Text = $"{c.CommitTotal / 1073741824.0:F1} / {c.CommitLimit / 1073741824.0:F1} GB";
                RamCachedValue.Text = $"{c.SystemCache / 1073741824.0:F1} GB";
            }
            if (_ramTick++ % 3 == 0)
                _ = Task.Run(Monitoring.MemoryHardware.GetCompressedBytes).ContinueWith(t =>
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _compressed = t.Result;
                        RamCompressedValue.Text = _compressed >= 0 ? $"{_compressed / 1048576.0:F0} MB" : "—";
                    }), TaskScheduler.Default);
        }

        private void ShowRamStatic()
        {
            var r = _ramInfo;
            if (r == null) return;
            RamSpeedValue.Text = r.SpeedMHz > 0
                ? (r.Type.Length > 0 ? $"{r.Type} · {r.SpeedMHz} MT/s" : $"{r.SpeedMHz} MT/s")
                : (r.Type.Length > 0 ? r.Type : "—");
            RamSlotsValue.Text = r.TotalSlots > 0 ? string.Format(L10n.T("{0} of {1}"), r.UsedSlots, r.TotalSlots) : "—";
            RamFormValue.Text = r.FormFactor.Length > 0 ? r.FormFactor : "—";
        }

        private void BuildCoreBars(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var label = new TextBlock
                {
                    Text = $"{L10n.T("Core")} {i}",
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["PulseMutedTextBrush"],
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                var bar = new ProgressBar { Maximum = 100, Margin = new Thickness(0, 4, 0, 0), MinHeight = 4 };

                var panel = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
                panel.Children.Add(label);
                panel.Children.Add(bar);

                CoresPanel.Children.Add(panel);
                _coreBars.Add(bar);
                _coreLabels.Add(label);
            }
        }

        /// <summary>کاشی‌های بالای داشبورد + وضعیت کلی سیستم</summary>
        private void UpdateTiles(SystemSnapshot s)
        {
            CpuBig.Text = $"{s.CpuTotal:F0}%";
            CpuRing.Value = s.CpuTotal;
            var cpuSub = new List<string>();
            if (s.CpuMhz > 0) cpuSub.Add($"{s.CpuMhz / 1000.0:F2} GHz");
            if (s.CpuTempC > 0) cpuSub.Add($"{s.CpuTempC:F0} °C");
            if (cpuSub.Count == 0) cpuSub.Add(LoadPalette.LabelFor(s.CpuTotal));
            CpuSub.Text = string.Join("  ·  ", cpuSub);

            MemBig.Text = $"{s.MemPercent:F0}%";
            MemRing.Value = s.MemPercent;
            MemSub.Text = $"{s.MemUsedGB:F1} / {s.MemTotalGB:F1} GB";

            if (s.Gpus.Count > 0 && s.GpuPercent >= 0)
            {
                GpuBig.Text = $"{s.GpuPercent:F0}%";
                GpuRing.Value = s.GpuPercent;
                GpuSub.Text = s.GpuTempC > 0
                    ? $"{s.GpuTempC:F0} °C  ·  {LoadPalette.LabelFor(s.GpuPercent)}"
                    : LoadPalette.LabelFor(s.GpuPercent);
            }
            else
            {
                GpuBig.Text = "—";
                GpuSub.Text = L10n.T("Not available");
            }

            DiskBig.Text = $"{s.DiskPercent:F0}%";
            DiskRing.Value = s.DiskPercent;
            DiskSub.Text = $"R {s.DiskReadMBs:F1}  ·  W {s.DiskWriteMBs:F1} MB/s";

            // وضعیت کلی: بدترین مقدار بین CPU / RAM / دیسک
            double worst = Math.Max(s.CpuTotal, Math.Max(s.MemPercent, s.DiskPercent));
            HealthDot.Fill = LoadPalette.BrushFor(worst);
            HealthText.Text = worst >= 85 ? L10n.T("Under heavy load")
                : worst >= 60 ? L10n.T("Working hard")
                : L10n.T("System healthy");

            if (s.UptimeSeconds > 0)
            {
                var up = TimeSpan.FromSeconds(s.UptimeSeconds);
                UptimeText.Text = "·  " + string.Format(L10n.T("Up {0}"),
                    up.TotalDays >= 1 ? $"{(int)up.TotalDays}d {up.Hours}h" : $"{up.Hours}h {up.Minutes}m");
            }
        }
    }
}
