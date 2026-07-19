using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TaskManagerPro.Helpers;
using TaskManagerPro.Models;
using TaskManagerPro.Monitoring;

namespace TaskManagerPro.Views
{
    /// <summary>
    /// صفحه‌ی Processes با گروه‌بندی مثل Task Manager ویندوز:
    /// Apps / Background processes / Windows processes
    /// - خواندن سریع با Process.GetProcesses (بدون WMI کُند)
    /// - بروزرسانی درجا (بدون Clear) تا حالت باز/بسته و اسکرول حفظ شود
    /// - ستون Network (نیاز به Run as administrator)
    /// </summary>
    public sealed partial class ProcessesPage : Page
    {
        /// <summary>ریشه‌های درخت (گروه‌ها یا نتایج جستجو)</summary>
        public ObservableCollection<ProcessNode> Roots { get; } = new();

        // گروه‌های ثابت — یک بار ساخته می‌شوند و در رفرش‌ها عوض نمی‌شوند (فیکس باگ بسته شدن درخت)
        private readonly ProcessNode _appsGroup = new() { IsGroup = true, Pid = -1, BaseTitle = "Apps", Name = "Apps", FallbackGlyph = "\uE71D", IsExpanded = true };
        private readonly ProcessNode _bgGroup = new() { IsGroup = true, Pid = -2, BaseTitle = "Background processes", Name = "Background processes", FallbackGlyph = "\uE9F5", IsExpanded = true };
        private readonly ProcessNode _winGroup = new() { IsGroup = true, Pid = -3, BaseTitle = "Windows processes", Name = "Windows processes", FallbackGlyph = "\uE770", IsExpanded = false };

        // نگاشت PID ← گره — گره‌ها بازاستفاده می‌شوند تا UI فقط مقادیر را آپدیت کند
        private readonly Dictionary<int, ProcessNode> _nodes = new();

        private ProcessNode? _selected;
        private DispatcherQueueTimer? _timer;
        private bool _menuOpen;
        private bool _refreshing;
        private bool _firstLoadDone;

        // برای محاسبه‌ی درصد CPU و سرعت دیسک بین دو رفرش
        private static readonly Dictionary<int, (TimeSpan Cpu, DateTime At)> PrevCpu = new();
        private static readonly Dictionary<int, (ulong Bytes, DateTime At)> PrevIo = new();

        // PIDهایی که همین الان End task شدند — تا ۱۵ ثانیه مخفی می‌مانند
        // (ویندوز پردازه‌ی زامبی را تا آزاد شدن هندل‌ها در لیست نگه می‌دارد)
        private static readonly Dictionary<int, DateTime> RecentlyKilled = new();

        /// <summary>داده‌ی خام یک پردازه که در ترد پس‌زمینه خوانده می‌شود</summary>
        private sealed class ProcRow
        {
            public int Pid;
            public string Name = "";
            public string Path = "";
            public bool HasWindow;
            public double CpuPercent;
            public double MemMB;
            public double DiskMBs;
            public double NetKBs;
            public double GpuPercent;
            public bool IsEco;
        }

        // پردازه‌های سیستمی ویندوز (گروه Windows processes)
        private static readonly HashSet<string> WinNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "explorer", "svchost", "csrss", "wininit", "winlogon", "services", "lsass", "smss",
            "dwm", "fontdrvhost", "conhost", "System", "Idle", "Registry", "Memory Compression",
            "sihost", "taskhostw", "ctfmon", "audiodg", "spoolsv", "SecurityHealthService",
            "dllhost", "WmiPrvSE", "RuntimeBroker", "SearchHost", "ShellExperienceHost",
            "StartMenuExperienceHost", "TextInputHost", "LockApp",
        };

        // پردازه‌هایی که کاربر فریز کرده (برای نمایش گزینه‌ی Resume)
        private static readonly HashSet<int> Suspended = new();

        // پردازه‌هایی که کاربر از همین برنامه Efficiency mode کرده (بازخورد فوری UI)
        private static readonly HashSet<int> EcoSet = new();

        public ProcessesPage()
        {
            InitializeComponent();
            SortCombo.SelectedIndex = 0;
            FilterCombo.SelectedIndex = 0;
            ApplyL10n();
            AppSettings.LanguageChanged += ApplyL10n;

            ProcessMenu.Opened += (s, e) => _menuOpen = true;
            ProcessMenu.Closed += (s, e) => _menuOpen = false;

            Loaded += async (s, e) =>
            {
                StartTimer();
                await RefreshAsync();
            };
            Unloaded += (s, e) => _timer?.Stop();
        }

