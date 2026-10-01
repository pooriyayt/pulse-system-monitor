using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using TaskManagerPro.Helpers;

namespace TaskManagerPro.Services
{
    /// <summary>نتیجه‌ی یک بار بهینه‌سازی حافظه</summary>
    public sealed class MemoryOptimizeResult
    {
        /// <summary>حجم آزادشده از Standby List (بایت). -1 یعنی قابل اندازه‌گیری نبود.</summary>
        public long StandbyFreedBytes { get; init; } = -1;

        /// <summary>حجم کم‌شده از Working Set پردازه‌ها (بایت)</summary>
        public long WorkingSetTrimmedBytes { get; init; }

        /// <summary>تعداد پردازه‌هایی که Working Set آن‌ها خالی شد</summary>
        public int ProcessesTrimmed { get; init; }

        /// <summary>آیا پاک‌سازی Standby List انجام شد؟ (نیاز به Administrator)</summary>
        public bool StandbyPurged { get; init; }
    }

    /// <summary>
    /// بهینه‌ساز حافظه:
    /// 1) خالی کردن Working Set پردازه‌ها با EmptyWorkingSet (psapi)
    /// 2) پاک کردن Standby List با NtSetSystemInformation (ntdll) — فقط با Run as administrator
    /// </summary>
    public static class MemoryOptimizerService
    {
        public static MemoryOptimizeResult Optimize(bool trimWorkingSets = true, bool purgeStandby = true)
        {
            // SeDebugPrivilege دسترسی به پردازه‌های بیشتری می‌دهد (اگر ادمین باشیم)
            TryEnablePrivilege("SeDebugPrivilege");
            TryEnablePrivilege("SeIncreaseQuotaPrivilege");
            bool canPurge = purgeStandby && TryEnablePrivilege("SeProfileSingleProcessPrivilege");

            long standbyBefore = canPurge ? QueryStandbyBytes() : -1;

            // اول Working Setها خالی شوند (صفحات به Standby/Modified می‌روند) و بعد Standby پاک شود
            var (trimmed, count) = trimWorkingSets ? TrimWorkingSets() : (0L, 0);

            bool purged = false;
            long standbyFreed = -1;
            if (canPurge)
            {
                long standbyMid = QueryStandbyBytes();
                int command = MemoryPurgeStandbyList;
                int status = NtSetSystemInformation(SystemMemoryListInformation, ref command, sizeof(int));
                purged = status >= 0;

                long standbyAfter = QueryStandbyBytes();
                long reference = standbyMid >= 0 ? standbyMid : standbyBefore;
                if (purged && reference >= 0 && standbyAfter >= 0)
                    standbyFreed = Math.Max(0, reference - standbyAfter);
            }

            return new MemoryOptimizeResult
            {
                StandbyFreedBytes = standbyFreed,
                StandbyPurged = purged,
                WorkingSetTrimmedBytes = trimmed,
                ProcessesTrimmed = count,
            };
        }

        /// <summary>حجم فعلی Standby List (بایت) یا -1 اگر قابل خواندن نیست (نیاز به Administrator)</summary>
        public static long GetStandbyBytes() =>
            TryEnablePrivilege("SeProfileSingleProcessPrivilege") ? QueryStandbyBytes() : -1;

        /// <summary>کل / در دسترس حافظه‌ی فیزیکی (بایت)</summary>
        public static (ulong total, ulong available) GetPhysicalMemory()
        {
            var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            return GlobalMemoryStatusEx(ref m) ? (m.ullTotalPhys, m.ullAvailPhys) : (0UL, 0UL);
        }

        /// <summary>آیا پاک کردن Standby List ممکن است؟ (فقط Administrator)</summary>
        public static bool CanPurgeStandby => AdminHelper.IsAdmin;

        // ---- Working Set ----

        private static (long trimmedBytes, int count) TrimWorkingSets()
        {
            long trimmed = 0;
            int count = 0;
            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    int pid = p.Id;
                    if (pid <= 4) continue; // Idle و System

                    using var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_QUOTA, false, pid);
                    if (handle.IsInvalid) continue;

                    long before = GetWorkingSet(handle);
                    if (!EmptyWorkingSet(handle)) continue;
                    long after = GetWorkingSet(handle);

                    count++;
                    if (before > 0 && after >= 0 && before > after) trimmed += before - after;
                }
            }
            return (trimmed, count);
        }

        private static long GetWorkingSet(SafeProcessHandle handle)
        {
            var counters = new PROCESS_MEMORY_COUNTERS { cb = (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS>() };
            return GetProcessMemoryInfo(handle, out counters, counters.cb)
                ? (long)(ulong)counters.WorkingSetSize
                : -1;
        }

        // ---- Standby List ----

        /// <summary>حجم فعلی Standby List (مجموع همه‌ی اولویت‌ها) یا -1</summary>
        private static long QueryStandbyBytes()
        {
            int size = Marshal.SizeOf<SYSTEM_MEMORY_LIST_INFORMATION>();
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                int status = NtQuerySystemInformation(SystemMemoryListInformation, buffer, size, out _);
                if (status < 0) return -1;

                var info = Marshal.PtrToStructure<SYSTEM_MEMORY_LIST_INFORMATION>(buffer);
                ulong pages = 0;
                foreach (var c in info.PageCountByPriority) pages += (ulong)c;
                return (long)(pages * (ulong)Environment.SystemPageSize);
            }
            catch
            {
                return -1;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // ---- Privileges ----

        /// <summary>فعال کردن یک Privilege روی توکن همین پردازه؛ true فقط اگر واقعاً داده شد</summary>
        internal static bool TryEnablePrivilege(string name)
        {
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token))
                return false;

            using (token)
            {
                if (!LookupPrivilegeValue(null, name, out var luid)) return false;

                var tp = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED,
                };
                if (!AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                    return false;

                // AdjustTokenPrivileges حتی وقتی Privilege را ندارد true برمی‌گرداند
                return Marshal.GetLastWin32Error() != ERROR_NOT_ALL_ASSIGNED;
            }
        }

        // ---- Native ----

        private const int SystemMemoryListInformation = 80;
        private const int MemoryPurgeStandbyList = 4;

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint PROCESS_SET_QUOTA = 0x0100;

        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
        private const int ERROR_NOT_ALL_ASSIGNED = 1300;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        // فقط یک Privilege — پس آرایه‌ی LUID_AND_ATTRIBUTES مستقیم داخل ساختار است
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_MEMORY_COUNTERS
        {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_MEMORY_LIST_INFORMATION
        {
            public UIntPtr ZeroPageCount;
            public UIntPtr FreePageCount;
            public UIntPtr ModifiedPageCount;
            public UIntPtr ModifiedNoWritePageCount;
            public UIntPtr BadPageCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public UIntPtr[] PageCountByPriority;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public UIntPtr[] RepurposedPagesByPriority;
            public UIntPtr ModifiedPageCountPageFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

        [DllImport("ntdll.dll")]
        private static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

        [DllImport("ntdll.dll")]
        private static extern int NtQuerySystemInformation(int infoClass, IntPtr info, int length, out int returnLength);

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyWorkingSet(SafeProcessHandle hProcess);

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessMemoryInfo(SafeProcessHandle hProcess, out PROCESS_MEMORY_COUNTERS counters, uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int pid);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string? systemName, string name, out LUID luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(
            SafeAccessTokenHandle token, [MarshalAs(UnmanagedType.Bool)] bool disableAll,
            ref TOKEN_PRIVILEGES newState, uint bufferLength, IntPtr previousState, IntPtr returnLength);
    }
}
