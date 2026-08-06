using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using TaskManagerPro.Helpers;
using TaskManagerPro.Models;
using TaskManagerPro.Monitoring;

namespace TaskManagerPro.Views
{
    /// <summary>
    /// تب Performance — مثل Task Manager ویندوز:
    /// سایدبار قطعات با آمار زنده + صفحه‌ی جزئیات اختصاصی برای هر قطعه.
    /// </summary>
    public sealed partial class PerformancePage : Page
    {
        public ObservableCollection<PerfSidebarItem> Items { get; } = new();

        private DispatcherTimer? _timer;
        private bool _busy;
        private string _selectedKey = "cpu";
        private readonly List<ProgressBar> _coreBars = new();
        private readonly List<TextBlock> _coreLabels = new();
        private readonly List<TextBlock> _statValues = new();
        private readonly List<TextBlock> _statLabels = new();
        private string _cpuName = "";
        private List<string> _gpuNames = new();

        // گراف تاریخچه‌دار: 0 = زنده، 1 = ۱۰ دقیقه، 2 = ۱ ساعت
        private int _histMode;
        // همین حالت‌ها برای گراف دوم (آپلود) — کاملاً مستقل از گراف اول
        private int _histMode2;
        private readonly List<TextBlock> _topNames = new();
        private readonly List<TextBlock> _topValues = new();
        private readonly List<TextBlock> _topNames2 = new();
        private readonly List<TextBlock> _topValues2 = new();

        public PerformancePage()
        {
            this.InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            AppSettings.LanguageChanged += ApplyL10n;
        }

        private void ApplyL10n()
        {
            PerfTitle.Text = L10n.T("Performance");
            LogicalProcLabel.Text = L10n.T("Logical processors");
            foreach (var combo in new[] { HistCombo, SecondHistCombo })
            {
                if (combo.Items.Count < 3) continue;
                ((ComboBoxItem)combo.Items[0]).Content = L10n.T("Live");
                ((ComboBoxItem)combo.Items[1]).Content = L10n.T("Last 10 minutes");
                ((ComboBoxItem)combo.Items[2]).Content = L10n.T("Last hour");
            }
            TopTitle.Text = L10n.T("Top processes");
            foreach (var item in Items)
            {
                item.Title = item.Key switch
                {
                    "cpu" => "CPU",
                    "memory" => L10n.T("Memory"),
                    "disk" => L10n.T("Disk"),
                    "network" => L10n.T("Network"),
                    "sensors" => L10n.T("Sensors"),
                    _ when item.Key.StartsWith("gpu") => Items.Count(i => i.Key.StartsWith("gpu")) > 1
                        ? $"GPU {item.Key.Replace("gpu", "")}"
                        : "GPU",
                    _ => item.Title,
                };
                if (item.Key == "sensors")
                    item.Subtitle = L10n.T("Temperature / Fan / Voltage / Power");
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyL10n();

            // ساخت شمارنده‌ها سنگین است — در پس‌زمینه و بدون قفل کردن UI
            await Monitoring.MonitorWarmup.StartAsync();

            // مشخصات ثابت را در پس‌زمینه بخوان تا UI قفل نشود
            _cpuName = await Task.Run(HardwareInfo.GetCpuName);
            _gpuNames = await Task.Run(HardwareInfo.GetGpuNames);

            // lambda لازم است: Task.Run(SystemMonitor.Instance.Read) خودِ Instance را
            // روی ترد UI می‌ساخت و باعث فریز می‌شد
            var first = await Task.Run(() => SystemMonitor.Instance.Read());

            if (Items.Count == 0)
            {
                Items.Add(new PerfSidebarItem { Key = "cpu", Glyph = "\uE950", Title = "CPU" });
                Items.Add(new PerfSidebarItem { Key = "memory", Glyph = "\uEEA0", Title = L10n.T("Memory") });

                // یک آیتم برای هر کارت گرافیک (شامل GPU داخلی Intel)
                var gpuIndexes = first.Gpus.Select(g => g.PhysIndex).Distinct().OrderBy(i => i).ToList();
                if (gpuIndexes.Count == 0)
                    for (int i = 0; i < _gpuNames.Count; i++) gpuIndexes.Add(i);

                foreach (var gi in gpuIndexes)
                {
                    Items.Add(new PerfSidebarItem
                    {
                        Key = $"gpu{gi}",
                        Glyph = "\uE7F4",
                        Title = gpuIndexes.Count > 1 ? $"GPU {gi}" : "GPU",
                        Subtitle = gi < _gpuNames.Count ? _gpuNames[gi] : "",
                    });
                }

                Items.Add(new PerfSidebarItem { Key = "disk", Glyph = "\uEDA2", Title = L10n.T("Disk") });
                Items.Add(new PerfSidebarItem { Key = "network", Glyph = "\uE839", Title = L10n.T("Network") });
                Items.Add(new PerfSidebarItem
                {
                    Key = "sensors",
                    Glyph = "\uE9CA",
                    Title = L10n.T("Sensors"),
                    Subtitle = L10n.T("Temperature / Fan / Voltage / Power"),
                });
            }

            SideList.SelectedIndex = 0;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(AppSettings.RefreshIntervalMs)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            AppSettings.RefreshIntervalChanged += OnIntervalChanged;

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

        private void SideList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SideList.SelectedItem is not PerfSidebarItem item) return;
            _selectedKey = item.Key;
            ConfigureDetail(item);
        }