        private void StartTimer()
        {
            if (_timer != null)
            {
                _timer.Start();
                return;
            }

            _timer = DispatcherQueue.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(AppSettings.RefreshIntervalMs, 1000));
            _timer.Tick += async (s, e) =>
            {
                // وقتی منوی کلیک راست باز است رفرش نمی‌کنیم تا منو نپرد
                if (!_menuOpen) await RefreshAsync();
            };
            AppSettings.RefreshIntervalChanged += () =>
            {
                if (_timer != null)
                    _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(AppSettings.RefreshIntervalMs, 1000));
            };
            _timer.Start();
        }

        // ---------- خواندن داده‌ها (ترد پس‌زمینه) ----------

        private async Task RefreshAsync()
        {
            if (_refreshing) return;
            _refreshing = true;
            try
            {
                var rows = await Task.Run(() =>
                {
                    try { SystemMonitor.Instance.Read(); } catch { }
                    try { NetworkMonitor.Instance.Snapshot(); } catch { }
                    return ReadProcesses();
                });

                ApplyData(rows);

                if (!_firstLoadDone)
                {
                    _firstLoadDone = true;
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    // اگر دسترسی ادمین نیست، هشدار ستون Network را نشان بده
                    AdminBar.IsOpen = !NetworkMonitor.Instance.IsAvailable;
                }
            }
            catch (Exception ex)
            {
                ShowError("Failed to refresh processes: " + ex.Message);
            }
            finally
            {
                _refreshing = false;
            }
        }

        private static List<ProcRow> ReadProcesses()
        {
            var now = DateTime.UtcNow;
            var gpuByPid = SystemMonitor.Instance.GpuByPid;
            var netRates = NetworkMonitor.Instance.RatesKBs;
            bool netOk = NetworkMonitor.Instance.IsAvailable;

            var rows = new List<ProcRow>(256);
            var alive = new HashSet<int>();

            // پاکسازی PIDهای منقضی از لیست تازه‌کشته‌شده‌ها
            lock (RecentlyKilled)
            {
                foreach (var k in RecentlyKilled.Where(kv => (now - kv.Value).TotalSeconds > 15).Select(kv => kv.Key).ToList())
                    RecentlyKilled.Remove(k);
            }

            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    try
                    {
                        int pid = p.Id;

                        // پردازه‌ی زامبی (Exit شده ولی هنوز در لیست ویندوز) را نشان نده
                        try { if (p.HasExited) continue; } catch { }

                        // پردازه‌ای که همین الان End task شده را نشان نده
                        lock (RecentlyKilled)
                            if (RecentlyKilled.ContainsKey(pid)) continue;

                        alive.Add(pid);

                        var row = new ProcRow
                        {
                            Pid = pid,
                            Name = p.ProcessName,
                            MemMB = p.WorkingSet64 / (1024.0 * 1024.0),
                        };

                        try { row.HasWindow = p.MainWindowHandle != IntPtr.Zero; } catch { }

                        row.Path = GetProcessPath(pid);

                        // درصد CPU = اختلاف زمان پردازنده بین دو رفرش
                        try
                        {
                            var cpu = p.TotalProcessorTime;
                            if (PrevCpu.TryGetValue(pid, out var prev))
                            {
                                double dt = (now - prev.At).TotalSeconds;
                                if (dt > 0.1)
                                    row.CpuPercent = Math.Clamp(
                                        (cpu - prev.Cpu).TotalSeconds / dt / Environment.ProcessorCount * 100.0, 0, 100);
                            }
                            PrevCpu[pid] = (cpu, now);
                        }
                        catch { }

                        // سرعت دیسک = اختلاف بایت‌های I/O بین دو رفرش
                        try
                        {
                            ulong io = ReadIoBytes(pid);
                            if (io > 0)
                            {
                                if (PrevIo.TryGetValue(pid, out var prevIo))
                                {
                                    double dt = (now - prevIo.At).TotalSeconds;
                                    if (dt > 0.1 && io >= prevIo.Bytes)
                                        row.DiskMBs = (io - prevIo.Bytes) / dt / (1024.0 * 1024.0);
                                }
                                PrevIo[pid] = (io, now);
                            }
                        }
                        catch { }

                        row.GpuPercent = gpuByPid.TryGetValue(pid, out var g) ? g : 0;
                        row.NetKBs = netOk ? (netRates.TryGetValue(pid, out var n) ? n : 0) : -1;

                        // برگ سبز Efficiency mode (EcoQoS) مثل Task Manager ویندوز
                        row.IsEco = IsEcoProcess(pid);
                        lock (EcoSet) if (EcoSet.Contains(pid)) row.IsEco = true;

                        rows.Add(row);
                    }
                    catch { }
                }
            }

            // پاکسازی PIDهای مرده از کش‌ها
            foreach (var dead in PrevCpu.Keys.Where(k => !alive.Contains(k)).ToList()) PrevCpu.Remove(dead);
            foreach (var dead in PrevIo.Keys.Where(k => !alive.Contains(k)).ToList()) PrevIo.Remove(dead);

            // آیکون‌ها همین‌جا در ترد پس‌زمینه از فایل EXE استخراج می‌شوند (UI کُند نمی‌شود)
            foreach (var r in rows)
                if (r.Path.Length > 0)
                    IconCache.Preload(r.Path);

            return rows;
        }

        private static int Classify(ProcRow r)
        {
            if (WinNames.Contains(r.Name)) return 2; // Windows processes
            if (r.HasWindow) return 0;               // Apps (پنجره‌دار)
            return 1;                                // Background processes
        }

        // ---------- اعمال داده‌ها روی UI ----------

        private void ApplyData(List<ProcRow> rows)
        {
            // وقتی منوی کلیک راست باز است هیچ تغییری روی لیست اعمال نکن —
            // رفرشی که «قبل از» باز شدن منو شروع شده بود لیست را جابه‌جا می‌کرد و منو می‌پرید
            // (و Suspend / Set priority عملاً غیرقابل کلیک می‌شدند)
            if (_menuOpen) return;

            var alive = new HashSet<int>();

            foreach (var r in rows)
            {
                // ردیفی که بین شروعِ خواندن و الان End task شده دوباره اضافه نشود (فیکس race)
                lock (RecentlyKilled)
                    if (RecentlyKilled.ContainsKey(r.Pid)) continue;

                alive.Add(r.Pid);

                if (!_nodes.TryGetValue(r.Pid, out var n))
                {
                    n = new ProcessNode { Pid = r.Pid };
                    _nodes[r.Pid] = n;
                }

                n.Name = r.Name;
                n.Path = r.Path;
                n.Category = Classify(r);
                n.Cpu = r.CpuPercent;
                n.MemoryMB = r.MemMB;
                n.DiskMBs = r.DiskMBs;
                n.NetKBs = r.NetKBs;
                n.GpuPercent = r.GpuPercent;
                n.IsEco = r.IsEco;

                if (n.Icon == null && r.Path.Length > 0)
                    n.Icon = IconCache.Get(r.Path);
            }

            // حذف پردازه‌هایی که دیگر زنده نیستند
            foreach (var dead in _nodes.Keys.Where(k => !alive.Contains(k)).ToList())
            {
                if (_selected != null && _selected.Pid == dead)
                {
                    _selected = null;
                    UpdateEndTaskBtn();
                }
                _nodes.Remove(dead);
            }

            RebuildView();
            StatusText.Text = string.Format(L10n.T("{0} processes  \u2022  updated {1}"), _nodes.Count, DateTime.Now.ToString("HH:mm:ss"));
        }

        /// <summary>فیلتر پیشرفته: همه / فقط برنامه‌ها / CPU بیش از ۱٪ / حافظه بیش از 100MB / شبکه‌ی فعال</summary>
        private bool PassFilter(ProcessNode n) => FilterCombo.SelectedIndex switch
        {
            1 => n.Category == 0,
            2 => n.Cpu > 1.0,
            3 => n.MemoryMB > 100.0,
            4 => n.NetKBs > 0.5,
            _ => true,
        };

        private void RebuildView()
        {
            string q = SearchBox.Text?.Trim() ?? "";
            int sort = SortCombo.SelectedIndex;

            if (q.Length > 0)
            {
                // جستجو: لیست تخت از همه‌ی پردازه‌های منطبق (همه جا پیدا می‌شود، نه فقط ریشه‌ها)
                var matches = _nodes.Values
                    .Where(n => (n.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                 n.Pid.ToString().Contains(q)) && PassFilter(n))
                    .ToList();
                SortList(matches, sort);
                SyncCollection(Roots, matches);
                return;
            }

            FillGroup(_appsGroup, 0, sort);
            FillGroup(_bgGroup, 1, sort);
            FillGroup(_winGroup, 2, sort);

            if (Roots.Count != 3 || Roots[0] != _appsGroup)
            {
                Roots.Clear();
                Roots.Add(_appsGroup);
                Roots.Add(_bgGroup);
                Roots.Add(_winGroup);
            }
        }

        private void FillGroup(ProcessNode group, int category, int sort)
        {
            var list = _nodes.Values.Where(n => n.Category == category && PassFilter(n)).ToList();
            SortList(list, sort);
            group.Name = $"{L10n.T(group.BaseTitle)} ({list.Count})";
            SyncCollection(group.Children, list);
        }

        /// <summary>
        /// به‌روزرسانی حداقلی کالکشن بدون Clear —
        /// حالت باز/بسته‌ی درخت و اسکرول حفظ می‌شود (فیکس باگ)
        /// </summary>
        private static void SyncCollection(ObservableCollection<ProcessNode> target, List<ProcessNode> desired)
        {
            var desiredSet = new HashSet<ProcessNode>(desired);

            for (int i = target.Count - 1; i >= 0; i--)
                if (!desiredSet.Contains(target[i]))
                    target.RemoveAt(i);

            for (int i = 0; i < desired.Count; i++)
            {
                int cur = target.IndexOf(desired[i]);
                if (cur == i) continue;
                // Remove + Insert به جای Move — TreeView در WinUI رویداد Move را روی
                // کالکشن‌های تو در تو درست هندل نمی‌کند و UI از دیتا جدا می‌افتاد
                // (باگ: ردیف پردازه‌ی End task شده در لیست می‌ماند)
                if (cur >= 0) target.RemoveAt(cur);
                target.Insert(i, desired[i]);
            }
        }

        private static void SortList(List<ProcessNode> list, int sort)
        {
            switch (sort)
            {
                case 1: list.Sort((a, b) => b.MemoryMB.CompareTo(a.MemoryMB)); break;
                case 2: list.Sort((a, b) => b.DiskMBs.CompareTo(a.DiskMBs)); break;
                case 3: list.Sort((a, b) => b.NetKBs.CompareTo(a.NetKBs)); break;
                case 4: list.Sort((a, b) => b.GpuPercent.CompareTo(a.GpuPercent)); break;
                case 5: list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)); break;
                case 6: list.Sort((a, b) => a.Pid.CompareTo(b.Pid)); break;
                default: list.Sort((a, b) => b.Cpu.CompareTo(a.Cpu)); break;
            }
        }

        // ---------- انتخاب و منو ----------

        private void Tree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            _selected = args.InvokedItem as ProcessNode;
            UpdateEndTaskBtn();
        }

        private void Row_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ProcessNode n && !n.IsGroup)
            {
                _selected = n;
                UpdateEndTaskBtn();

                // اگر پردازه مسیر فایل ندارد، Open file location کلا مخفی می‌شود
                bool hasPath = !string.IsNullOrEmpty(n.Path);
                MenuOpenLocation.Visibility = hasPath ? Visibility.Visible : Visibility.Collapsed;
                MenuLocationSep.Visibility = MenuOpenLocation.Visibility;

                // Suspend یا Resume — بسته به وضعیت فعلی پردازه
                bool suspended = Suspended.Contains(n.Pid);
                MenuSuspend.Visibility = suspended ? Visibility.Collapsed : Visibility.Visible;
                MenuResume.Visibility = suspended ? Visibility.Visible : Visibility.Collapsed;

                // Efficiency mode — روشن/خاموش بسته به وضعیت فعلی
                MenuEco.Text = n.IsEco ? L10n.T("Disable efficiency mode") : L10n.T("Efficiency mode");

                ProcessMenu.ShowAt(fe, e.GetPosition(fe));
                e.Handled = true;
            }
        }

        private void UpdateEndTaskBtn()
        {
            bool ok = _selected != null && !_selected.IsGroup;
            EndTaskBtn.IsEnabled = ok;

            bool isExplorer = ok && string.Equals(_selected!.Name, "explorer", StringComparison.OrdinalIgnoreCase);
            EndTaskLabel.Text = isExplorer ? L10n.T("Restart") : L10n.T("End task");
            MenuEndTask.Text = isExplorer ? L10n.T("Restart") : L10n.T("End task");
        }

        // ---------- End task / Restart ----------

        private void EndTask_Click(object sender, RoutedEventArgs e) => EndSelected();

        private void DeleteAccel(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            EndSelected();
            args.Handled = true;
        }

        private async void EndSelected()
        {
            var n = _selected;
            if (n == null || n.IsGroup) return;

            try
            {
                if (string.Equals(n.Name, "explorer", StringComparison.OrdinalIgnoreCase))
                {
                    // ری‌استارت اکسپلورر مثل Task Manager ویندوز
                    await Task.Run(() =>
                    {
                        foreach (var p in Process.GetProcessesByName("explorer"))
                            using (p) { try { p.Kill(); } catch { } }
                        try { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true }); } catch { }
                    });
                }
                else
                {
                    using var p = Process.GetProcessById(n.Pid);
                    p.Kill(true);
                }

                // صدای تأیید (قابل خاموش کردن در تنظیمات)
                if (AppSettings.EndTaskSound)
                    SoundHelper.PlayEndTask();

                MarkKilled(n.Pid);
                RemoveNodeNow(n.Pid);
                await RefreshAsync();
            }
            catch (ArgumentException)
            {
                // پردازه از قبل بسته شده — فقط ردیفش را بردار، خطا لازم نیست
                MarkKilled(n.Pid);
                RemoveNodeNow(n.Pid);
            }
            catch (InvalidOperationException)
            {
                // پردازه حین Kill خودش خارج شد — همان رفتار
                MarkKilled(n.Pid);
                RemoveNodeNow(n.Pid);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // دسترسی رد شد (پردازه‌ی محافظت‌شده/ادمین) — تلاش دوباره با taskkill
                bool ok = await Task.Run(() => TryTaskKill(n.Pid));
                if (ok)
                {
                    if (AppSettings.EndTaskSound) SoundHelper.PlayEndTask();
                    MarkKilled(n.Pid);
                    RemoveNodeNow(n.Pid);
                    await RefreshAsync();
                }
                else
                {
                    ShowError(string.Format(L10n.T("Could not end \"{0}\" — access denied. Try running the app as administrator."), n.Name));
                }
            }
            catch (Exception ex)
            {
                ShowError(string.Format(L10n.T("Could not end \"{0}\": {1}"), n.Name, ex.Message));
            }
        }

        /// <summary>Kill جایگزین با taskkill — برای پردازه‌هایی که Kill مستقیم رویشان جواب نمی‌دهد</summary>
        private static bool TryTaskKill(int pid)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("taskkill", $"/PID {pid} /T /F")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                if (p == null) return false;
                p.WaitForExit(4000);
                return p.HasExited && p.ExitCode == 0;
            }
            catch { return false; }
        }

        /// <summary>ثبت PID کشته‌شده تا در رفرش‌های بعدی (پردازه‌ی زامبی) دوباره ظاهر نشود</summary>
        private static void MarkKilled(int pid)
        {
            lock (RecentlyKilled) RecentlyKilled[pid] = DateTime.UtcNow;
        }

        /// <summary>
        /// حذف فوری ردیف پردازه از UI — بدون صبر برای رفرش بعدی،
        /// تا پردازه‌ی مرده در لیست نماند و دوباره قابل کلیک نباشد.
        /// </summary>
        private void RemoveNodeNow(int pid)
        {
            if (_nodes.Remove(pid))
            {
                if (_selected != null && _selected.Pid == pid)
                {
                    _selected = null;
                    UpdateEndTaskBtn();
                }
                RebuildView();
            }
        }

        // ---------- سایر آیتم‌های منو ----------

        private void Priority_Click(object sender, RoutedEventArgs e)
        {
            var n = _selected;
            if (n == null || n.IsGroup) return;
            try
            {
                using var p = Process.GetProcessById(n.Pid);
                p.PriorityClass = (sender as FrameworkElement)?.Tag switch
                {
                    "realtime" => ProcessPriorityClass.RealTime,
                    "high" => ProcessPriorityClass.High,
                    "abovenormal" => ProcessPriorityClass.AboveNormal,
                    "belownormal" => ProcessPriorityClass.BelowNormal,
                    "low" => ProcessPriorityClass.Idle,
                    _ => ProcessPriorityClass.Normal,
                };
            }
            catch (Exception ex)
            {
                ShowError("Could not change priority: " + ex.Message);
            }
        }

        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            var n = _selected;
            if (n == null || string.IsNullOrEmpty(n.Path)) return;
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{n.Path}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        /// <summary>\u062c\u0632\u0626\u06cc\u0627\u062a \u06a9\u0627\u0645\u0644 \u067e\u0631\u062f\u0627\u0632\u0647: thread \u0647\u0627\u060c handle \u0647\u0627\u060c \u0645\u0633\u06cc\u0631\u060c command line \u0648 ...</summary>
        private async void Properties_Click(object sender, RoutedEventArgs e)
        {
            var n = _selected;
            if (n == null || n.IsGroup) return;

            string details = await Task.Run(() => BuildDetails(n));

            var text = new TextBlock
            {
                Text = details,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                FontSize = 13,
            };

            var dlg = new ContentDialog
            {
                Title = $"{n.Name}  (PID {n.Pid})",
                PrimaryButtonText = L10n.T("Copy"),
                CloseButtonText = L10n.T("Close"),
                XamlRoot = XamlRoot,
                Content = new ScrollViewer { Content = text, MaxHeight = 420 },
            };
            dlg.PrimaryButtonClick += (s, a) => CopyToClipboard(details);
            try { await dlg.ShowAsync(); } catch { }
        }

        /// <summary>\u062c\u0645\u0639\u200c\u0622\u0648\u0631\u06cc \u062c\u0632\u0626\u06cc\u0627\u062a \u067e\u0631\u062f\u0627\u0632\u0647 \u062f\u0631 \u062a\u0631\u062f \u067e\u0633\u200c\u0632\u0645\u06cc\u0646\u0647</summary>
        private static string BuildDetails(ProcessNode n)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Name:          {n.Name}");
            sb.AppendLine($"PID:           {n.Pid}");
            sb.AppendLine($"Path:          {(string.IsNullOrEmpty(n.Path) ? "\u2014" : n.Path)}");

            try
            {
                using var p = Process.GetProcessById(n.Pid);
                try { sb.AppendLine($"Threads:       {p.Threads.Count}"); } catch { }
                try { sb.AppendLine($"Handles:       {p.HandleCount}"); } catch { }
                try { sb.AppendLine($"Started:       {p.StartTime:yyyy-MM-dd HH:mm:ss}"); } catch { }
                try { sb.AppendLine($"Priority:      {p.PriorityClass}"); } catch { }
                try { sb.AppendLine($"Total CPU:     {p.TotalProcessorTime:hh\\:mm\\:ss}"); } catch { }
            }
            catch { }

            sb.AppendLine($"CPU:           {n.CpuText}");
            sb.AppendLine($"Memory:        {n.MemText}");
            sb.AppendLine($"Disk:          {n.DiskText}");
            sb.AppendLine($"Network:       {n.NetText}");
            sb.AppendLine($"GPU:           {n.GpuText}");

            // Command line \u0627\u0632 WMI (\u0645\u0645\u06a9\u0646 \u0627\u0633\u062a \u0628\u0631\u0627\u06cc \u067e\u0631\u062f\u0627\u0632\u0647\u200c\u0647\u0627\u06cc \u0633\u06cc\u0633\u062a\u0645\u06cc \u062e\u0627\u0644\u06cc \u0628\u0627\u0634\u062f)
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {n.Pid}");
                foreach (System.Management.ManagementObject o in searcher.Get())
                {
                    var cmd = o["CommandLine"]?.ToString();
                    if (!string.IsNullOrEmpty(cmd))
                        sb.AppendLine($"Command line:  {cmd}");
                    break;
                }
            }
            catch { }

            return sb.ToString().TrimEnd();
        }

        private static void CopyToClipboard(string text)
        {
            try
            {
                var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
                pkg.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
            }
            catch { }
        }

        /// <summary>\u06a9\u067e\u06cc \u062c\u0632\u0626\u06cc\u0627\u062a \u067e\u0631\u062f\u0627\u0632\u0647\u200c\u06cc \u0627\u0646\u062a\u062e\u0627\u0628\u200c\u0634\u062f\u0647 \u062f\u0631 \u06a9\u0644\u06cc\u067e\u200c\u0628\u0648\u0631\u062f</summary>
        private async void CopyDetails_Click(object sender, RoutedEventArgs e)
        {
            var n = _selected;
            if (n == null || n.IsGroup) return;
            string details = await Task.Run(() => BuildDetails(n));
            CopyToClipboard(details);
        }

        /// <summary>Export \u06a9\u0644 \u0644\u06cc\u0633\u062a \u067e\u0631\u062f\u0627\u0632\u0647\u200c\u0647\u0627 \u0628\u0647 CSV</summary>
        private async void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileSavePicker
                {
                    SuggestedFileName = $"processes-{DateTime.Now:yyyyMMdd-HHmmss}",
                };
                picker.FileTypeChoices.Add("CSV file", new List<string> { ".csv" });

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSaveFileAsync();
                if (file == null) return;

                var sb = new StringBuilder();
                sb.AppendLine("Name,PID,CPU %,Memory MB,Disk MB/s,Network KB/s,GPU %,Path");
                foreach (var n in _nodes.Values.OrderByDescending(x => x.Cpu))
                {
                    sb.AppendLine(string.Join(",",
                        Csv(n.Name), n.Pid,
                        n.Cpu.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                        n.MemoryMB.ToString("F0", System.Globalization.CultureInfo.InvariantCulture),
                        n.DiskMBs.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                        (n.NetKBs < 0 ? 0 : n.NetKBs).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                        n.GpuPercent.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                        Csv(n.Path ?? "")));
                }

                await Windows.Storage.FileIO.WriteTextAsync(file, sb.ToString());
            }
            catch (Exception ex)
            {
                ShowError("Export failed: " + ex.Message);
            }
        }

        private static string Csv(string s) =>
            s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

        // ---------- Suspend / Resume / Efficiency mode ----------

        private void Suspend_Click(object sender, RoutedEventArgs e)
        {
            var n = _selected;
            if (n == null || n.IsGroup) return;
            IntPtr h = OpenProcess(0x0800 /* PROCESS_SUSPEND_RESUME */, false, n.Pid);
            if (h == IntPtr.Zero) { ShowError($"Could not open \"{n.Name}\" (access denied?)"); return; }
            try
            {
                if (NtSuspendProcess(h) == 0) Suspended.Add(n.Pid);
                else ShowError($"Could not suspend \"{n.Name}\"");
            }
            finally { CloseHandle(h); }
        }

        private void Resume_Click(object sender, RoutedEventArgs e)
        {
            var n = _selected;
            if (n == null || n.IsGroup) return;
            IntPtr h = OpenProcess(0x0800, false, n.Pid);
            if (h == IntPtr.Zero) { ShowError($"Could not open \"{n.Name}\""); return; }
            try
            {
                if (NtResumeProcess(h) == 0) Suspended.Remove(n.Pid);
                else ShowError($"Could not resume \"{n.Name}\"");
            }
            finally { CloseHandle(h); }
        }

        /// <summary>\u062d\u0627\u0644\u062a \u0628\u0647\u0631\u0647\u200c\u0648\u0631\u06cc: \u0627\u0648\u0644\u0648\u06cc\u062a Idle + EcoQoS (\u0645\u062b\u0644 \u0628\u0631\u06af \u0633\u0628\u0632 Task Manager \u0648\u06cc\u0646\u062f\u0648\u0632)</summary>
        private void Efficiency_Click(object sender, RoutedEventArgs e)
        {
            var n = _selected;
            if (n == null || n.IsGroup) return;
            bool enable = !n.IsEco; // تاگل: اگر روشن است خاموش کن
            try
            {
                using var p = Process.GetProcessById(n.Pid);
                p.PriorityClass = enable ? ProcessPriorityClass.Idle : ProcessPriorityClass.Normal;

                var state = new PROCESS_POWER_THROTTLING_STATE
                {
                    Version = 1,
                    ControlMask = 1, // PROCESS_POWER_THROTTLING_EXECUTION_SPEED
                    StateMask = enable ? 1u : 0u, // 1 = EcoQoS \u0631\u0648\u0634\u0646\u060c 0 = \u062e\u0627\u0645\u0648\u0634
                };
                SetProcessInformation(p.Handle, 4 /* ProcessPowerThrottling */,
                    ref state, Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());

                // بازخورد فوری: برگ سبز همین الان نمایش/مخفی شود
                lock (EcoSet)
                {
                    if (enable) EcoSet.Add(n.Pid);
                    else EcoSet.Remove(n.Pid);
                }
                n.IsEco = enable;
            }
            catch (Exception ex)
            {
                ShowError((enable ? "Could not enable efficiency mode: " : "Could not disable efficiency mode: ") + ex.Message);
            }
        }

        /// <summary>\u062a\u0631\u062c\u0645\u0647\u200c\u06cc \u0645\u062a\u0646\u200c\u0647\u0627\u06cc \u062b\u0627\u0628\u062a \u0635\u0641\u062d\u0647</summary>
        private void ApplyL10n()
        {
            SearchBox.PlaceholderText = L10n.T("Search processes  (Ctrl+F)");
            MenuSuspend.Text = L10n.T("Suspend");
            MenuResume.Text = L10n.T("Resume");
            MenuEco.Text = L10n.T("Efficiency mode");
            MenuCopy.Text = L10n.T("Copy details");
            MenuOpenLocation.Text = L10n.T("Open file location");

            // Column headers
            ColName.Text = L10n.T("Name");
            ColPid.Text = "PID";
            ColCpu.Text = "CPU";
            ColMemory.Text = L10n.T("Memory");
            ColDisk.Text = L10n.T("Disk");
            ColNetwork.Text = L10n.T("Network");
            ColGpu.Text = "GPU";

            // Toolbar buttons
            RefreshBtnLabel.Text = L10n.T("Refresh");
            ExportLabel.Text = L10n.T("Export CSV");
            RunTaskLabel.Text = L10n.T("Run new task");
            RunFlyoutTitle.Text = L10n.T("Open");
            RunInput.PlaceholderText = L10n.T("e.g. notepad, cmd, mspaint");
            RunBtn.Content = L10n.T("Run");
            EndTaskLabel.Text = L10n.T("End task");
            LoadingText.Text = L10n.T("Loading processes...");

            // Admin bar
            AdminBar.Title = L10n.T("Run as administrator");

            // Sort ComboBox items
            ((ComboBoxItem)SortCombo.Items[0]).Content = L10n.T("Sort: CPU");
            ((ComboBoxItem)SortCombo.Items[1]).Content = L10n.T("Sort: Memory");
            ((ComboBoxItem)SortCombo.Items[2]).Content = L10n.T("Sort: Disk");
            ((ComboBoxItem)SortCombo.Items[3]).Content = L10n.T("Sort: Network");
            ((ComboBoxItem)SortCombo.Items[4]).Content = L10n.T("Sort: GPU");
            ((ComboBoxItem)SortCombo.Items[5]).Content = L10n.T("Sort: Name");
            ((ComboBoxItem)SortCombo.Items[6]).Content = L10n.T("Sort: PID");

            // Filter ComboBox items
            ((ComboBoxItem)FilterCombo.Items[0]).Content = L10n.T("Filter: All");
            ((ComboBoxItem)FilterCombo.Items[1]).Content = L10n.T("Filter: Apps only");
            ((ComboBoxItem)FilterCombo.Items[2]).Content = L10n.T("Filter: CPU > 1%");
            ((ComboBoxItem)FilterCombo.Items[3]).Content = L10n.T("Filter: Memory > 100 MB");
            ((ComboBoxItem)FilterCombo.Items[4]).Content = L10n.T("Filter: Active network");
            RebuildView();
        }

        // ---------- نوار ابزار ----------

        private void RunTask_Click(object sender, RoutedEventArgs e)
        {
            string cmd = RunInput.Text?.Trim() ?? "";
            if (cmd.Length == 0) return;
            try
            {
                Process.Start(new ProcessStartInfo(cmd) { UseShellExecute = true });
                RunFlyout.Hide();
                RunInput.Text = "";
            }
            catch (Exception ex)
            {
                ShowError("Could not start: " + ex.Message);
            }
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_firstLoadDone) RebuildView();
        }

        private void Sort_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_firstLoadDone) RebuildView();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private void FindAccel(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            SearchBox.Focus(FocusState.Programmatic);
            args.Handled = true;
        }

        private async void RefreshAccel(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            await RefreshAsync();
            args.Handled = true;
        }

        private void ShowError(string msg)
        {
            ErrorBar.Message = msg;
            ErrorBar.IsOpen = true;
        }

        // ---------- P/Invoke ----------

        private static string GetProcessPath(int pid)
        {
            IntPtr h = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
            if (h == IntPtr.Zero) return "";
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (QueryFullProcessImageName(h, 0, sb, ref size))
                    return sb.ToString();
            }
            catch { }
            finally { CloseHandle(h); }
            return "";
        }

        private static ulong ReadIoBytes(int pid)
        {
            IntPtr h = OpenProcess(0x1000, false, pid);
            if (h == IntPtr.Zero) return 0;
            try
            {
                if (GetProcessIoCounters(h, out IO_COUNTERS io))
                    return io.ReadTransferCount + io.WriteTransferCount;
            }
            catch { }
            finally { CloseHandle(h); }
            return 0;
        }

        /// <summary>آیا EcoQoS (Efficiency mode) روی این پردازه فعال است؟</summary>
        private static bool IsEcoProcess(int pid)
        {
            IntPtr h = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
            if (h == IntPtr.Zero) return false;
            try
            {
                var state = new PROCESS_POWER_THROTTLING_STATE { Version = 1 };
                if (GetProcessInformation(h, 4 /* ProcessPowerThrottling */,
                        ref state, Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>()))
                    return (state.ControlMask & 1) != 0 && (state.StateMask & 1) != 0;
            }
            catch { }
            finally { CloseHandle(h); }
            return false;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int access, bool inherit, int pid);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder exeName, ref int size);

        [DllImport("kernel32.dll")]
        private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS counters);

        [DllImport("ntdll.dll")]
        private static extern int NtSuspendProcess(IntPtr hProcess);

        [DllImport("ntdll.dll")]
        private static extern int NtResumeProcess(IntPtr hProcess);

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessInformation(IntPtr hProcess, int infoClass,
            ref PROCESS_POWER_THROTTLING_STATE info, int size);

        [DllImport("kernel32.dll")]
        private static extern bool GetProcessInformation(IntPtr hProcess, int infoClass,
            ref PROCESS_POWER_THROTTLING_STATE info, int size);

        [DllImport("user32.dll")]
        private static extern bool MessageBeep(uint type);
    }
}
