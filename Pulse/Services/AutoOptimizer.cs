using System;
using System.Threading.Tasks;
using TaskManagerPro.Helpers;
using TaskManagerPro.Monitoring;

namespace TaskManagerPro.Services
{
    /// <summary>
    /// بهینه‌سازی خودکار حافظه: وقتی مصرف رم از حد تعیین‌شده بیشتر شد (و حداکثر هر ۵ دقیقه یک بار)
    /// در پس‌زمینه حافظه را آزاد می‌کند.
    /// </summary>
    public static class AutoOptimizer
    {
        private static DateTime _last = DateTime.MinValue;
        private static bool _running;
        private static bool _started;

        /// <summary>آخرین نتیجه‌ی بهینه‌سازی خودکار (برای نمایش در صفحه)</summary>
        public static MemoryOptimizeResult? LastResult { get; private set; }
        public static DateTime LastRun => _last;
        public static event Action? Ran;

        public static void Start()
        {
            if (_started) return;
            _started = true;
            HistoryStore.Sampled += OnSample;
        }

        private static void OnSample(SystemSnapshot s)
        {
            if (!AppSettings.AutoOptimize || _running) return;
            if (s.MemPercent < AppSettings.AutoOptimizeLimit) return;
            if ((DateTime.UtcNow - _last).TotalMinutes < 5) return;

            _running = true;
            _last = DateTime.UtcNow;
            Task.Run(() =>
            {
                try
                {
                    LastResult = MemoryOptimizerService.Optimize(true, MemoryOptimizerService.CanPurgeStandby);
                    Ran?.Invoke();
                }
                catch { }
                finally { _running = false; }
            });
        }
    }
}
