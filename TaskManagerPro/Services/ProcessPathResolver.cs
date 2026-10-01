using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace TaskManagerPro.Services
{
    /// <summary>
    /// مسیر و نام پردازه از روی PID با QueryFullProcessImageName
    /// (با PROCESS_QUERY_LIMITED_INFORMATION — بدون ادمین هم برای بیشتر پردازه‌ها کار می‌کند)
    /// </summary>
    public static class ProcessPathResolver
    {
        // کلید: PID + زمان شروع، تا PIDهای بازیافتی اشتباه نشوند
        private static readonly ConcurrentDictionary<(int, long), string?> Cache = new();

        public static string? GetPath(int pid)
        {
            if (pid <= 4) return null;

            using var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (handle.IsInvalid) return null;

            long start = GetProcessTimes(handle, out var creation, out _, out _, out _) ? creation : 0;
            return Cache.GetOrAdd((pid, start), _ =>
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                return QueryFullProcessImageName(handle, 0, sb, ref size) ? sb.ToString(0, size) : null;
            });
        }

        /// <summary>نام نمایشی پردازه (بدون .exe)</summary>
        public static string GetName(int pid, string? path)
        {
            if (pid == 0) return "System Idle Process";
            if (pid == 4) return "System";
            if (!string.IsNullOrEmpty(path)) return Path.GetFileNameWithoutExtension(path);
            try
            {
                using var p = System.Diagnostics.Process.GetProcessById(pid);
                return p.ProcessName;
            }
            catch
            {
                return "PID " + pid;
            }
        }

        /// <summary>پاک کردن کش (برای جلوگیری از رشد بی‌حد)</summary>
        public static void TrimCache()
        {
            if (Cache.Count > 4096) Cache.Clear();
        }

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(SafeProcessHandle hProcess, uint flags, StringBuilder exeName, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessTimes(SafeProcessHandle hProcess, out long creation, out long exit, out long kernel, out long user);
    }
}