        /// <summary>چیدمان بخش جزئیات را برای قطعه‌ی انتخاب‌شده آماده کن</summary>
        private void ConfigureDetail(PerfSidebarItem item)
        {
            MainGraph.Clear();
            SecondGraph.Clear();
            MainGraph.ExitStatic();
            SecondGraph.ExitStatic();
            CoresCard.Visibility = Visibility.Collapsed;
            SecondCard.Visibility = Visibility.Collapsed;
            SecondTopCard.Visibility = Visibility.Collapsed;
            SecondHistRow.Visibility = Visibility.Collapsed;
            EnginesCard.Visibility = Visibility.Collapsed;
            SensorsCard.Visibility = Visibility.Collapsed;

            // واحد پیش‌فرض اعداد محور (برای هر قطعه پایین‌تر تنظیم می‌شود)
            MainGraph.Unit = TaskManagerPro.Controls.GraphUnit.Percent;
            SecondGraph.Unit = TaskManagerPro.Controls.GraphUnit.Percent;
            UpdateSpanLabels();

            bool isSensors = item.Key == "sensors";
            MainCard.Visibility = isSensors ? Visibility.Collapsed : Visibility.Visible;
            StatsCard.Visibility = isSensors ? Visibility.Collapsed : Visibility.Visible;
            TopCard.Visibility = isSensors ? Visibility.Collapsed : Visibility.Visible;

            // برگشت به حالت زنده هنگام عوض شدن قطعه
            _histMode = 0;
            _histMode2 = 0;
            if (HistCombo.SelectedIndex != 0) HistCombo.SelectedIndex = 0;
            if (SecondHistCombo.SelectedIndex != 0) SecondHistCombo.SelectedIndex = 0;
            HistScrollRow.Visibility = Visibility.Collapsed;
            SecondHistCombo.Visibility = Visibility.Collapsed;

            DetailIcon.Glyph = item.Glyph;
            DetailTitle.Text = item.Title;
            DetailSubtitle.Text = "";

            switch (KeyKind(item.Key))
            {
                case "sensors":
                    SensorsCard.Visibility = Visibility.Visible;
                    DetailSubtitle.Text = L10n.T("Temperature / Fan / Voltage / Power");
                    // باز کردن دسترسی سنسورها کُند است — یک بار در پس‌زمینه
                    if (!SensorMonitor.IsStarted)
                        _ = Task.Run(SensorMonitor.Start);
                    // لودینگ از همان لحظه‌ی ورود (نه بعد از اولین تیک تایمر)
                    UpdateSensors(new List<SensorReading>());
                    break;

                case "cpu":
                    DetailSubtitle.Text = _cpuName;
                    MainGraph.AutoScale = false;
                    MainGraph.MaxValue = 100;
                    MainGraphLabel.Text = L10n.T("% Utilization");
                    CoresCard.Visibility = Visibility.Visible;
                    break;

                case "memory":
                    MainGraph.AutoScale = false;
                    MainGraph.MaxValue = 100;
                    MainGraphLabel.Text = L10n.T("Memory usage (%)");
                    break;

                case "gpu":
                    // برای دمای GPU از LibreHardwareMonitor استفاده می‌شود — یک بار در پس‌زمینه باز شود
                    if (!SensorMonitor.IsStarted && !SensorMonitor.Failed)
                        _ = Task.Run(SensorMonitor.Start);
                    int gi = GpuIndex(item.Key);
                    DetailSubtitle.Text = gi < _gpuNames.Count ? _gpuNames[gi] : "";
                    MainGraph.AutoScale = false;
                    MainGraph.MaxValue = 100;
                    MainGraphLabel.Text = L10n.T("% Utilization");

                    // گراف‌های موتورهای GPU مثل Task Manager ویندوز
                    EnginesCard.Visibility = Visibility.Visible;
                    foreach (var eg in new[] { Eng0Graph, Eng1Graph, Eng2Graph, Eng3Graph })
                    {
                        eg.AutoScale = false;
                        eg.MaxValue = 100;
                        eg.ShowAxis = false;
                        eg.Clear();
                    }
                    break;

                case "disk":
                    MainGraph.AutoScale = false;
                    MainGraph.MaxValue = 100;
                    MainGraphLabel.Text = L10n.T("Active time (%)");
                    SecondCard.Visibility = Visibility.Visible;
                    SecondGraph.AutoScale = true;
                    SecondGraph.Unit = TaskManagerPro.Controls.GraphUnit.MegaBytesPerSec;
                    SecondGraphLabel.Text = L10n.T("Transfer rate — Read + Write (MB/s)");
                    break;

                case "network":
                    MainGraph.AutoScale = true;
                    MainGraph.Unit = TaskManagerPro.Controls.GraphUnit.SpeedKBs;
                    MainGraphLabel.Text = L10n.T("Download");
                    SecondCard.Visibility = Visibility.Visible;
                    SecondTopCard.Visibility = Visibility.Visible;
                    SecondGraph.AutoScale = true;
                    SecondGraph.Unit = TaskManagerPro.Controls.GraphUnit.SpeedKBs;
                    SecondGraphLabel.Text = L10n.T("Upload");
                    // فقط گراف آپلود تاریخچه‌ی مستقل دارد
                    SecondHistCombo.Visibility = Visibility.Visible;
                    break;
            }
        }

