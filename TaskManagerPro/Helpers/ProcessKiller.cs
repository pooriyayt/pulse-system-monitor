using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace TaskManagerPro.Helpers
{
    /// <summary>نتیجه‌ی تلاش برای بستن پردازه</summary>
    public enum KillOutcome
    {
        Killed,
        AlreadyExited,
        AccessDenied,
        Cancelled,
        Failed,
    }

    /// <summary>
    /// بستن پردازه‌ها با پشتیبانی از ارتقای دسترسی:
    /// اگر برنامه ادمین نباشد و ویندوز اجازه ندهد، فقط یک taskkill با UAC اجرا می‌شود
    /// (کل برنامه دوباره با دسترسی ادمین باز نمی‌شود).
    /// </summary>
    public static class ProcessKiller
    {
        private const int ERROR_ACCESS_DENIED = 5;
        private const int ERROR_CANCELLED = 1223;

        /// <summary>تلاش عادی برای بستن پردازه (روی ترد پس‌زمینه)</summary>
        public static Task<KillOutcome> TryKillAsync(int pid) => Task.Run(() =>
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                p.Kill(true);
                p.WaitForExit(3000);
                return KillOutcome.Killed;
            }
            catch (ArgumentException)
            {
                return KillOutcome.AlreadyExited;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ERROR_ACCESS_DENIED)
            {
                return KillOutcome.AccessDenied;
            }
            catch (InvalidOperationException)
            {
                return KillOutcome.AlreadyExited;
            }
            catch
            {
                return KillOutcome.Failed;
            }
        });

        /// <summary>بستن پردازه‌ها با taskkill و درخواست UAC</summary>
        public static async Task<KillOutcome> KillElevatedAsync(IEnumerable<int> pids)
        {
            var list = pids.Where(p => p > 4).Distinct().ToList();
            if (list.Count == 0) return KillOutcome.AlreadyExited;

            try
            {
                var psi = new ProcessStartInfo("taskkill.exe",
                    "/F /T " + string.Join(" ", list.Select(p => "/PID " + p)))
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                using var proc = Process.Start(psi);
                if (proc == null) return KillOutcome.Failed;
                await proc.WaitForExitAsync();
                return proc.ExitCode == 0 ? KillOutcome.Killed : KillOutcome.Failed;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ERROR_CANCELLED)
            {
                return KillOutcome.Cancelled;
            }
            catch
            {
                return KillOutcome.Failed;
            }
        }
    }
}
