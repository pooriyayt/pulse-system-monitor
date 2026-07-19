using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TaskManagerPro.Monitoring;

namespace TaskManagerPro.Helpers
{
    /// <summary>
    /// مدیریت System Tray:
    /// - چند آیکون زنده‌ی همزمان (CPU / RAM / GPU / Disk / سرعت دانلود / سرعت آپلود)
    /// - دو استایل: بج رنگی با پس‌زمینه یا فقط عدد رنگی بدون پس‌زمینه
    /// - اندازه‌ی متن قابل تنظیم (75٪ تا 150٪)
    /// - هات‌کی سراسری Ctrl+Alt+T برای باز/مخفی کردن پنجره
    /// </summary>
    public class TrayManager : IDisposable
    {
        // ---- ثابت‌های ویندوز ----
        private const uint WM_APP_TRAY = 0x8001;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const uint WM_HOTKEY = 0x0312;

        private const uint NIM_ADD = 0x0;
        private const uint NIM_MODIFY = 0x1;
        private const uint NIM_DELETE = 0x2;
        private const uint NIF_MESSAGE = 0x1;
        private const uint NIF_ICON = 0x2;
        private const uint NIF_TIP = 0x4;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_T = 0x54;
        private const int HOTKEY_ID = 1;

        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint MF_STRING = 0x0;
        private const uint MF_SEPARATOR = 0x800;
        private const int MENU_SHOW = 1;
        private const int MENU_EXIT = 2;

        private static readonly string[] MetricNames = { "CPU", "RAM", "GPU", "Disk", "Download", "Upload" };

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private readonly WndProcDelegate _wndProc;

        private IntPtr _hwnd;
        private DispatcherQueueTimer? _timer;
        private readonly HashSet<uint> _icons = new();

        // تاریخچه‌ی کوتاه هر متریک برای استایل mini-گراف
        private readonly Dictionary<int, Queue<double>> _history = new();
        private const int HistoryLen = 16;
        private bool _hotkeyOn;
        private bool _updating;

        public TrayManager()
        {
            _wndProc = WndProcImpl;

            // یک پنجره‌ی مخفی فقط برای دریافت پیام‌های Tray و هات‌کی
            var wc = new WNDCLASS
            {
                lpszClassName = "TaskManagerProTrayWindow",
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = GetModuleHandleW(null),
            };
            RegisterClassW(ref wc);

            _hwnd = CreateWindowExW(0, wc.lpszClassName, "TrayMsgWindow", 0,
                0, 0, 0, 0, new IntPtr(-3) /* HWND_MESSAGE */, IntPtr.Zero, wc.hInstance, IntPtr.Zero);

            _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(AppSettings.RefreshIntervalMs, 1000));
            _timer.Tick += (s, e) => UpdateIcons();

