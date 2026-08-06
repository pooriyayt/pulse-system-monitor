using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using TaskManagerPro.Models;

namespace TaskManagerPro.Helpers
{
    /// <summary>
    /// خواندن و تغییر واقعی برنامه‌های استارتاپ ویندوز.
    ///
    /// منابعی که خوانده می‌شوند (همان‌هایی که ویندوز موقع بوت اجرا می‌کند):
    ///  - HKCU\...\Run   و  HKLM\...\Run   در هر دو نمای ۶۴ و ۳۲ بیتی (Wow6432Node)
    ///  - پوشه‌ی Startup کاربر فعلی و پوشه‌ی Startup همه‌ی کاربران
    ///
    /// غیرفعال کردن «واقعاً» انجام می‌شود، نه فقط ظاهری:
    ///  - آیتم رجیستری: مقدار از کلید Run برداشته و در کلید پشتیبان برنامه نگه داشته می‌شود
    ///    (کلید StartupApproved هم مثل Task Manager ویندوز به‌روز می‌شود).
    ///  - آیتم پوشه‌ای: فایل میانبر به زیرپوشه‌ی «Disabled by Pulse» منتقل می‌شود؛
    ///    ویندوز محتویات زیرپوشه‌ها را موقع بوت اجرا نمی‌کند.
    /// در هر دو حالت با روشن کردن دوباره‌ی کلید، آیتم دقیقاً سر جای اولش برمی‌گردد.
    /// </summary>
    public static class StartupScanner
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ApprovedRun32Key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
        private const string ApprovedFolderKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        /// <summary>کلید پشتیبان خود برنامه — آیتم‌های غیرفعال‌شده این‌جا نگه داشته می‌شوند</summary>
        private const string BackupKey = @"Software\Pulse\StartupBackup";

        private const string DisabledFolderName = "Disabled by Pulse";

        // ---------- خواندن ----------

        public static List<StartupItem> Scan()
        {
            var items = new List<StartupItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (isMachine, is32) in new[] { (false, false), (false, true), (true, false), (true, true) })
                ReadRegistry(items, seen, isMachine, is32);

            ReadBackups(items, seen);
            ReadFolder(items, seen, isMachine: false);
            ReadFolder(items, seen, isMachine: true);

