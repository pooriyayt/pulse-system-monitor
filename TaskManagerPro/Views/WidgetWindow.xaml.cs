using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TaskManagerPro.Helpers;
using TaskManagerPro.Monitoring;

namespace TaskManagerPro.Views
{
    /// <summary>
    /// ویجت شناور دسکتاپ: پنجره‌ی کوچک always-on-top با گراف زنده‌ی CPU / RAM / GPU.
    /// با گرفتن نوار بالایی جابه‌جا می‌شود؛ دکمه‌ی × فقط ویجت را خاموش می‌کند.
    /// </summary>
    public sealed partial class WidgetWindow : Window
    {
        private DispatcherQueueTimer? _timer;
        private bool _busy;

        public WidgetWindow()
        {
            this.InitializeComponent();

            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(DragBar);

            var aw = this.AppWindow;
            aw.Resize(new Windows.Graphics.SizeInt32(340, 130));
            aw.IsShownInSwitchers = false;
            try { aw.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico")); } catch { }

            if (aw.Presenter is OverlappedPresenter p)
            {
                p.IsAlwaysOnTop = true;
                p.IsResizable = false;
                p.IsMaximizable = false;
                p.IsMinimizable = false;
                p.SetBorderAndTitleBar(true, false);
            }

            _timer = DispatcherQueue.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(AppSettings.RefreshIntervalMs, 1000));
            _timer.Tick += (s, e) => Tick();
            _timer.Start();

            this.Closed += (s, e) =>
            {
                _timer?.Stop();
                _timer = null;
            };

            Tick();
        }

        private void Tick()
        {
            if (_busy) return;
            _busy = true;
            var dq = DispatcherQueue;
            System.Threading.Tasks.Task.Run(() =>
            {
                SystemSnapshot? snap = null;
                try { snap = SystemMonitor.Instance.Read(); } catch { }
                dq.TryEnqueue(() =>
                {
                    _busy = false;
                    if (snap == null) return;
                    CpuGraph.AddValue(snap.CpuTotal);
                    RamGraph.AddValue(snap.MemPercent);
                    GpuGraph.AddValue(Math.Max(snap.GpuPercent, 0));
                    CpuLabel.Text = $"CPU {snap.CpuTotal:F0}%";
                    RamLabel.Text = $"RAM {snap.MemPercent:F0}%";
                    GpuLabel.Text = snap.GpuPercent >= 0 ? $"GPU {snap.GpuPercent:F0}%" : "GPU —";
                });
            });
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // خاموش کردن از تنظیمات — خود App پنجره را می‌بندد
            AppSettings.WidgetEnabled = false;
        }
    }
}