            AppSettings.RefreshIntervalChanged += () =>
            {
                if (_timer != null)
                    _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(AppSettings.RefreshIntervalMs, 1000));
            };
        }

        /// <summary>بر اساس تنظیمات فعلی، آیکون‌ها و هات‌کی را فعال/غیرفعال می‌کند</summary>
        public void ApplySettings()
        {
            // هات‌کی سراسری Ctrl+Alt+T
            if (AppSettings.HotkeyEnabled && !_hotkeyOn)
            {
                try { _hotkeyOn = RegisterHotKey(_hwnd, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_T); } catch { }
            }
            else if (!AppSettings.HotkeyEnabled && _hotkeyOn)
            {
                try { UnregisterHotKey(_hwnd, HOTKEY_ID); } catch { }
                _hotkeyOn = false;
            }

            if (AppSettings.TrayEnabled)
            {
                _timer?.Start();
                UpdateIcons();
            }
            else
            {
                _timer?.Stop();
                RemoveAllIcons();
            }
        }

        // ---------- بروزرسانی آیکون‌ها ----------

        private void UpdateIcons()
        {
            if (!AppSettings.TrayEnabled || _hwnd == IntPtr.Zero || _updating) return;
            _updating = true;

            var dq = DispatcherQueue.GetForCurrentThread();
            System.Threading.Tasks.Task.Run(() =>
            {
                SystemSnapshot? snap = null;
                try { snap = SystemMonitor.Instance.Read(); } catch { }
                dq.TryEnqueue(() =>
                {
                    _updating = false;
                    if (snap != null && AppSettings.TrayEnabled) RenderIcons(snap);
                });
            });
        }

        private void RenderIcons(SystemSnapshot s)
        {
            var metrics = AppSettings.TrayMetricsList;
            if (metrics.Count == 0) metrics.Add(0);

            // آیکون‌هایی که دیگر در لیست انتخاب نیستند حذف می‌شوند
            foreach (var id in _icons.ToList())
                if (!metrics.Contains((int)id - 1))
                    RemoveIcon(id);

            foreach (var m in metrics)
            {
                // هر آیکون می‌تواند رنگ / استایل / اندازه‌ی مخصوص خودش را داشته باشد
                string hexOverride = AppSettings.GetTrayIconColor(m);
                var color = ColorFromHex(hexOverride.Length > 0 ? hexOverride : AppSettings.TrayColor);
                int styleOverride = AppSettings.GetTrayIconStyle(m);
                int style = styleOverride >= 0 ? styleOverride : AppSettings.TrayStyle;
                int scaleOverride = AppSettings.GetTrayIconScale(m);
                float scale = (scaleOverride > 0 ? scaleOverride : AppSettings.TrayTextScale) / 100f;

                double val = m switch
                {
                    1 => s.MemPercent,
                    2 => Math.Max(s.GpuPercent, 0),
                    3 => s.DiskPercent,
                    4 => s.NetRecvKBs,
                    5 => s.NetSentKBs,
                    _ => s.CpuTotal,
                };

                // تاریخچه‌ی کوتاه برای استایل mini-گراف
                if (!_history.TryGetValue(m, out var hist)) _history[m] = hist = new Queue<double>();
                hist.Enqueue(val);
                while (hist.Count > HistoryLen) hist.Dequeue();

                bool isNet = m >= 4;
                string text;
                string sub = "";
                if (isNet)
                {
                    double mbs = val / 1024.0;
                    text = mbs >= 10 ? mbs.ToString("F0") : mbs.ToString("F1");
                    sub = m == 4 ? "\u2193" : "\u2191"; // فلش دانلود / آپلود
                }
                else
                {
                    text = Math.Clamp(Math.Round(val), 0, 99).ToString("F0");
                }

                IntPtr hIcon = style == 2
                    ? CreateGraphIcon(hist.ToArray(), color, isNet)
                    : CreateTrayIcon(text, sub, color, style, scale);

                string tip = isNet
                    ? $"{MetricNames[m]}: {FormatSpeedTip(val)}"
                    : $"{MetricNames[m]}: {val:F0}%";
                tip += $" \u2022 CPU {s.CpuTotal:F0}% RAM {s.MemPercent:F0}%";
                if (tip.Length > 63) tip = tip.Substring(0, 63);

                ShowOrModifyIcon((uint)(m + 1), hIcon, tip);
                DestroyIcon(hIcon);
            }
        }

        private static string FormatSpeedTip(double kbs) =>
            kbs >= 1024 ? $"{kbs / 1024.0:F1} MB/s" : $"{kbs:F0} KB/s";

        /// <summary>ساخت آیکون 32×32 با متن زنده — دو استایل: بج رنگی یا فقط متن رنگی</summary>
        private static IntPtr CreateTrayIcon(string text, string sub, Color color, int style, float scale)
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            Color textColor = Color.White;
            if (style == 0)
            {
                // بج رنگی گردگوشه با متن سفید
                using var path = RoundedRect(new RectangleF(0, 0, 31, 31), 9);
                using var brush = new SolidBrush(color);
                g.FillPath(brush, path);
            }
            else
            {
                // بدون پس‌زمینه — فقط متن با رنگ انتخابی
                textColor = color;
            }

            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            using var textBrush = new SolidBrush(textColor);

            if (!string.IsNullOrEmpty(sub))
            {
                // حالت سرعت شبکه: فلش کوچک بالا + عدد MB/s پایین
                using var subFont = new Font("Segoe UI", 8.5f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
                using var mainFont = new Font("Segoe UI", 14f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
                g.DrawString(sub, subFont, textBrush, new RectangleF(0, 1, 32, 11), format);
                g.DrawString(text, mainFont, textBrush, new RectangleF(0, 10, 32, 22), format);
            }
            else
            {
                float size = (text.Length >= 3 ? 13f : 17f) * scale;
                using var font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
                g.DrawString(text, font, textBrush, new RectangleF(0, 0, 32, 32), format);
            }

            return bmp.GetHicon();
        }

        /// <summary>آیکون 32×32 با mini-گراف زنده به‌جای عدد (استایل 2)</summary>
        private static IntPtr CreateGraphIcon(double[] values, Color color, bool autoScale)
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // پس‌زمینه‌ی تیره‌ی نیمه‌شفاف تا گراف روی هر تسک‌باری دیده شود
            using (var path = RoundedRect(new RectangleF(0, 0, 31, 31), 7))
            using (var bg = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                g.FillPath(bg, path);

            if (values.Length >= 2)
            {
                double max = 100;
                if (autoScale)
                {
                    max = 1;
                    foreach (var v in values) if (v > max) max = v;
                    max *= 1.2;
                }

                var pts = new PointF[values.Length];
                float stepX = 30f / (values.Length - 1);
                for (int i = 0; i < values.Length; i++)
                {
                    float ratio = (float)Math.Min(Math.Max(values[i], 0) / max, 1.0);
                    pts[i] = new PointF(1 + i * stepX, 29 - ratio * 26);
                }

                // ناحیه‌ی پرشده‌ی محو زیر خط
                var fill = new PointF[values.Length + 2];
                pts.CopyTo(fill, 0);
                fill[values.Length] = new PointF(31, 31);
                fill[values.Length + 1] = new PointF(1, 31);
                using (var fillBrush = new SolidBrush(Color.FromArgb(70, color)))
                    g.FillPolygon(fillBrush, fill);

                using var pen = new Pen(color, 2f);
                g.DrawLines(pen, pts);
            }

            return bmp.GetHicon();
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color ColorFromHex(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                    return Color.FromArgb(255,
                        Convert.ToInt32(hex.Substring(0, 2), 16),
                        Convert.ToInt32(hex.Substring(2, 2), 16),
                        Convert.ToInt32(hex.Substring(4, 2), 16));
            }
            catch { }
            return Color.FromArgb(255, 97, 175, 254);
        }

        // ---------- مدیریت آیکون‌ها در Shell ----------

        private void ShowOrModifyIcon(uint id, IntPtr hIcon, string tip)
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = id,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_APP_TRAY,
                hIcon = hIcon,
                szTip = tip,
            };

            if (_icons.Contains(id))
            {
                Shell_NotifyIconW(NIM_MODIFY, ref data);
            }
            else
            {
                if (Shell_NotifyIconW(NIM_ADD, ref data))
                    _icons.Add(id);
            }
        }

        private void RemoveIcon(uint id)
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = id,
            };
            Shell_NotifyIconW(NIM_DELETE, ref data);
            _icons.Remove(id);
        }

        private void RemoveAllIcons()
        {
            foreach (var id in _icons.ToList())
                RemoveIcon(id);
        }

        // ---------- پیام‌های ویندوز ----------

        private IntPtr WndProcImpl(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_APP_TRAY)
            {
                int evt = (int)(lParam.ToInt64() & 0xFFFF);
                if (evt == WM_LBUTTONUP) ShowWindowNow();
                else if (evt == WM_RBUTTONUP) ShowMenu();
                return IntPtr.Zero;
            }

            if (msg == WM_HOTKEY && wParam.ToInt64() == HOTKEY_ID)
            {
                ToggleWindow();
                return IntPtr.Zero;
            }

            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        /// <summary>نمایش پنجره‌ی اصلی (از Tray یا هات‌کی)</summary>
        public static void ShowWindowNow()
        {
            var w = App.MainAppWindow;
            if (w == null) return;
            w.AppWindow.Show();
            if (w.AppWindow.Presenter is OverlappedPresenter p &&
                p.State == OverlappedPresenterState.Minimized)
                p.Restore();
            w.Activate();
        }

        /// <summary>باز/مخفی کردن پنجره با هات‌کی Ctrl+Alt+T</summary>
        public static void ToggleWindow()
        {
            var w = App.MainAppWindow;
            if (w == null) return;
            if (w.AppWindow.IsVisible) w.AppWindow.Hide();
            else ShowWindowNow();
        }

        private void ShowMenu()
        {
            IntPtr menu = CreatePopupMenu();
            AppendMenuW(menu, MF_STRING, (UIntPtr)MENU_SHOW, L10n.T("Show Pulse"));
            AppendMenuW(menu, MF_SEPARATOR, UIntPtr.Zero, null);
            AppendMenuW(menu, MF_STRING, (UIntPtr)MENU_EXIT, L10n.T("Exit"));

            GetCursorPos(out POINT pt);
            SetForegroundWindow(_hwnd);
            int cmd = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);
            DestroyMenu(menu);

            if (cmd == MENU_SHOW) ShowWindowNow();
            else if (cmd == MENU_EXIT) ExitApp();
        }

        private static void ExitApp()
        {
            App.IsExiting = true;
            App.MainAppWindow?.Close();
            Application.Current.Exit();
        }

        public void Dispose()
        {
            try { RemoveAllIcons(); } catch { }
            try { if (_hotkeyOn) UnregisterHotKey(_hwnd, HOTKEY_ID); } catch { }
            try { if (_hwnd != IntPtr.Zero) DestroyWindow(_hwnd); } catch { }
            _hwnd = IntPtr.Zero;
            _timer?.Stop();
            _timer = null;
        }

        // ---------- ساختارها و P/Invoke ----------

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASS
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szTip;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y,
            int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string? lpModuleName);
    }
}
