using System;
using System.Threading.Tasks;

namespace TaskManagerPro.Monitoring
{
    /// <summary>
    /// آماده‌سازی شمارنده‌های سیستم در پس‌زمینه.
    ///
    /// ساختن <see cref="SystemMonitor"/> سنگین است (باز کردن ده‌ها Performance Counter،
    /// شمارنده‌های GPU و چند پرس‌وجوی WMI) و چند ثانیه طول می‌کشد. چون Instance یک فیلد
    /// استاتیک است، هر بار که کدی روی ترد UI به آن دست بزند — حتی به شکل
    /// Task.Run(SystemMonitor.Instance.Read) که Instance را قبل از رفتن به ترد دیگر
    /// می‌خواند — کل این ساخت روی ترد UI انجام می‌شود و پنجره فریز می‌ماند.
    ///
    /// این کلاس جدا از SystemMonitor است تا خواندن IsReady خودش باعث ساخته شدن
    /// Instance نشود.
    /// </summary>
    public static class MonitorWarmup
    {
        private static readonly object Lock = new();
        private static Task? _task;

        /// <summary>true یعنی شمارنده‌ها آماده‌اند و خواندن دیگر کند نیست</summary>
        public static bool IsReady { get; private set; }

        /// <summary>شروع (یا پیوستن به) آماده‌سازی پس‌زمینه</summary>
        public static Task StartAsync()
        {
            lock (Lock)
            {
                return _task ??= Task.Run(() =>
                {
                    try { _ = SystemMonitor.Instance; }
                    catch { }
                    finally { IsReady = true; }
                });
            }
        }
    }
}
