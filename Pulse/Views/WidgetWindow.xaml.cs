using System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TaskManagerPro.Helpers;
using TaskManagerPro.Monitoring;

namespace TaskManagerPro.Views
{
    /// <summary>
    /// ویجت شناور دسکتاپ: کارت شیشه‌ای always-on-top با گیج و گراف زنده‌ی CPU / RAM / GPU،
    /// سرعت شبکه و دمای CPU. با گرفتن نوار بالایی جابه‌جا می‌شود؛ دکمه‌ی × ویجت را خاموش می‌کند.
    /// </summary>
    public sealed partial class WidgetWindow : Window
    {
        public WidgetWindow()
        {
            this.InitializeComponent();

            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(DragBar);

            var aw = this.AppWindow;
            double scale = GetDpiScale();
            aw.Resize(new Windows.Graphics.SizeInt32((int)(380 * scale), (int)(232 * scale)));
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

            // داده از نمونه‌برداری سراسری (بدون خواندن دوباره‌ی شمارنده‌ها)
            HistoryStore.Sampled += OnSampled;
            this.Closed += (s, e) => HistoryStore.Sampled -= OnSampled;
            if (HistoryStore.Latest is SystemSnapshot last) Show(last);
        }

        private double GetDpiScale()
        {
            try { return GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0; }
            catch { return 1.0; }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        private void OnSampled(SystemSnapshot s) => DispatcherQueue.TryEnqueue(() => Show(s));

        private void Show(SystemSnapshot snap)
        {
            CpuRing.Value = snap.CpuTotal;
            RamRing.Value = snap.MemPercent;
            GpuRing.Value = Math.Max(snap.GpuPercent, 0);
            CpuValue.Text = $"{snap.CpuTotal:F0}%";
            RamValue.Text = $"{snap.MemPercent:F0}%";
            GpuValue.Text = snap.GpuPercent >= 0 ? $"{snap.GpuPercent:F0}%" : "—";
            CpuDetail.Text = snap.CpuMhz > 0 ? $"{snap.CpuMhz / 1000.0:F2} GHz" : " ";
            RamDetail.Text = $"{snap.MemUsedGB:F1} / {snap.MemTotalGB:F0} GB";
            GpuDetail.Text = snap.GpuTempC > 0 ? $"{snap.GpuTempC:F0} °C" : (snap.GpuPercent >= 0 ? "Active" : "N/A");
            DownText.Text = Speed(snap.NetRecvKBs);
            UpText.Text = Speed(snap.NetSentKBs);
            TempText.Text = snap.CpuTempC > 0 ? $"{snap.CpuTempC:F0} °C" : "—";
            ClockText.Text = DateTime.Now.ToString("HH:mm");
        }

        private static string Speed(double kbs) =>
            kbs >= 1024 ? $"{kbs / 1024.0:F1} MB/s" : $"{kbs:F0} KB/s";

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // خاموش کردن از تنظیمات — خود App پنجره را می‌بندد
            AppSettings.WidgetEnabled = false;
        }
    }
}