        private static string KeyKind(string key) => key.StartsWith("gpu") ? "gpu" : key;

        private static int GpuIndex(string key) =>
            int.TryParse(key.Substring(3), out var i) ? i : 0;

        private async void Timer_Tick(object? sender, object e)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                bool wantSensors = _selectedKey == "sensors";
                bool wantGpuTemp = KeyKind(_selectedKey) == "gpu";
                string topMetric = TopMetricKey();
                string topMetric2 = SecondTopMetricKey();

                var (s, tops, tops2, sensors) = await Task.Run(() =>
                {
                    var snap = SystemMonitor.Instance.Read();
                    List<TopProc>? top = null;
                    List<TopProc>? top2 = null;
                    if (topMetric.Length > 0)
                    {
                        try { NetworkMonitor.Instance.Snapshot(); } catch { }
                        ProcessSampler.Sample();
                        top = ProcessSampler.Top(topMetric);
                        if (topMetric2.Length > 0) top2 = ProcessSampler.Top(topMetric2);
                    }
                    var sens = wantSensors ? SensorMonitor.Read() : null;

                    // دمای GPU: اگر ویندوز گزارش نکرد، از LibreHardwareMonitor بگیر
                    if (wantGpuTemp && snap.GpuTempC <= 0 && SensorMonitor.IsStarted)
                    {
                        double t = SensorMonitor.ReadGpuTemp();
                        if (t > 0) snap.GpuTempC = t;
                    }

                    return (snap, top, top2, sens);
                });

