using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        private readonly List<ProgressBar> _coreBars = new();
        private readonly List<TextBlock> _coreLabels = new();

        public OverviewPage()
        {
            this.InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
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
                // خواندن شمارنده‌ها در Thread جدا تا UI روان بماند
                var s = await Task.Run(SystemMonitor.Instance.Read);
                UpdateUi(s);
            }
            catch
            {
                // خطاهای موقتی شمارنده‌ها مهم نیستند؛ در آپدیت بعدی جبران می‌شود
            }
            finally
            {
                _busy = false;
            }
        }

        private void UpdateUi(SystemSnapshot s)
        {
            // CPU (درصد + سرعت لحظه‌ای + دما اگر در دسترس باشد)
            CpuGraph.AddValue(s.CpuTotal);
            var cpuLine = $"{s.CpuTotal:F0}%";
            if (s.CpuMhz > 0) cpuLine += $"   |   {s.CpuMhz / 1000.0:F2} GHz";
            if (s.CpuTempC > 0) cpuLine += $"   |   {s.CpuTempC:F0} °C";
            CpuText.Text = cpuLine;

            if (_coreBars.Count == 0 && s.CpuCores.Length > 0)
                BuildCoreBars(s.CpuCores.Length);

            for (int i = 0; i < _coreBars.Count && i < s.CpuCores.Length; i++)
            {
                _coreBars[i].Value = s.CpuCores[i];
                _coreLabels[i].Text = $"Core {i}: {s.CpuCores[i]:F0}%";
            }

            // Memory + Page File
            MemGraph.AddValue(s.MemPercent);
            MemText.Text = $"{s.MemUsedGB:F1} / {s.MemTotalGB:F1} GB  ({s.MemPercent:F0}%)";
            PageFileText.Text = $"Page File: {s.PageFilePercent:F0}% used";

            // GPU ها — همه‌ی کارت‌ها به تفکیک (شامل GPU داخلی Intel)
            if (s.Gpus.Count > 0)
            {
                GpuGraph.AddValue(Math.Max(s.GpuPercent, 0));

                var lines = new List<string>();
                foreach (var g in s.Gpus)
                {
                    string temp = s.GpuTempC > 0 ? $"   |   {s.GpuTempC:F0} °C" : "";
                    lines.Add($"{g.Name}:  {g.UsagePercent:F0}%   |   VRAM: {FormatMB(g.DedicatedMB)}{temp}");
                }
                GpuText.Text = string.Join("\n", lines);
            }
            else
            {
                GpuText.Text = "GPU usage is not available on this system";
            }

            // Disk
            DiskGraph.AddValue(s.DiskPercent);
            DiskText.Text = $"{s.DiskPercent:F0}%   |   Read: {s.DiskReadMBs:F1} MB/s   |   Write: {s.DiskWriteMBs:F1} MB/s";

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

        private void BuildCoreBars(int count)
        {
            for (int i = 0; i < count; i++)
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
    }
}
