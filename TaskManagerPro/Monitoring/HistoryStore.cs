using System;
using System.Collections.Generic;
using Microsoft.UI.Dispatching;
using TaskManagerPro.Helpers;

namespace TaskManagerPro.Monitoring
{
    /// <summary>
    /// تاریخچه‌ی یک‌ساعته‌ی متریک‌های اصلی سیستم (نمونه‌برداری هر ثانیه).
    /// برای گراف تاریخچه‌دار (۱۰ دقیقه / ۱ ساعت با اسکرول به عقب) و mini-گراف Tray.
    /// همچنین آلارم مصرف از همین‌جا چک می‌شود.
    /// </summary>
    public static class HistoryStore
    {
        public const int Capacity = 3600; // یک ساعت با رزولوشن ۱ ثانیه

        private static readonly object Lock = new();
        private static readonly Dictionary<string, double[]> Buffers = new();
        private static int _count;
        private static int _head;
        private static DispatcherQueueTimer? _timer;
        private static bool _reading;

        private static readonly string[] Keys = { "cpu", "mem", "gpu", "disk", "netdown", "netup" };

        static HistoryStore()
        {
            foreach (var k in Keys) Buffers[k] = new double[Capacity];
        }

        /// <summary>تعداد نمونه‌های ذخیره‌شده تا الان</summary>
        public static int Count { get { lock (Lock) return _count; } }

        /// <summary>شروع نمونه‌برداری سراسری (یک بار در OnLaunched)</summary>
        public static void Start(DispatcherQueue dq)
        {
            if (_timer != null) return;
            _timer = dq.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => Sample();
            _timer.Start();
        }

        private static void Sample()
        {
            if (_reading) return;
            _reading = true;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var s = SystemMonitor.Instance.Read();
                    lock (Lock)
                    {
                        Buffers["cpu"][_head] = s.CpuTotal;
                        Buffers["mem"][_head] = s.MemPercent;
                        Buffers["gpu"][_head] = Math.Max(s.GpuPercent, 0);
                        Buffers["disk"][_head] = s.DiskPercent;
                        Buffers["netdown"][_head] = s.NetRecvKBs;
                        Buffers["netup"][_head] = s.NetSentKBs;
                        _head = (_head + 1) % Capacity;
                        if (_count < Capacity) _count++;
                    }
                    AlarmManager.Check(s);
                }
                catch { }
                finally { _reading = false; }
            });
        }

        /// <summary>
        /// یک پنجره از تاریخچه: durationSec ثانیه که offsetSec ثانیه قبل تمام می‌شود
        /// (offset = 0 یعنی تا همین الان). خروجی به points نقطه فشرده می‌شود (میانگین).
        /// </summary>
        public static double[] GetSeries(string key, int durationSec, int offsetSec, int points = 120)
        {
            lock (Lock)
            {
                if (!Buffers.TryGetValue(key, out var buf) || _count == 0)
                    return Array.Empty<double>();

                durationSec = Math.Clamp(durationSec, 2, Capacity);
                offsetSec = Math.Clamp(offsetSec, 0, Math.Max(0, _count - durationSec));

                int available = Math.Min(durationSec, _count - offsetSec);
                if (available < 2) return Array.Empty<double>();

                // ایندکس قدیمی‌ترین نمونه‌ی پنجره در بافر حلقوی
                int newest = (_head - 1 - offsetSec + Capacity * 2) % Capacity;
                int oldest = (newest - (available - 1) + Capacity * 2) % Capacity;

                var raw = new double[available];
                for (int i = 0; i < available; i++)
                    raw[i] = buf[(oldest + i) % Capacity];

                if (available <= points) return raw;

                // فشرده‌سازی با میانگین گرفتن هر بازه
                var result = new double[points];
                double step = (double)available / points;
                for (int i = 0; i < points; i++)
                {
                    int a = (int)(i * step);
                    int b = Math.Max(a + 1, (int)((i + 1) * step));
                    double sum = 0;
                    for (int j = a; j < b && j < available; j++) sum += raw[j];
                    result[i] = sum / (b - a);
                }
                return result;
            }
        }

        /// <summary>آخرین n نمونه‌ی یک متریک (برای mini-گراف Tray)</summary>
        public static double[] GetRecent(string key, int n) => GetSeries(key, n, 0, n);
    }
}
