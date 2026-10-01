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
        /// <summary>توضیح ثابت هر دیسک (حرف درایو + مدل) — زیرنویس سایدبار زنده است و عوض می‌شود</summary>
        private readonly Dictionary<int, string> _diskCaptions = new();

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
            PerfSubtitle.Text = L10n.T("Live graphs, history and sensors for every component");
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
                    "network" => L10n.T("Network"),
                    "sensors" => L10n.T("Sensors"),
                    _ when item.Key.StartsWith("gpu") => Items.Count(i => i.Key.StartsWith("gpu")) > 1
                        ? $"GPU {item.Key.Replace("gpu", "")}"
                        : "GPU",
                    _ when item.Key.StartsWith("disk") => Items.Count(i => i.Key.StartsWith("disk")) > 1
                        ? $"{L10n.T("Disk")} {item.Key.Replace("disk", "")}"
                        : L10n.T("Disk"),
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

                // یک آیتم برای هر دیسک فیزیکی (مثل Task Manager ویندوز)
                var diskIndexes = first.Disks.Select(d => d.Index).Distinct().OrderBy(i => i).ToList();
                if (diskIndexes.Count == 0) diskIndexes.Add(0);

                foreach (var di in diskIndexes)
                {
                    var d = first.Disks.FirstOrDefault(x => x.Index == di);
                    if (d != null) _diskCaptions[di] = DiskCaption(d);
                    Items.Add(new PerfSidebarItem
                    {
                        Key = $"disk{di}",
                        Glyph = "\uEDA2",
                        Title = diskIndexes.Count > 1 ? $"{L10n.T("Disk")} {di}" : L10n.T("Disk"),
                        Subtitle = d != null ? DiskCaption(d) : "",
                    });
                }
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
            ModulesCard.Visibility = Visibility.Collapsed;

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
                    if (_cpuInfo == null)
                        _ = Task.Run(Monitoring.CpuHardware.GetInfo).ContinueWith(t =>
                            DispatcherQueue.TryEnqueue(() => _cpuInfo = t.Result), TaskScheduler.Default);
                    break;

                case "memory":
                    MainGraph.AutoScale = false;
                    MainGraph.MaxValue = 100;
                    MainGraphLabel.Text = L10n.T("Memory usage (%)");
                    _ = LoadRamInfoAsync();
                    break;

                case "gpu":
                    // برای دمای GPU از LibreHardwareMonitor استفاده می‌شود — یک بار در پس‌زمینه باز شود
                    if (!SensorMonitor.IsStarted && !SensorMonitor.Failed)
                        _ = Task.Run(SensorMonitor.Start);
                    int gi = GpuIndex(item.Key);
                    DetailSubtitle.Text = gi < _gpuNames.Count ? _gpuNames[gi] : "";
                    if (_gpuStatic == null)
                        _ = Task.Run(Monitoring.GpuHardware.GetAll).ContinueWith(t =>
                            DispatcherQueue.TryEnqueue(() => _gpuStatic = t.Result), TaskScheduler.Default);
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
                    DetailSubtitle.Text = _diskCaptions.GetValueOrDefault(DiskIndex(item.Key), "");
                    MainGraph.AutoScale = false;
                    MainGraph.MaxValue = 100;
                    MainGraphLabel.Text = L10n.T("Active time (%)");
                    SecondCard.Visibility = Visibility.Visible;
                    SecondGraph.AutoScale = true;
                    SecondGraph.Unit = TaskManagerPro.Controls.GraphUnit.MegaBytesPerSec;
                    SecondGraphLabel.Text = L10n.T("Transfer rate — Read + Write (MB/s)");
                    // گراف انتقال هم مثل گراف بالا تاریخچه‌ی مستقل دارد
                    SecondHistCombo.Visibility = Visibility.Visible;
                    if (_diskStatic == null)
                        _ = Task.Run(Monitoring.DiskHardware.GetAll).ContinueWith(t =>
                            DispatcherQueue.TryEnqueue(() => _diskStatic = t.Result), TaskScheduler.Default);
                    // شمارنده‌های سلامت (دما / فرسودگی) با هر بار ورود دوباره خوانده می‌شوند
                    _ = Task.Run(Monitoring.DiskHardware.GetReliability).ContinueWith(t =>
                        DispatcherQueue.TryEnqueue(() => _diskRel = t.Result), TaskScheduler.Default);
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

        private static string KeyKind(string key) =>
            key.StartsWith("gpu") ? "gpu" : key.StartsWith("disk") ? "disk" : key;

        private static int GpuIndex(string key) =>
            int.TryParse(key.Substring(3), out var i) ? i : 0;

        private static int DiskIndex(string key) =>
            key.Length > 4 && int.TryParse(key.Substring(4), out var i) ? i : 0;

        /// <summary>توضیح کوتاه یک دیسک: حرف‌های درایو + مدل</summary>
        private static string DiskCaption(DiskStat d)
        {
            if (d.Letters.Length > 0 && d.Model.Length > 0) return $"{d.Letters}  •  {d.Model}";
            if (d.Model.Length > 0) return d.Model;
            return d.Letters;
        }

        private static DiskStat? FindDisk(SystemSnapshot s, int index)
        {
            var d = s.Disks.FirstOrDefault(x => x.Index == index);
            if (d == null && index < s.Disks.Count) d = s.Disks[index];
            return d;
        }

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
                    {
                        int dIdx = DiskIndex(item.Key);
                        var d = FindDisk(s, dIdx);
                        if (d != null && !_diskCaptions.ContainsKey(dIdx))
                        {
                            _diskCaptions[dIdx] = DiskCaption(d);
                            if (_selectedKey == item.Key) DetailSubtitle.Text = _diskCaptions[dIdx];
                        }
                        item.Subtitle = d != null
                            ? $"{d.ActivePercent:F0}%  •  {L10n.T("Read")} {d.ReadMBs:F1} / {L10n.T("Write")} {d.WriteMBs:F1} MB/s"
                            : $"{s.DiskPercent:F0}%  •  {L10n.T("Read")} {s.DiskReadMBs:F1} / {L10n.T("Write")} {s.DiskWriteMBs:F1} MB/s";
                        break;
                    }
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
                        ("Temperature", s.CpuTempC > 0 ? $"{s.CpuTempC:F0} °C" : "N/A"),
                        ("Base speed", _cpuInfo is { BaseMHz: > 0 } ci1 ? $"{ci1.BaseMHz / 1000.0:F2} GHz" : "N/A"),
                        ("Sockets", _cpuInfo is { Sockets: > 0 } ci2 ? ci2.Sockets.ToString() : "N/A"),
                        ("Cores", _cpuInfo is { Cores: > 0 } ci3 ? ci3.Cores.ToString() : "N/A"),
                        ("Logical processors", _cpuInfo is { LogicalProcessors: > 0 } ci4 ? ci4.LogicalProcessors.ToString() : "N/A"),
                        ("Virtualization", _cpuInfo is { Virtualization.Length: > 0 } ci5 ? L10n.T(ci5.Virtualization) : "N/A"),
                        ("L1 cache", _cpuInfo is { L1KB: > 0 } ci6 ? FormatCache(ci6.L1KB) : "N/A"),
                        ("L2 cache", _cpuInfo is { L2KB: > 0 } ci7 ? FormatCache(ci7.L2KB) : "N/A"),
                        ("L3 cache", _cpuInfo is { L3KB: > 0 } ci8 ? FormatCache(ci8.L3KB) : "N/A"));
                    UpdateCores(s);
                    break;

                case "memory":
                {
                    MainGraph.AddValue(s.MemPercent);
                    var mc = Monitoring.MemoryHardware.GetCounters();
                    if (_memTick++ % 3 == 0) _compressed = Monitoring.MemoryHardware.GetCompressedBytes();
                    var ram = _ramInfo;
                    SetStats(
                        ("In use", $"{s.MemUsedGB:F1} GB"),
                        ("Available", $"{s.MemAvailableGB:F1} GB"),
                        ("Committed", mc is { } c1 ? $"{Gb(c1.CommitTotal)} / {Gb(c1.CommitLimit)} GB" : "N/A"),
                        ("Cached", mc is { } c2 ? $"{Gb(c2.SystemCache)} GB" : "N/A"),
                        ("Paged pool", mc is { } c3 ? $"{c3.PagedPool / 1048576.0:F0} MB" : "N/A"),
                        ("Non-paged pool", mc is { } c4 ? $"{c4.NonPagedPool / 1048576.0:F0} MB" : "N/A"),
                        ("Compressed", _compressed >= 0 ? $"{_compressed / 1048576.0:F0} MB" : "N/A"),
                        ("Speed", ram != null && ram.SpeedMHz > 0 ? $"{ram.SpeedMHz} MT/s" : "N/A"),
                        ("Slots used", ram != null && ram.TotalSlots > 0 ? string.Format(L10n.T("{0} of {1}"), ram.UsedSlots, ram.TotalSlots) : "N/A"),
                        ("Form factor", ram != null && ram.FormFactor.Length > 0 ? ram.FormFactor : "N/A"),
                        ("Page file", $"{s.PageFilePercent:F0}% used"));
                    break;
                }

                case "gpu":
                {
                    var g = FindGpu(s, GpuIndex(_selectedKey));
                    MainGraph.AddValue(g?.UsagePercent ?? 0);
                    UpdateEngine(Eng0Label, Eng0Graph, "3D", "3D", g);
                    UpdateEngine(Eng1Label, Eng1Graph, "Copy", "Copy", g);
                    UpdateEngine(Eng2Label, Eng2Graph, L10n.T("Video Decode"), "VideoDecode", g);
                    UpdateEngine(Eng3Label, Eng3Graph, L10n.T("Video Processing"), "VideoProcessing", g);
                    int gIdx = GpuIndex(_selectedKey);
                    var gi = gIdx < _gpuNames.Count && _gpuStatic != null ? FindGpuInfo(_gpuNames[gIdx]) : null;
                    string dedicated = g == null ? "N/A"
                        : gi is { DedicatedBytes: > 0 } ? $"{FormatMB(g.DedicatedMB)} / {gi.DedicatedBytes / 1073741824.0:0.#} GB"
                        : FormatMB(g.DedicatedMB);
                    SetStats(
                        ("Utilization", g != null ? $"{g.UsagePercent:F0}%" : "N/A"),
                        ("Dedicated memory", dedicated),
                        ("Shared memory", $"{s.MemTotalGB / 2:F1} GB"),
                        ("Temperature", s.GpuTempC > 0 ? $"{s.GpuTempC:F0} °C" : "N/A"),
                        ("GPU type", gi != null ? L10n.T(gi.Integrated ? "Integrated" : "Dedicated") : "N/A"),
                        ("Vendor", gi is { Vendor.Length: > 0 } ? gi.Vendor : "N/A"),
                        ("Driver version", gi is { DriverVersion.Length: > 0 } ? gi.DriverVersion : "N/A"),
                        ("Driver date", gi?.DriverDate is DateTime dd ? dd.ToString("yyyy-MM-dd") : "N/A"),
                        ("Display", gi is { Resolution.Length: > 0 } ? gi.Resolution : L10n.T("Not driving a display")));
                    break;
                }

                case "disk":
                {
                    var d = FindDisk(s, DiskIndex(_selectedKey));
                    double active = d?.ActivePercent ?? s.DiskPercent;
                    double read = d?.ReadMBs ?? s.DiskReadMBs;
                    double write = d?.WriteMBs ?? s.DiskWriteMBs;

                    MainGraph.AddValue(active);
                    SecondGraph.AddValue(read + write);
                    var di = _diskStatic != null && _diskStatic.TryGetValue(DiskIndex(_selectedKey), out var dinfo) ? dinfo : null;
                    string type = di == null ? "N/A"
                        : string.Join(" ", new[] { di.Bus, di.MediaType }.Where(x => x.Length > 0)) is { Length: > 0 } t ? t : "N/A";
                    string link = di is { PcieGen: > 0 }
                        ? $"PCIe {di.PcieGen}.0" + (di.PcieLanes > 0 ? $" x{di.PcieLanes}" : "")
                          + (di.PcieMaxGen > di.PcieGen ? "  " + string.Format(L10n.T("(max {0})"), $"{di.PcieMaxGen}.0") : "")
                        : "N/A";
                    SetStats(
                        ("Active time", $"{active:F0}%"),
                        ("Read speed", $"{read:F1} MB/s"),
                        ("Write speed", $"{write:F1} MB/s"),
                        ("Brand", di is { Brand.Length: > 0 } ? di.Brand : "N/A"),
                        ("Type", type),
                        ("Interface", link),
                        ("Capacity", di is { SizeBytes: > 0 } ? FormatCapacity(di.SizeBytes) : "N/A"),
                        ("Health", di is { Health.Length: > 0 } ? L10n.T(di.Health) : "N/A"),
                        ("Firmware", di is { Firmware.Length: > 0 } ? di.Firmware : "N/A"),
                        ("Rotation speed", di is { SpindleRpm: > 0 } ? $"{di.SpindleRpm} RPM" : (di?.MediaType == "SSD" ? L10n.T("None (SSD)") : "N/A")),
                        ("Life remaining", RelText(r => r.WearPercent >= 0 ? $"{100 - r.WearPercent}%" : null)),
                        ("Disk temperature", RelText(r => r.TemperatureC > 0
                            ? $"{r.TemperatureC} °C" + (r.TemperatureMaxC > 0 ? $"  ({L10n.T("max")} {r.TemperatureMaxC} °C)" : "")
                            : null)),
                        ("Power-on hours", RelText(r => r.PowerOnHours >= 0 ? $"{r.PowerOnHours:N0} h" : null)),
                        ("Read / write errors", RelText(r => r.ReadErrors >= 0 || r.WriteErrors >= 0
                            ? $"{Math.Max(0, r.ReadErrors)} / {Math.Max(0, r.WriteErrors)}" : null)));
                    break;
                }

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
        private Monitoring.CpuInfo? _cpuInfo;
        private Dictionary<int, Monitoring.DiskInfo>? _diskStatic;
        private Dictionary<int, Monitoring.DiskReliability>? _diskRel;

        /// <summary>
        /// متن یک شمارنده‌ی سلامت دیسک انتخاب‌شده. بدون دسترسی ادمین ویندوز این اطلاعات را
        /// نمی‌دهد — به‌جای عدد ساختگی «نیاز به ادمین» نوشته می‌شود.
        /// </summary>
        private string RelText(Func<Monitoring.DiskReliability, string?> pick)
        {
            if (_diskRel == null) return "…";
            if (_diskRel.TryGetValue(DiskIndex(_selectedKey), out var r))
                return pick(r) ?? "N/A";
            return AdminHelper.IsAdmin ? "N/A" : L10n.T("Needs admin");
        }

        private static string FormatCapacity(ulong bytes)
        {
            // مثل برچسب سازنده (اعشاری): 512 GB، 1 TB
            double gb = bytes / 1e9;
            return gb >= 1000 ? $"{gb / 1000:0.##} TB" : $"{gb:0} GB";
        }
        private List<Monitoring.GpuInfo>? _gpuStatic;

        private static Monitoring.GpuInfo? FindGpuInfo(string name) => Monitoring.GpuHardware.Find(name);

        private static string FormatCache(uint kb) => kb >= 1024 ? $"{kb / 1024.0:0.#} MB" : $"{kb} KB";

        private Monitoring.RamInfo? _ramInfo;
        private long _compressed = -1;
        private int _memTick;

        private static string Gb(ulong bytes) => (bytes / 1073741824.0).ToString("F1");

        /// <summary>مشخصات ثابت رم (نوع، سرعت، اسلات‌ها، ماژول‌ها) — یک بار در پس‌زمینه</summary>
        private async Task LoadRamInfoAsync()
        {
            ModulesCard.Visibility = Visibility.Visible;
            _ramInfo ??= await Task.Run(Monitoring.MemoryHardware.GetInfo);
            var info = _ramInfo;
            if (_selectedKey != "memory") return;

            ulong total = 0;
            foreach (var m in info.Modules) total += m.CapacityBytes;
            var parts = new List<string>();
            if (total > 0) parts.Add($"{total / 1073741824.0:F0} GB");
            if (info.Type.Length > 0) parts.Add(info.Type);
            if (info.SpeedMHz > 0) parts.Add($"{info.SpeedMHz} MT/s");
            if (info.TotalSlots > 0) parts.Add(string.Format(L10n.T("{0} of {1} slots used"), info.UsedSlots, info.TotalSlots));
            DetailSubtitle.Text = string.Join("  ·  ", parts);

            ModulesTitle.Text = L10n.T("Installed memory modules");
            ModulesPanel.Children.Clear();
            for (int i = 0; i < Math.Max(info.TotalSlots, info.Modules.Count); i++)
            {
                var row = new Grid { ColumnSpacing = 12 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                if (i < info.Modules.Count)
                {
                    var m = info.Modules[i];
                    row.Children.Add(new TextBlock { Text = m.Slot.Length > 0 ? m.Slot : $"Slot {i + 1}", Opacity = 0.7, FontSize = 13 });
                    var mid = new TextBlock
                    {
                        Text = string.Join("  ·  ", new[] { m.Manufacturer, m.PartNumber, m.Type, m.FormFactor }.Where(t => t.Length > 0)),
                        FontSize = 13,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    };
                    Grid.SetColumn(mid, 1);
                    row.Children.Add(mid);
                    uint mhz = m.ConfiguredMHz > 0 ? m.ConfiguredMHz : m.SpeedMHz;
                    var right = new TextBlock
                    {
                        Text = $"{m.CapacityBytes / 1073741824.0:F0} GB" + (mhz > 0 ? $"  ·  {mhz} MT/s" : "")
                            + (m.SpeedMHz > mhz && mhz > 0 ? "  " + string.Format(L10n.T("(rated {0})"), m.SpeedMHz) : ""),
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        FontSize = 13,
                    };
                    Grid.SetColumn(right, 2);
                    row.Children.Add(right);
                }
                else
                {
                    row.Children.Add(new TextBlock { Text = $"Slot {i + 1}", Opacity = 0.7, FontSize = 13 });
                    var empty = new TextBlock { Text = L10n.T("Empty slot"), Opacity = 0.5, FontSize = 13 };
                    Grid.SetColumn(empty, 1);
                    row.Children.Add(empty);
                }
                ModulesPanel.Children.Add(row);
            }
            if (info.Modules.Count == 0)
                ModulesPanel.Children.Add(new TextBlock { Text = L10n.T("Module details are not available on this system."), Opacity = 0.6 });
        }

        private void SetStats(params (string Label, string Value)[] stats)
        {
            while (_statValues.Count < stats.Length)
            {
                var value = new TextBlock
                {
                    FontSize = 22,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["DisplayFont"],
                };
                var label = new TextBlock { FontSize = 12, Opacity = 0.65, TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis };

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
                var grid = new Grid { Padding = new Thickness(0, 3, 0, 3) };
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
                var grid = new Grid { Padding = new Thickness(0, 3, 0, 3) };
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
            "disk" => Monitoring.HistoryStore.HasDisk(DiskIndex(_selectedKey))
                ? Monitoring.HistoryStore.DiskKey(DiskIndex(_selectedKey))
                : "disk",
            "network" => "netdown",
            _ => "cpu",
        };

        /// <summary>کلید HistoryStore برای گراف دوم ("" یعنی تاریخچه ندارد)</summary>
        private string SecondHistKey() => KeyKind(_selectedKey) switch
        {
            "network" => "netup",
            "disk" => Monitoring.HistoryStore.HasDisk(DiskIndex(_selectedKey))
                ? Monitoring.HistoryStore.DiskIoKey(DiskIndex(_selectedKey))
                : "diskio",
            _ => "",
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