                UpdateSidebar(s);
                UpdateDetail(s);
                if (tops != null) UpdateTop(tops);
                if (tops2 != null) UpdateSecondTop(tops2);
                if (sensors != null) UpdateSensors(sensors);
                if (_histMode > 0) UpdateMainHistory();
                if (_histMode2 > 0) UpdateSecondHistory();
            }
            catch
            {
                // خطاهای موقتی شمارنده‌ها مهم نیستند
            }
            finally
            {
                _busy = false;
            }
        }

        private GpuStat? FindGpu(SystemSnapshot s, int index)
        {
            var g = s.Gpus.FirstOrDefault(x => x.PhysIndex == index);
            if (g == null && index < s.Gpus.Count) g = s.Gpus[index];
            return g;
        }

        private void UpdateSidebar(SystemSnapshot s)
        {
            foreach (var item in Items)
            {
                switch (KeyKind(item.Key))
                {
                    case "cpu":
                        item.Subtitle = s.CpuMhz > 0
                            ? $"{s.CpuTotal:F0}%  •  {s.CpuMhz / 1000.0:F2} GHz"
                            : $"{s.CpuTotal:F0}%";
                        break;
                    case "memory":
                        item.Subtitle = $"{s.MemUsedGB:F1}/{s.MemTotalGB:F1} GB  ({s.MemPercent:F0}%)";
                        break;
                    case "gpu":
                        var g = FindGpu(s, GpuIndex(item.Key));
                        item.Subtitle = g != null
                            ? $"{g.UsagePercent:F0}%  •  {FormatMB(g.DedicatedMB)}"
                            : L10n.T("Not available");
                        break;
                    case "disk":
                        item.Subtitle = $"{s.DiskPercent:F0}%  •  {L10n.T("Read")} {s.DiskReadMBs:F1} / {L10n.T("Write")} {s.DiskWriteMBs:F1} MB/s";
                        break;
                    case "network":
                        item.Subtitle = $"↓ {FormatSpeed(s.NetRecvKBs)}   ↑ {FormatSpeed(s.NetSentKBs)}";
                        break;
                }
            }
        }

        private void UpdateDetail(SystemSnapshot s)
        {
            switch (KeyKind(_selectedKey))
            {
                case "cpu":
                    MainGraph.AddValue(s.CpuTotal);
                    SetStats(
                        ("Utilization", $"{s.CpuTotal:F0}%"),
                        ("Speed", s.CpuMhz > 0 ? $"{s.CpuMhz / 1000.0:F2} GHz" : "N/A"),
                        ("Processes", s.ProcessCount >= 0 ? $"{s.ProcessCount:F0}" : "N/A"),
                        ("Threads", s.ThreadCount >= 0 ? $"{s.ThreadCount:F0}" : "N/A"),
                        ("Up time", s.UptimeSeconds > 0 ? FormatUptime(s.UptimeSeconds) : "N/A"),
                        ("Temperature", s.CpuTempC > 0 ? $"{s.CpuTempC:F0} °C" : "N/A"));
                    UpdateCores(s);
                    break;

                case "memory":
                    MainGraph.AddValue(s.MemPercent);
                    SetStats(
                        ("In use", $"{s.MemUsedGB:F1} GB"),
                        ("Available", $"{s.MemAvailableGB:F1} GB"),
                        ("Total", $"{s.MemTotalGB:F1} GB"),
                        ("Page file", $"{s.PageFilePercent:F0}% used"));
                    break;

                case "gpu":
                {
                    var g = FindGpu(s, GpuIndex(_selectedKey));
                    MainGraph.AddValue(g?.UsagePercent ?? 0);
                    UpdateEngine(Eng0Label, Eng0Graph, "3D", "3D", g);
                    UpdateEngine(Eng1Label, Eng1Graph, "Copy", "Copy", g);
                    UpdateEngine(Eng2Label, Eng2Graph, L10n.T("Video Decode"), "VideoDecode", g);
                    UpdateEngine(Eng3Label, Eng3Graph, L10n.T("Video Processing"), "VideoProcessing", g);
                    SetStats(
                        ("Utilization", g != null ? $"{g.UsagePercent:F0}%" : "N/A"),
                        ("Dedicated memory", g != null ? FormatMB(g.DedicatedMB) : "N/A"),
                        ("Temperature", s.GpuTempC > 0 ? $"{s.GpuTempC:F0} °C" : "N/A"));
                    break;
                }

                case "disk":
                    MainGraph.AddValue(s.DiskPercent);
                    SecondGraph.AddValue(s.DiskReadMBs + s.DiskWriteMBs);
                    SetStats(
                        ("Active time", $"{s.DiskPercent:F0}%"),
                        ("Read speed", $"{s.DiskReadMBs:F1} MB/s"),
                        ("Write speed", $"{s.DiskWriteMBs:F1} MB/s"));
                    break;

                case "network":
                    MainGraph.AddValue(s.NetRecvKBs);
                    SecondGraph.AddValue(s.NetSentKBs);
                    SetStats(
                        ("Download", FormatSpeed(s.NetRecvKBs)),
                        ("Upload", FormatSpeed(s.NetSentKBs)));
                    break;
            }
        }

        /// <summary>بلوک‌های آمار عددی را بساز/به‌روز کن (عدد بزرگ + برچسب کوچک)</summary>
        private void SetStats(params (string Label, string Value)[] stats)
        {
            while (_statValues.Count < stats.Length)
            {
                var value = new TextBlock
                {
                    FontSize = 20,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                };
                var label = new TextBlock { FontSize = 12, Opacity = 0.7 };

                var panel = new StackPanel { Spacing = 2 };
                panel.Children.Add(value);
                panel.Children.Add(label);

                StatsPanel.Children.Add(panel);
                _statValues.Add(value);
                _statLabels.Add(label);
            }

            for (int i = 0; i < _statValues.Count; i++)
            {
                bool used = i < stats.Length;
                ((StackPanel)StatsPanel.Children[i]).Visibility =
                    used ? Visibility.Visible : Visibility.Collapsed;
                if (used)
                {
                    SetTextAnimated(_statValues[i], stats[i].Value);
                    _statLabels[i].Text = L10n.T(stats[i].Label);
                }
            }
        }

        /// <summary>گراف یک موتور GPU را به‌روز کن</summary>
        private static void UpdateEngine(TextBlock label, TaskManagerPro.Controls.LiveGraph graph, string display, string key, GpuStat? g)
        {
            double val = 0;
            if (g != null && g.Engines.TryGetValue(key, out var v)) val = v;
            graph.AddValue(val);
            label.Text = $"{display} — {val:F0}%";
        }

        /// <summary>تغییر نرم متن — به جای پرش، عدد با فید کوتاه عوض می‌شود (حس پرمیوم)</summary>
        private static void SetTextAnimated(TextBlock tb, string text)
        {
            if (tb.Text == text) return;
            tb.Text = text;

            var anim = new DoubleAnimation
            {
                From = 0.3,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(anim, tb);
            Storyboard.SetTargetProperty(anim, "Opacity");
            var sb = new Storyboard();
            sb.Children.Add(anim);
            sb.Begin();
        }

        private void UpdateCores(SystemSnapshot s)
        {
            if (_coreBars.Count == 0 && s.CpuCores.Length > 0)
            {
                for (int i = 0; i < s.CpuCores.Length; i++)
                {
                    var label = new TextBlock { Text = $"{L10n.T("Core")} {i}", FontSize = 12, Opacity = 0.8 };
                    var bar = new ProgressBar { Maximum = 100, Margin = new Thickness(0, 2, 16, 0) };

                    var panel = new StackPanel();
                    panel.Children.Add(label);
                    panel.Children.Add(bar);

                    CoresPanel.Children.Add(panel);
                    _coreBars.Add(bar);
                    _coreLabels.Add(label);
                }
            }

            for (int i = 0; i < _coreBars.Count && i < s.CpuCores.Length; i++)
            {
                _coreBars[i].Value = s.CpuCores[i];
                _coreLabels[i].Text = $"{L10n.T("Core")} {i}: {s.CpuCores[i]:F0}%";
            }
        }

        // ---------- Top processes زیر گراف ----------

        /// <summary>متریک Top processes برای قطعه‌ی انتخاب‌شده ("" یعنی نمایش نده)</summary>
        private string TopMetricKey() => KeyKind(_selectedKey) switch
        {
            "cpu" => "cpu",
            "memory" => "mem",
            "gpu" => "gpu",
            "disk" => "disk",
            "network" => "netdown",
            _ => "",
        };

        /// <summary>متریک Top processes گراف دوم (فعلاً فقط آپلود در بخش شبکه)</summary>
        private string SecondTopMetricKey() =>
            KeyKind(_selectedKey) == "network" ? "netup" : "";

        private void UpdateTop(List<TopProc> tops)
        {
            TopTitle.Text = KeyKind(_selectedKey) == "network"
                ? $"{L10n.T("Top processes")} — {L10n.T("Download")}"
                : L10n.T("Top processes");

            while (_topNames.Count < 3)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var name = new TextBlock { FontSize = 13, TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis };
                var val = new TextBlock { FontSize = 13, Opacity = 0.8 };
                Grid.SetColumn(val, 1);
                grid.Children.Add(name);
                grid.Children.Add(val);

                TopPanel.Children.Add(grid);
                _topNames.Add(name);
                _topValues.Add(val);
            }

            for (int i = 0; i < 3; i++)
            {
                bool used = i < tops.Count;
                ((Grid)TopPanel.Children[i]).Visibility = used ? Visibility.Visible : Visibility.Collapsed;
                if (used)
                {
                    _topNames[i].Text = $"{tops[i].Name}  (PID {tops[i].Pid})";
                    _topValues[i].Text = tops[i].Text;
                }
            }
        }

        /// <summary>پرمصرف‌ترین پردازه‌های گراف دوم (آپلود)</summary>
        private void UpdateSecondTop(List<TopProc> tops)
        {
            SecondTopTitle.Text = $"{L10n.T("Top processes")} — {L10n.T("Upload")}";

            while (_topNames2.Count < 3)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var name = new TextBlock { FontSize = 13, TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis };
                var val = new TextBlock { FontSize = 13, Opacity = 0.8 };
                Grid.SetColumn(val, 1);
                grid.Children.Add(name);
                grid.Children.Add(val);

                SecondTopPanel.Children.Add(grid);
                _topNames2.Add(name);
                _topValues2.Add(val);
            }

            for (int i = 0; i < 3; i++)
            {
                bool used = i < tops.Count;
                ((Grid)SecondTopPanel.Children[i]).Visibility = used ? Visibility.Visible : Visibility.Collapsed;
                if (used)
                {
                    _topNames2[i].Text = $"{tops[i].Name}  (PID {tops[i].Pid})";
                    _topValues2[i].Text = tops[i].Text;
                }
            }
        }

        // ---------- سنسورها ----------

        private void UpdateSensors(List<SensorReading> readings)
        {
            SensorsPanel.Children.Clear();

            if (readings.Count == 0)
            {
                string msg;
                bool loading = false;
                if (!SensorMonitor.StartAttempted)
                {
                    msg = L10n.T("Opening hardware sensors...");
                    loading = true;
                }
                else if (SensorMonitor.Failed || SensorMonitor.IsStarted)
                {
                    // یا باز کردن شکست خورد، یا باز شد ولی هیچ سنسوری گزارش نشد —
                    // در هر دو حالت سخت‌افزار/درایور این سیستم داده‌ای نمی‌دهد
                    msg = AdminHelper.IsAdmin
                        ? L10n.T("Your system does not support this section. The hardware or its driver does not expose temperature/fan/voltage sensors.")
                        : L10n.T("No sensors available. Try running the app as administrator — if it still shows nothing, your system does not support this section.");
                    if (SensorMonitor.Failed && SensorMonitor.FailureMessage.Length > 0)
                        msg += $"\n({SensorMonitor.FailureMessage})";
                }
                else
                {
                    msg = L10n.T("Opening hardware sensors...");
                    loading = true;
                }

                if (loading)
                {
                    // لودینگ حین باز شدن سنسورها (چند ثانیه طول می‌کشد)
                    var panel = new StackPanel
                    {
                        Spacing = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 24, 0, 24),
                    };
                    panel.Children.Add(new ProgressRing
                    {
                        IsActive = true,
                        Width = 36,
                        Height = 36,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    });
                    panel.Children.Add(new TextBlock
                    {
                        Text = msg,
                        Opacity = 0.7,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    });
                    SensorsPanel.Children.Add(panel);
                }
                else
                {
                    SensorsPanel.Children.Add(new TextBlock
                    {
                        Text = msg,
                        Opacity = 0.7,
                        TextWrapping = TextWrapping.Wrap,
                    });
                }
                return;
            }

            foreach (var group in readings.GroupBy(r => r.Hardware))
            {
                SensorsPanel.Children.Add(new TextBlock
                {
                    Text = group.Key,
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                });

                var wrap = new VariableSizedWrapGrid
                {
                    Orientation = Orientation.Horizontal,
                    ItemWidth = 210,
                    ItemHeight = 40,
                };

                foreach (var r in group.OrderBy(x => x.Kind).ThenBy(x => x.Name))
                {
                    var panel = new StackPanel();
                    panel.Children.Add(new TextBlock { Text = r.ValueText, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                    panel.Children.Add(new TextBlock { Text = $"{r.Kind} — {r.Name}", FontSize = 11, Opacity = 0.6, TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis });
                    wrap.Children.Add(panel);
                }

                SensorsPanel.Children.Add(wrap);
            }
        }

        // ---------- گراف تاریخچه‌دار ----------

        /// <summary>کلید HistoryStore برای قطعه‌ی انتخاب‌شده</summary>
        private string HistKey() => KeyKind(_selectedKey) switch
        {
            "memory" => "mem",
            "gpu" => "gpu",
            "disk" => "disk",
            "network" => "netdown",
            _ => "cpu",
        };

        /// <summary>کلید HistoryStore برای گراف دوم ("" یعنی تاریخچه ندارد)</summary>
        private string SecondHistKey() =>
            KeyKind(_selectedKey) == "network" ? "netup" : "";

        private void Hist_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (HistCombo.SelectedIndex < 0) return;
            _histMode = HistCombo.SelectedIndex;

            if (_histMode == 0)
            {
                MainGraph.ExitStatic();
                HistScrollRow.Visibility = Visibility.Collapsed;
            }
            else
            {
                HistScrollRow.Visibility = Visibility.Visible;
                HistSlider.Value = 100; // 100 = تا همین الان
                UpdateMainHistory();
            }
            UpdateSpanLabels();
        }

        private void SecondHist_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (SecondHistCombo.SelectedIndex < 0) return;
            _histMode2 = SecondHistCombo.SelectedIndex;

            if (_histMode2 == 0 || SecondHistKey().Length == 0)
            {
                _histMode2 = SecondHistKey().Length == 0 ? 0 : _histMode2;
                SecondGraph.ExitStatic();
                SecondHistRow.Visibility = Visibility.Collapsed;
            }
            else
            {
                SecondHistRow.Visibility = Visibility.Visible;
                SecondHistSlider.Value = 100;
                UpdateSecondHistory();
            }
            UpdateSpanLabels();
        }

        private void HistSlider_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_histMode > 0) UpdateMainHistory();
        }

        private void SecondHistSlider_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_histMode2 > 0) UpdateSecondHistory();
        }

        /// <summary>برچسب بازه‌ی زمانی گوشه‌ی گراف‌ها (نشان می‌دهد گراف چند دقیقه را پوشش می‌دهد)</summary>
        private void UpdateSpanLabels()
        {
            // حالت زنده: ۶۰ نقطه × فاصله‌ی رفرش
            string live = string.Format(L10n.T("Last {0} s"),
                Math.Max(1, (int)Math.Round(60 * AppSettings.RefreshIntervalMs / 1000.0)));

            static string ModeText(int mode, string live) => mode switch
            {
                1 => L10n.T("Last 10 minutes"),
                2 => L10n.T("Last hour"),
                _ => live,
            };

            MainGraph.SpanText = ModeText(_histMode, live);
            SecondGraph.SpanText = ModeText(SecondHistKey().Length > 0 ? _histMode2 : 0, live);
        }

        private void UpdateMainHistory()
        {
            int duration = _histMode == 1 ? 600 : 3600;
            int available = Monitoring.HistoryStore.Count;
            int offset = SliderOffset(HistSlider.Value, duration, available);

            MainGraph.SetStaticSeries(Monitoring.HistoryStore.GetSeries(HistKey(), duration, offset));
            HistLabel.Text = HistRangeText(offset, duration, available);
        }

        private void UpdateSecondHistory()
        {
            string key = SecondHistKey();
            if (key.Length == 0) { SecondGraph.ExitStatic(); return; }

            int duration = _histMode2 == 1 ? 600 : 3600;
            int available = Monitoring.HistoryStore.Count;
            int offset = SliderOffset(SecondHistSlider.Value, duration, available);

            SecondGraph.SetStaticSeries(Monitoring.HistoryStore.GetSeries(key, duration, offset));
            SecondHistLabel.Text = HistRangeText(offset, duration, available);
        }

        /// <summary>مقدار اسلایدر (۱۰۰ = تا همین الان) را به «چند ثانیه قبل» تبدیل می‌کند</summary>
        private static int SliderOffset(double sliderValue, int duration, int available)
        {
            int maxOffset = Math.Max(0, available - duration);
            return (int)((100 - sliderValue) / 100.0 * maxOffset);
        }

        private static string HistRangeText(int offset, int duration, int available)
        {
            var endAgo = TimeSpan.FromSeconds(offset);
            var startAgo = TimeSpan.FromSeconds(Math.Min(offset + duration, Math.Max(available, duration)));
            return $"-{(int)startAgo.TotalMinutes}m … {(offset == 0 ? L10n.T("now") : $"-{(int)endAgo.TotalMinutes}m")}";
        }

        private static string FormatSpeed(double kbs) =>
            kbs >= 1024 ? $"{kbs / 1024.0:F1} MB/s" : $"{kbs:F0} KB/s";

        private static string FormatMB(double mb) =>
            mb >= 1024 ? $"{mb / 1024.0:F1} GB" : $"{mb:F0} MB";

        private static string FormatUptime(double seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return $"{(int)t.TotalDays}:{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
        }
    }
}
