using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace TaskManagerPro.Helpers
{
    /// <summary>اجرای دوباره‌ی برنامه (برای اعمال تنظیماتی مثل تغییر ظاهر، یا گرفتن دسترسی ادمین)</summary>
    public static class AppRestart
    {
        /// <summary>
        /// بستن و اجرای دوباره. نمونه‌ی جدید با --relaunch صبر می‌کند تا این نمونه کامل بسته شود
        /// (وگرنه محافظ اجرای چندباره آن را به همین نمونه‌ی در حال بسته شدن هدایت می‌کرد).
        /// </summary>
        public static void Restart()
        {
            // ۱) راه رسمی Windows App SDK (برای نسخه‌ی نصب‌شده)؛ در صورت موفقیت برنمی‌گردد
            try { AppInstance.Restart("--relaunch"); }
            catch { }

            // ۲) پشتیبان: اجرای مستقیم فایل و بستن همین نمونه
            try
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return;
                Process.Start(new ProcessStartInfo(exe, "--relaunch") { UseShellExecute = false });
            }
            catch { return; }

            App.IsExiting = true;
            try { App.MainAppWindow?.Close(); } catch { }
            Application.Current.Exit();
        }

        /// <summary>
        /// اجرای دوباره با Run as administrator. true یعنی کاربر UAC را تأیید کرد و این نمونه بسته می‌شود.
        /// نمونه‌ی جدید با آرگومان --relaunch صبر می‌کند تا این نمونه کاملاً بسته شود.
        /// </summary>
        public static bool RestartAsAdmin()
        {
            try
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return false;
                Process.Start(new ProcessStartInfo(exe, "--relaunch") { UseShellExecute = true, Verb = "runas" });
            }
            catch
            {
                return false; // کاربر UAC را رد کرد
            }

            App.IsExiting = true;
            try { App.MainAppWindow?.Close(); } catch { }
            Application.Current.Exit();
            return true;
        }
    }
}
