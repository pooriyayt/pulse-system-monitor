using System;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using TaskManagerPro.Monitoring;

namespace TaskManagerPro.Helpers
{
    /// <summary>
    /// آلارم مصرف: وقتی CPU / RAM / دما از حد تعیین‌شده رد شود، نوتیفیکیشن ویندوز می‌فرستد.
    /// هر آلارم حداکثر هر ۲ دقیقه یک بار تکرار می‌شود تا اسپم نشود، و فقط وقتی دوباره
    /// فعال می‌شود که مقدار یک بار زیر حد برگشته باشد.
    /// </summary>
    public static class AlarmManager
    {
        private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);

        private static DateTime _lastCpu = DateTime.MinValue, _lastRam = DateTime.MinValue, _lastTemp = DateTime.MinValue;
        private static bool _cpuHigh, _ramHigh, _tempHigh;
        private static bool _registered;

        /// <summary>با هر نمونه‌برداری HistoryStore صدا زده می‌شود</summary>
        public static void Check(SystemSnapshot s)
        {
            if (!AppSettings.AlarmEnabled) return;

            CheckOne(s.CpuTotal, AppSettings.AlarmCpuLimit, ref _cpuHigh, ref _lastCpu,
                L10n.IsFa ? "مصرف CPU بالا" : "High CPU usage",
                L10n.IsFa ? $"CPU به {s.CpuTotal:F0}% رسید (حد: {AppSettings.AlarmCpuLimit}%)"
                          : $"CPU reached {s.CpuTotal:F0}% (limit: {AppSettings.AlarmCpuLimit}%)");

            CheckOne(s.MemPercent, AppSettings.AlarmRamLimit, ref _ramHigh, ref _lastRam,
                L10n.IsFa ? "مصرف RAM بالا" : "High memory usage",
                L10n.IsFa ? $"RAM به {s.MemPercent:F0}% رسید (حد: {AppSettings.AlarmRamLimit}%)"
                          : $"Memory reached {s.MemPercent:F0}% (limit: {AppSettings.AlarmRamLimit}%)");

            if (s.CpuTempC > 0)
                CheckOne(s.CpuTempC, AppSettings.AlarmTempLimit, ref _tempHigh, ref _lastTemp,
                    L10n.IsFa ? "دمای بالا" : "High temperature",
                    L10n.IsFa ? $"دما به {s.CpuTempC:F0}°C رسید (حد: {AppSettings.AlarmTempLimit}°C)"
                              : $"Temperature reached {s.CpuTempC:F0}°C (limit: {AppSettings.AlarmTempLimit}°C)");
        }

        private static void CheckOne(double value, int limit, ref bool wasHigh, ref DateTime last, string title, string msg)
        {
            // هیسترزیس ۵ واحدی: تا زیر (حد − 5) برنگشته دوباره آلارم نمی‌دهد
            if (value < limit - 5) { wasHigh = false; return; }
            if (value < limit) return;

            var now = DateTime.UtcNow;
            if (wasHigh && now - last < Cooldown) return;

            wasHigh = true;
            last = now;
            Notify(title, msg);
        }

        private static void Notify(string title, string msg)
        {
            try
            {
                if (!_registered)
                {
                    AppNotificationManager.Default.Register();
                    _registered = true;
                }

                var notif = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(msg)
                    .BuildNotification();
                AppNotificationManager.Default.Show(notif);
            }
            catch
            {
                // اگر نوتیفیکیشن در دسترس نبود (مثلاً بدون MSIX)، بی‌صدا رد می‌شویم
            }
        }
    }
}
