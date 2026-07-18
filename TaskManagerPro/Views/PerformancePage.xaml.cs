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
        private readonly List<TextBlock> _topNames = new();
        private readonly List<TextBlock> _topValues = new();

        public PerformancePage()
        {
            this.InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // مشخصات ثابت را در پس‌زمینه بخوان تا UI قفل نشود
            _cpuName = await Task.Run(HardwareInfo.GetCpuName);
            _gpuNames = await Task.Run(HardwareInfo.GetGpuNames);

            var first = await Task.Run(SystemMonitor.Instance.Read);

            if (Items.Count == 0)
            {
                Items.Add(new PerfSidebarItem { Key = "cpu", Glyph = "\uE950", Title = "CPU" });
                Items.Add(new PerfSidebarItem { Key = "memory", Glyph = "\uEEA0", Title = "Memory" });

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

                Items.Add(new PerfSidebarItem { Key = "disk", Glyph = "\uEDA2", Title = "Disk" });
                Items.Add(new PerfSidebarItem { Key = "network", Glyph = "\uE839", Title = "Network" });
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
            CoresCard.Visibility = Visibility.Collapsed;
            SecondCard.Visibility = Visibility.Collapsed;
            EnginesCard.Visibility = Visibility.Collapsed;
            SensorsCard.Visibility = Visibility.Collapsed;

            bool isSensors = item.Key == "sensors";
            MainCard.Visibility = isSensors ? Visibility.Collapsed : Visibility.Visible;
            StatsCard.Visibility = isSensors ? Visibility.Collapsed : Visibility.Visible;
            TopCard.Visibility = isSensors ? Visibility.Collapsed : Visibility.Visible;

            // برگشت به حالت زنده هنگام عوض شدن قطعه
            _histMode = 0;
            if (HistCombo.SelectedIndex != 0) HistCombo.SelectedIndex = 0;
            HistScrollRow.Visibility = Visibility.Collapsed;

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
                    MainGraphLabel.Text = "% Utilization";
                    CoresCard.Visibility = Visibility.Visible;
                    break;

                case "memory":
                    MainGraph.AutoScale = false;
                    MainGraph.MaxValue = 100;
                    MainGraphLabel.Text = "Memory usage (%)";
                    break;

                case "gpu":
                    // برای دمای GPU از LibreHardwareMonitor استفاده می‌شود — یک بار در پس‌زمینه باز شود
                    if (!SensorMonitor.IsStarted && !SensorMonitor.Failed)
                        _ = Task.Run(SensorMonitor.Start);
                    int gi = GpuIndex(item.Key);
                    DetailSubtitle.Text = gi < _gpuNames.Count ? _gpuNames[gi] : "";
                    MainGraph.AutoScale = false;
                    MainGraph.MaxValue = 100;
                    MainGraphLabel.Text = "% Utilization";

                    // گراف‌های موتورهای GPU مثل Task Manager ویندوز
                    EnginesCard.Visibility = Visibility.Visible;
                    foreach (var eg in new[] { Eng0Graph, Eng1Graph, Eng2Graph, Eng3Graph })
                    {
                        eg.AutoScale = false;
                        eg.MaxValue = 100;
                        eg.Clear();
                    }
                    break;

                case "disk":
                    MainGraph.AutoScale = false;
                    MainGraph.MaxValue = 100;
                    MainGraphLabel.Text = "Active time (%)";
                    SecondCard.Visibility = Visibility.Visible;
                    SecondGraph.AutoScale = true;
                    SecondGraphLabel.Text = "Transfer rate — Read + Write (MB/s)";
                    break;

                case "network":
                    MainGraph.AutoScale = true;
                    MainGraphLabel.Text = "Download";
                    SecondCard.Visibility = Visibility.Visible;
                    SecondGraph.AutoScale = true;
                    SecondGraphLabel.Text = "Upload";
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

                var (s, tops, sensors) = await Task.Run(() =>
                {
                    var snap = SystemMonitor.Instance.Read();
                    List<TopProc>? top = null;
                    if (topMetric.Length > 0)
                    {
                        try { NetworkMonitor.Instance.Snapshot(); } catch { }
                        ProcessSampler.Sample();
                        top = ProcessSampler.Top(topMetric);
                    }
                    var sens = wantSensors ? SensorMonitor.Read() : null;

                    // دمای GPU: اگر ویندوز گزارش نکرد، از LibreHardwareMonitor بگیر
                    if (wantGpuTemp && snap.GpuTempC <= 0 && SensorMonitor.IsStarted)
                    {
                        double t = SensorMonitor.ReadGpuTemp();
                        if (t > 0) snap.GpuTempC = t;
                    }

                    return (snap, top, sens);
                });

                UpdateSidebar(s);
                UpdateDetail(s);
                if (tops != null) UpdateTop(tops);
                if (sensors != null) UpdateSensors(sensors);
                if (_histMode > 0) UpdateHistory();
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
                            : "Not available";
                        break;
                    case "disk":
                        item.Subtitle = $"{s.DiskPercent:F0}%  •  R {s.DiskReadMBs:F1} / W {s.DiskWriteMBs:F1} MB/s";
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
                    UpdateEngine(Eng2Label, Eng2Graph, "Video Decode", "VideoDecode", g);
                    UpdateEngine(Eng3Label, Eng3Graph, "Video Processing", "VideoProcessing", g);
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
                    _statLabels[i].Text = stats[i].Label;
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
                    var label = new TextBlock { Text = $"Core {i}", FontSize = 12, Opacity = 0.8 };
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
                _coreLabels[i].Text = $"Core {i}: {s.CpuCores[i]:F0}%";
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
            "network" => "net",
            _ => "",
        };

        private void UpdateTop(List<TopProc> tops)
        {
            TopTitle.Text = L10n.T("Top processes");

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
                UpdateHistory();
            }
        }

        private void HistSlider_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_histMode > 0) UpdateHistory();
        }

        private void UpdateHistory()
        {
            int duration = _histMode == 1 ? 600 : 3600;
            int available = Monitoring.HistoryStore.Count;
            int maxOffset = Math.Max(0, available - duration);
            int offset = (int)((100 - HistSlider.Value) / 100.0 * maxOffset);

            var series = Monitoring.HistoryStore.GetSeries(HistKey(), duration, offset);
            MainGraph.SetStaticSeries(series);

            var endAgo = TimeSpan.FromSeconds(offset);
            var startAgo = TimeSpan.FromSeconds(Math.Min(offset + duration, available));
            HistLabel.Text = $"-{(int)startAgo.TotalMinutes}m … {(offset == 0 ? "now" : $"-{(int)endAgo.TotalMinutes}m")}";
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