            items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return items;
        }

        private static RegistryKey BaseKey(bool isMachine, bool is32) =>
            RegistryKey.OpenBaseKey(
                isMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                is32 ? RegistryView.Registry32 : RegistryView.Registry64);

        private static string Key(StartupItem i) =>
            $"{(int)i.Kind}|{i.IsMachine}|{i.Name}";

        private static void ReadRegistry(List<StartupItem> items, HashSet<string> seen, bool isMachine, bool is32)
        {
            try
            {
                using var baseKey = BaseKey(isMachine, is32);
                using var run = baseKey.OpenSubKey(RunKey);
                if (run == null) return;

                using var approved = baseKey.OpenSubKey(is32 ? ApprovedRun32Key : ApprovedRunKey);

                foreach (var name in run.GetValueNames())
                {
                    if (string.IsNullOrEmpty(name)) continue;

                    string command = run.GetValue(name)?.ToString() ?? "";
                    var item = new StartupItem
                    {
                        Name = name,
                        Command = command,
                        Enabled = IsApproved(approved, name),
                        Kind = StartupKind.Registry,
                        IsMachine = isMachine,
                        Is32BitView = is32,
                        Impact = EstimateImpact(command),
                        ExePath = ExtractExePath(command),
                    };

                    // نمای ۳۲ و ۶۴ بیتی برای بیشتر کلیدها یکی است — تکراری نشان نده
                    if (seen.Add(Key(item))) items.Add(item);
                }
            }
            catch
            {
                // اگر دسترسی خواندن نبود، از این منبع رد می‌شویم
            }
        }

        /// <summary>وضعیت فعال/غیرفعال از کلید StartupApproved (بایت اول زوج = فعال)</summary>
        private static bool IsApproved(RegistryKey? approved, string name)
        {
            if (approved?.GetValue(name) is byte[] bytes && bytes.Length > 0)
                return bytes[0] % 2 == 0;
            return true;
        }

        /// <summary>آیتم‌هایی که خودمان غیرفعال کرده‌ایم (از کلید Run برداشته شده‌اند)</summary>
        private static void ReadBackups(List<StartupItem> items, HashSet<string> seen)
        {
            try
            {
                using var root = Registry.CurrentUser.OpenSubKey(BackupKey);
                if (root == null) return;

                foreach (var sub in root.GetSubKeyNames())
                {
                    bool isMachine = sub.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase);
                    bool is32 = sub.EndsWith("32", StringComparison.OrdinalIgnoreCase);

                    using var key = root.OpenSubKey(sub);
                    if (key == null) continue;

                    foreach (var name in key.GetValueNames())
                    {
                        string command = key.GetValue(name)?.ToString() ?? "";
                        var item = new StartupItem
                        {
                            Name = name,
                            Command = command,
                            Enabled = false,
                            Kind = StartupKind.Registry,
                            IsMachine = isMachine,
                            Is32BitView = is32,
                            Impact = EstimateImpact(command),
                            ExePath = ExtractExePath(command),
                        };
                        if (seen.Add(Key(item))) items.Add(item);
                    }
                }
            }
            catch { }
        }

        private static string StartupFolder(bool isMachine) =>
            Environment.GetFolderPath(isMachine
                ? Environment.SpecialFolder.CommonStartup
                : Environment.SpecialFolder.Startup);

        private static void ReadFolder(List<StartupItem> items, HashSet<string> seen, bool isMachine)
        {
            try
            {
                string folder = StartupFolder(isMachine);
                if (folder.Length == 0 || !Directory.Exists(folder)) return;

                using var approved = Registry.CurrentUser.OpenSubKey(ApprovedFolderKey);

                void AddFile(string file, bool enabled)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.Length == 0 || name.Equals("desktop", StringComparison.OrdinalIgnoreCase)) return;

                    string target = ResolveShortcut(file);
                    var item = new StartupItem
                    {
                        Name = name,
                        Command = target.Length > 0 ? target : file,
                        Enabled = enabled && IsApproved(approved, Path.GetFileName(file)),
                        Kind = StartupKind.Folder,
                        IsMachine = isMachine,
                        FilePath = file,
                        Impact = EstimateImpact(target.Length > 0 ? target : file),
                        ExePath = target.Length > 0 ? ExtractExePath(target) : "",
                    };
                    if (seen.Add(Key(item))) items.Add(item);
                }

                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    string ext = Path.GetExtension(file);
                    if (ext.Equals(".ini", StringComparison.OrdinalIgnoreCase)) continue;
                    AddFile(file, enabled: true);
                }

                // آیتم‌هایی که خودمان غیرفعال کرده‌ایم
                string disabledDir = Path.Combine(folder, DisabledFolderName);
                if (Directory.Exists(disabledDir))
                    foreach (var file in Directory.EnumerateFiles(disabledDir))
                        AddFile(file, enabled: false);
            }
            catch { }
        }

        // ---------- تغییر وضعیت ----------

        /// <summary>
        /// فعال/غیرفعال کردن واقعی یک آیتم. در صورت نداشتن دسترسی (آیتم‌های HKLM و
        /// پوشه‌ی مشترک) استثنا پرتاب می‌شود تا UI پیام «Run as administrator» بدهد.
        /// </summary>
        public static void SetEnabled(StartupItem item, bool enable)
        {
            if (item.Kind == StartupKind.Folder) SetFolderEnabled(item, enable);
            else SetRegistryEnabled(item, enable);
        }

        private static string BackupSubKey(StartupItem item) =>
            (item.IsMachine ? "HKLM" : "HKCU") + (item.Is32BitView ? "32" : "");

        private static void SetRegistryEnabled(StartupItem item, bool enable)
        {
            using var baseKey = BaseKey(item.IsMachine, item.Is32BitView);
            string approvedPath = item.Is32BitView ? ApprovedRun32Key : ApprovedRunKey;

            if (enable)
            {
                // مقدار را از پشتیبان به کلید Run برگردان
                string command = item.Command;
                using (var backup = Registry.CurrentUser.OpenSubKey($@"{BackupKey}\{BackupSubKey(item)}", writable: true))
                {
                    if (backup?.GetValue(item.Name) is string saved && saved.Length > 0) command = saved;
                }

                using (var run = baseKey.CreateSubKey(RunKey, writable: true)
                    ?? throw new InvalidOperationException("Cannot open Run key"))
                {
                    run.SetValue(item.Name, command, RegistryValueKind.String);
                }

                using (var backup = Registry.CurrentUser.OpenSubKey($@"{BackupKey}\{BackupSubKey(item)}", writable: true))
                {
                    try { backup?.DeleteValue(item.Name, throwOnMissingValue: false); } catch { }
                }

                WriteApproved(baseKey, approvedPath, item.Name, true);
                item.Command = command;
            }
            else
            {
                // اول پشتیبان بگیر، بعد از کلید Run بردار تا واقعاً موقع بوت اجرا نشود
                string command = item.Command;
                using (var run = baseKey.OpenSubKey(RunKey, writable: true))
                {
                    if (run != null)
                    {
                        if (run.GetValue(item.Name) is string cur && cur.Length > 0) command = cur;

                        using (var backup = Registry.CurrentUser.CreateSubKey($@"{BackupKey}\{BackupSubKey(item)}", writable: true))
                        {
                            backup?.SetValue(item.Name, command, RegistryValueKind.String);
                        }

                        run.DeleteValue(item.Name, throwOnMissingValue: false);
                    }
                }

                WriteApproved(baseKey, approvedPath, item.Name, false);
            }
        }

        /// <summary>هماهنگ نگه داشتن وضعیت با Task Manager خود ویندوز</summary>
        private static void WriteApproved(RegistryKey baseKey, string approvedPath, string name, bool enabled)
        {
            try
            {
                using var key = baseKey.CreateSubKey(approvedPath, writable: true);
                if (key == null) return;
                var bytes = new byte[12];
                bytes[0] = (byte)(enabled ? 0x02 : 0x03);
                key.SetValue(name, bytes, RegistryValueKind.Binary);
            }
            catch
            {
                // این کلید فقط برای هماهنگی است؛ اثر واقعی از جابه‌جایی مقدار Run می‌آید
            }
        }

        private static void SetFolderEnabled(StartupItem item, bool enable)
        {
            string folder = StartupFolder(item.IsMachine);
            string disabledDir = Path.Combine(folder, DisabledFolderName);
            string fileName = Path.GetFileName(item.FilePath);
            if (fileName.Length == 0) throw new InvalidOperationException("Startup shortcut not found");

            string activePath = Path.Combine(folder, fileName);
            string disabledPath = Path.Combine(disabledDir, fileName);

            if (enable)
            {
                if (File.Exists(disabledPath))
                {
                    File.Move(disabledPath, activePath, overwrite: true);
                    item.FilePath = activePath;
                }
            }
            else
            {
                Directory.CreateDirectory(disabledDir);
                if (File.Exists(activePath))
                {
                    File.Move(activePath, disabledPath, overwrite: true);
                    item.FilePath = disabledPath;
                }
            }

            // وضعیت را برای Task Manager ویندوز هم می‌نویسیم
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(ApprovedFolderKey, writable: true);
                if (key != null)
                {
                    var bytes = new byte[12];
                    bytes[0] = (byte)(enable ? 0x02 : 0x03);
                    key.SetValue(fileName, bytes, RegistryValueKind.Binary);
                }
            }
            catch { }
        }

        // ---------- کمکی ----------

        /// <summary>مسیر مقصد یک فایل .lnk را می‌خواند (بدون کتابخانه‌ی جانبی، با Shell COM)</summary>
        private static string ResolveShortcut(string path)
        {
            if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return path;
            try
            {
                var type = Type.GetTypeFromProgID("WScript.Shell");
                if (type == null) return "";
                object? shell = Activator.CreateInstance(type);
                if (shell == null) return "";

                object? sc = type.InvokeMember("CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { path });
                if (sc == null) return "";

                string target = sc.GetType().InvokeMember("TargetPath",
                    System.Reflection.BindingFlags.GetProperty, null, sc, null) as string ?? "";
                string args = sc.GetType().InvokeMember("Arguments",
                    System.Reflection.BindingFlags.GetProperty, null, sc, null) as string ?? "";

                if (target.Length == 0) return "";
                return args.Length > 0 ? $"\"{target}\" {args}" : target;
            }
            catch { return ""; }
        }

        /// <summary>
        /// تخمین تأثیر روی زمان بوت از روی اندازه‌ی فایل اجرایی + فایل‌های کنارش (DLLها).
        /// </summary>
        public static int EstimateImpact(string command)
        {
            try
            {
                string exe = ExtractExePath(command);
                if (exe.Length == 0 || !File.Exists(exe)) return 0;

                long size = new FileInfo(exe).Length;

                try
                {
                    var dir = Path.GetDirectoryName(exe);
                    if (dir != null)
                    {
                        int count = 0;
                        foreach (var f in Directory.EnumerateFiles(dir, "*.dll"))
                        {
                            size += new FileInfo(f).Length / 4; // وزن کمتر از خود EXE
                            if (++count >= 50) break;
                        }
                    }
                }
                catch { }

                double mb = size / (1024.0 * 1024.0);
                return mb >= 60 ? 3 : mb >= 15 ? 2 : 1;
            }
            catch { return 0; }
        }

        /// <summary>مسیر EXE را از رشته‌ی فرمان درمی‌آورد (با یا بدون کوتیشن و آرگومان)</summary>
        public static string ExtractExePath(string command)
        {
            command = command.Trim();
            if (command.Length == 0) return "";

            if (command.StartsWith('"'))
            {
                int end = command.IndexOf('"', 1);
                return end > 1 ? command.Substring(1, end - 1) : "";
            }

            int exeIdx = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx > 0) return command.Substring(0, exeIdx + 4);

            int space = command.IndexOf(' ');
            return space > 0 ? command.Substring(0, space) : command;
        }
    }
}
