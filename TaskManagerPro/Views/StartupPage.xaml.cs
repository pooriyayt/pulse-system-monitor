using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using TaskManagerPro.Models;

namespace TaskManagerPro.Views
{
    /// <summary>
    /// مدیریت برنامه‌های Startup از طریق رجیستری ویندوز.
    /// لیست برنامه‌ها از کلید Run خوانده می‌شود و وضعیت فعال/غیرفعال
    /// در کلید StartupApproved ذخیره می‌شود (همان روشی که Task Manager ویندوز استفاده می‌کند).
    /// </summary>
    public sealed partial class StartupPage : Page
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        public ObservableCollection<StartupItem> Items { get; } = new();

        public StartupPage()
        {
            this.InitializeComponent();
            Loaded += (s, e) => LoadItems();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadItems();

        private void LoadItems()
        {
            Items.Clear();

            LoadFromHive(Registry.CurrentUser, isMachine: false);
            LoadFromHive(Registry.LocalMachine, isMachine: true);

            StartupList.ItemsSource = Items;
            _ = LoadIconsAsync();
        }

        /// <summary>استخراج آیکون‌ها در پس‌زمینه و ست کردن روی ترد UI</summary>
        private async System.Threading.Tasks.Task LoadIconsAsync()
        {
            var paths = new List<string>();
            foreach (var item in Items)
                if (item.ExePath.Length > 0 && System.IO.File.Exists(item.ExePath))
                    paths.Add(item.ExePath);

            await System.Threading.Tasks.Task.Run(() =>
            {
                foreach (var p in paths)
                    Helpers.IconCache.Preload(p);
            });

            foreach (var item in Items)
                if (item.ExePath.Length > 0)
                    item.Icon = Helpers.IconCache.Get(item.ExePath);
        }

        private void LoadFromHive(RegistryKey hive, bool isMachine)
        {
            try
            {
                using var run = hive.OpenSubKey(RunKey);
                if (run == null) return;

                using var approved = hive.OpenSubKey(ApprovedKey);

                foreach (var name in run.GetValueNames())
                {
                    if (string.IsNullOrEmpty(name)) continue;

                    bool enabled = true;
                    // بایت اول زوج = فعال (0x02)، فرد = غیرفعال (0x03)
                    if (approved?.GetValue(name) is byte[] bytes && bytes.Length > 0)
                        enabled = bytes[0] % 2 == 0;

                    string command = run.GetValue(name)?.ToString() ?? "";
                    Items.Add(new StartupItem
                    {
                        Name = name,
                        Command = command,
                        Enabled = enabled,
                        IsMachine = isMachine,
                        Impact = EstimateImpact(command),
                        ExePath = ExtractExePath(command),
                    });
                }
            }
            catch
            {
                // اگر دسترسی خواندن نبود، از این بخش رد می‌شویم
            }
        }

        private void Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch toggle) return;
            if (toggle.DataContext is not StartupItem item) return;

            bool wantEnabled = toggle.IsOn;
            if (wantEnabled == item.Enabled) return; // تغییری رخ نداده (مثلاً هنگام ساخت لیست)

            try
            {
                SetEnabled(item, wantEnabled);
                item.Enabled = wantEnabled;
            }
            catch (Exception ex)
            {
                // برگرداندن کلید به حالت قبل
                toggle.IsOn = item.Enabled;
                ShowError($"{ex.Message} — برای تغییر آیتم‌های «All users» برنامه را با Run as administrator اجرا کن.");
            }
        }

        private static void SetEnabled(StartupItem item, bool enabled)
        {
            var hive = item.IsMachine ? Registry.LocalMachine : Registry.CurrentUser;
            using var key = hive.CreateSubKey(ApprovedKey, writable: true)
                ?? throw new InvalidOperationException("Cannot open StartupApproved key");

            var bytes = new byte[12];
            bytes[0] = (byte)(enabled ? 0x02 : 0x03);
            key.SetValue(item.Name, bytes, RegistryValueKind.Binary);
        }

        /// <summary>
        /// تخمین تأثیر روی زمان بوت از روی اندازه‌ی فایل اجرایی + فایل‌های کنارش (DLLها).
        /// اندازه‌گیری واقعی نیاز به trace بوت دارد؛ این تخمین همان حسی را می‌دهد که
        /// Task Manager با High/Medium/Low نشان می‌دهد.
        /// </summary>
        private static int EstimateImpact(string command)
        {
            try
            {
                string exe = ExtractExePath(command);
                if (exe.Length == 0 || !System.IO.File.Exists(exe)) return 0;

                long size = new System.IO.FileInfo(exe).Length;

                // DLLهای کنار فایل هم موقع استارت لود می‌شوند — تا سقف ۵۰ فایل می‌شماریم
                try
                {
                    var dir = System.IO.Path.GetDirectoryName(exe);
                    if (dir != null)
                    {
                        int count = 0;
                        foreach (var f in System.IO.Directory.EnumerateFiles(dir, "*.dll"))
                        {
                            size += new System.IO.FileInfo(f).Length / 4; // وزن کمتر از خود EXE
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

        /// <summary>مسیر EXE را از رشته‌ی فرمان رجیستری درمی‌آورد (با یا بدون کوتیشن و آرگومان)</summary>
        private static string ExtractExePath(string command)
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

        private void ShowError(string message)
        {
            ErrorBar.Message = message;
            ErrorBar.IsOpen = true;
        }
    }
}
