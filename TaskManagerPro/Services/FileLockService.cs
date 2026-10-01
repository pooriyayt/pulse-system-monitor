using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ComFileTime = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace TaskManagerPro.Services
{
    /// <summary>یک پردازه که فایل/پوشه را قفل کرده است</summary>
    public sealed class LockingProcessInfo
    {
        public int Pid { get; init; }
        public string AppName { get; init; } = "";
        public string ServiceName { get; init; } = "";
        public string? Path { get; init; }
        public bool IsService { get; init; }
        public bool IsCritical { get; init; }
    }

    /// <summary>
    /// «چه چیزی این فایل را قفل کرده؟» — با Restart Manager ویندوز (rstrtmgr.dll)
    /// برای پوشه‌ها، فایل‌های داخل پوشه (تا سقف مشخص) بررسی می‌شوند.
    /// </summary>
    public static class FileLockService
    {
        /// <summary>سقف تعداد فایل‌هایی که داخل یک پوشه بررسی می‌شوند</summary>
        public const int MaxFolderFiles = 5000;

        /// <summary>[ترد پس‌زمینه] پردازه‌هایی که مسیر داده‌شده را باز نگه داشته‌اند</summary>
        public static List<LockingProcessInfo> FindLockers(string path)
        {
            var files = CollectFiles(path);
            if (files.Length == 0) return new List<LockingProcessInfo>();

            using var session = RmSession.Start();
            session.Register(files);

            var result = new List<LockingProcessInfo>();
            foreach (var info in session.GetList())
            {
                int pid = info.Process.dwProcessId;
                result.Add(new LockingProcessInfo
                {
                    Pid = pid,
                    AppName = info.strAppName ?? "",
                    ServiceName = info.strServiceShortName ?? "",
                    Path = ProcessPathResolver.GetPath(pid),
                    IsService = info.ApplicationType == RM_APP_TYPE.RmService,
                    IsCritical = info.ApplicationType == RM_APP_TYPE.RmCritical,
                });
            }
            return result
                .GroupBy(r => r.Pid)
                .Select(g => g.First())
                .OrderBy(r => r.AppName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// [ترد پس‌زمینه] آزاد کردن قفل: همه‌ی پردازه‌های قفل‌کننده با RmShutdown بسته می‌شوند.
        /// خروجی کد خطای Win32 است (0 یعنی موفق).
        /// </summary>
        public static int ForceUnlock(string path)
        {
            var files = CollectFiles(path);
            if (files.Length == 0) return 0;

            using var session = RmSession.Start();
            session.Register(files);
            return RmShutdown(session.Handle, RmForceShutdown, IntPtr.Zero);
        }

        private static string[] CollectFiles(string path)
        {
            if (File.Exists(path)) return new[] { path };
            if (!Directory.Exists(path)) throw new FileNotFoundException(null, path);

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };
            return Directory.EnumerateFiles(path, "*", options).Take(MaxFolderFiles).ToArray();
        }

        // ---- Session wrapper ----

        private sealed class RmSession : IDisposable
        {
            public uint Handle { get; }

            private RmSession(uint handle) => Handle = handle;

            public static RmSession Start()
            {
                var key = new StringBuilder(CCH_RM_SESSION_KEY + 1);
                int err = RmStartSession(out uint handle, 0, key);
                if (err != 0) throw new System.ComponentModel.Win32Exception(err);
                return new RmSession(handle);
            }

            public void Register(string[] files)
            {
                int err = RmRegisterResources(Handle, (uint)files.Length, files, 0, null, 0, null);
                if (err != 0) throw new System.ComponentModel.Win32Exception(err);
            }

            public RM_PROCESS_INFO[] GetList()
            {
                uint count = 0;
                uint reasons = 0;
                int err = RmGetList(Handle, out uint needed, ref count, null, ref reasons);

                // ممکن است بین دو فراخوانی لیست بزرگ‌تر شود — چند بار تلاش
                for (int attempt = 0; err == ERROR_MORE_DATA && attempt < 5; attempt++)
                {
                    var arr = new RM_PROCESS_INFO[needed];
                    count = needed;
                    err = RmGetList(Handle, out needed, ref count, arr, ref reasons);
                    if (err == 0) return arr.Take((int)count).ToArray();
                }

                if (err != 0) throw new System.ComponentModel.Win32Exception(err);
                return Array.Empty<RM_PROCESS_INFO>();
            }

            public void Dispose() => RmEndSession(Handle);
        }

        // ---- Native ----

        private const int CCH_RM_SESSION_KEY = 32; // sizeof(GUID) * 2
        private const int CCH_RM_MAX_APP_NAME = 255;
        private const int CCH_RM_MAX_SVC_NAME = 63;
        private const int ERROR_MORE_DATA = 234;
        private const uint RmForceShutdown = 0x1;

        private enum RM_APP_TYPE
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public ComFileTime ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
            public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
            public string strServiceShortName;
            public RM_APP_TYPE ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(
            uint pSessionHandle,
            uint nFiles, string[] rgsFilenames,
            uint nApplications, RM_UNIQUE_PROCESS[]? rgApplications,
            uint nServices, string[]? rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(
            uint dwSessionHandle,
            out uint pnProcInfoNeeded,
            ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[]? rgAffectedApps,
            ref uint lpdwRebootReasons);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmShutdown(uint dwSessionHandle, uint lActionFlags, IntPtr fnStatus);
    }
}
