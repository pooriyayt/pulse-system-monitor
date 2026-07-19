using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;

namespace TaskManagerPro.Helpers
{
    /// <summary>تم‌های برنامه</summary>
    public enum AppTheme
    {
        Dark = 0,
        Light = 1,
        LiquidGlass = 2,
        Midnight = 3,
        Aurora = 4,
        Oled = 5,
        Paper = 6,
    }

    /// <summary>
    /// تنظیمات سراسری برنامه که در LocalSettings ویندوز ذخیره می‌شوند
    /// و با بستن/باز کردن برنامه حفظ می‌مانند.
    /// </summary>
    public static class AppSettings
    {
        // ---------- رویدادها ----------

        /// <summary>وقتی سرعت رفرش عوض شد</summary>
        public static event Action? RefreshIntervalChanged;

        /// <summary>وقتی تم، رنگ Accent یا حالت پر کردن گراف عوض شد</summary>
        public static event Action? AppearanceChanged;

        /// <summary>وقتی تنظیمات Tray یا هات‌کی عوض شد</summary>
        public static event Action? TraySettingsChanged;

        /// <summary>وقتی ویجت دسکتاپ روشن/خاموش شد</summary>
        public static event Action? WidgetChanged;

        /// <summary>وقتی زبان برنامه عوض شد</summary>
        public static event Action? LanguageChanged;

        // ---------- مقادیر ----------

        private static int _refreshIntervalMs = 1000;
        /// <summary>فاصله‌ی بروزرسانی به میلی‌ثانیه (بین 500 تا 2000)</summary>
        public static int RefreshIntervalMs
        {
            get => _refreshIntervalMs;
            set
            {
                value = Math.Clamp(value, 500, 2000);
                if (_refreshIntervalMs == value) return;
                _refreshIntervalMs = value;
                Save();
                RefreshIntervalChanged?.Invoke();
            }
        }

        private static AppTheme _theme = AppTheme.Dark;
        public static AppTheme Theme
        {
            get => _theme;
            set
            {
                if (_theme == value) return;
                _theme = value;
                Save();
                AppearanceChanged?.Invoke();
            }
        }

        private static string _accentColor = "#61AFFE";
        /// <summary>رنگ Accent کل برنامه + گراف‌ها (کد هگز)</summary>
        public static string AccentColor
        {
            get => _accentColor;
            set
            {
                if (_accentColor == value) return;
                _accentColor = value;
                Save();
                AppearanceChanged?.Invoke();
            }
        }

        private static bool _graphFill = true;
        /// <summary>آیا زیر خط گراف‌ها با گرادیان پر شود؟</summary>
        public static bool GraphFill
        {
            get => _graphFill;
            set
            {
                if (_graphFill == value) return;
                _graphFill = value;
                Save();
                AppearanceChanged?.Invoke();
            }
        }

        private static bool _alwaysOnTop;
        /// <summary>پنجره همیشه روی بقیه‌ی پنجره‌ها بماند؟</summary>
        public static bool AlwaysOnTop
        {
            get => _alwaysOnTop;
            set
            {
                if (_alwaysOnTop == value) return;
                _alwaysOnTop = value;
                Save();
                ThemeManager.ApplyAlwaysOnTop();
            }
        }

        private static bool _trayEnabled;
        /// <summary>فعال بودن آیکون‌های زنده‌ی System Tray (با Minimize/Close برنامه به Tray می‌رود)</summary>
        public static bool TrayEnabled
        {
            get => _trayEnabled;
            set
            {
                if (_trayEnabled == value) return;
                _trayEnabled = value;
                Save();
                TraySettingsChanged?.Invoke();
            }
        }

        private static string _trayMetricsCsv = "0";
        /// <summary>
        /// لیست متریک‌های Tray به صورت CSV. هر متریک یک آیکون جدا می‌سازد.
        /// 0=CPU  1=RAM  2=GPU  3=Disk  4=سرعت دانلود  5=سرعت آپلود
        /// </summary>
        public static string TrayMetricsCsv
        {
            get => _trayMetricsCsv;
            set
            {
                if (_trayMetricsCsv == value) return;
                _trayMetricsCsv = value;
                Save();
                TraySettingsChanged?.Invoke();
            }
        }

        /// <summary>لیست متریک‌های Tray به صورت عدد (از TrayMetricsCsv)</summary>
        public static List<int> TrayMetricsList
        {
            get
            {
                var list = new List<int>();
                foreach (var part in _trayMetricsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (int.TryParse(part, out int v) && v >= 0 && v <= 5 && !list.Contains(v))
                        list.Add(v);
                return list;
            }
        }

        private static int _trayStyle;
        /// <summary>استایل آیکون Tray: 0 = بج رنگی، 1 = فقط عدد رنگی، 2 = mini-گراف زنده</summary>
        public static int TrayStyle
        {
            get => _trayStyle;
            set
            {
                value = Math.Clamp(value, 0, 2);
                if (_trayStyle == value) return;
                _trayStyle = value;
                Save();
                TraySettingsChanged?.Invoke();
            }
        }

        private static int _trayTextScale = 100;
        /// <summary>اندازه‌ی متن آیکون Tray به درصد (75 تا 150)</summary>
        public static int TrayTextScale
        {
            get => _trayTextScale;
            set
            {
                value = Math.Clamp(value, 75, 150);
                if (_trayTextScale == value) return;
                _trayTextScale = value;
                Save();
                TraySettingsChanged?.Invoke();
            }
        }

        private static string _trayColor = "#61AFFE";
        /// <summary>رنگ آیکون‌های Tray (کد هگز)</summary>
        public static string TrayColor
        {
            get => _trayColor;
            set
            {
                if (_trayColor == value) return;
                _trayColor = value;
                Save();
                TraySettingsChanged?.Invoke();
            }
        }

        private static bool _hotkeyEnabled = true;
        /// <summary>هات‌کی سراسری Ctrl+Alt+T برای باز کردن برنامه</summary>
        public static bool HotkeyEnabled
        {
            get => _hotkeyEnabled;
            set
            {
                if (_hotkeyEnabled == value) return;
                _hotkeyEnabled = value;
                Save();
                TraySettingsChanged?.Invoke();
            }
        }

        // ---------- تنظیمات جداگانه‌ی هر آیکون Tray ----------
        // هر متریک می‌تواند رنگ / استایل / اندازه‌ی مخصوص خودش را داشته باشد.
        // مقدار خالی یا -1 یعنی «از تنظیم سراسری استفاده کن».

        /// <summary>رنگ اختصاصی آیکون یک متریک ("" = سراسری)</summary>
        public static string GetTrayIconColor(int metric)
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue($"TrayC{metric}", out var v) &&
                    v is string s && s.StartsWith('#'))
                    return s;
            }
            catch { }
            return "";
        }

        public static void SetTrayIconColor(int metric, string hex)
        {
            try { ApplicationData.Current.LocalSettings.Values[$"TrayC{metric}"] = hex ?? ""; } catch { }
            TraySettingsChanged?.Invoke();
        }

        /// <summary>استایل اختصاصی آیکون یک متریک (-1 = سراسری، 0 = بج، 1 = متن، 2 = گراف)</summary>
        public static int GetTrayIconStyle(int metric)
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue($"TrayS{metric}", out var v) && v is int i)
                    return Math.Clamp(i, -1, 2);
            }
            catch { }
            return -1;
        }

        public static void SetTrayIconStyle(int metric, int style)
        {
            try { ApplicationData.Current.LocalSettings.Values[$"TrayS{metric}"] = Math.Clamp(style, -1, 2); } catch { }
            TraySettingsChanged?.Invoke();
        }

        /// <summary>اندازه‌ی اختصاصی متن آیکون یک متریک به درصد (-1 = سراسری)</summary>
        public static int GetTrayIconScale(int metric)
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue($"TrayZ{metric}", out var v) && v is int i)
                    return i < 0 ? -1 : Math.Clamp(i, 75, 150);
            }
            catch { }
            return -1;
        }

        public static void SetTrayIconScale(int metric, int scale)
        {
            try { ApplicationData.Current.LocalSettings.Values[$"TrayZ{metric}"] = scale < 0 ? -1 : Math.Clamp(scale, 75, 150); } catch { }
            TraySettingsChanged?.Invoke();
        }

        // ---------- آلارم مصرف ----------

        private static bool _alarmEnabled;
        /// <summary>نوتیفیکیشن ویندوز وقتی CPU / RAM / دما از حد رد شد</summary>
        public static bool AlarmEnabled
        {
            get => _alarmEnabled;
            set { if (_alarmEnabled == value) return; _alarmEnabled = value; Save(); }
        }

        private static int _alarmCpu = 90;
        public static int AlarmCpuLimit
        {
            get => _alarmCpu;
            set { value = Math.Clamp(value, 50, 100); if (_alarmCpu == value) return; _alarmCpu = value; Save(); }
        }

        private static int _alarmRam = 90;
        public static int AlarmRamLimit
        {
            get => _alarmRam;
            set { value = Math.Clamp(value, 50, 100); if (_alarmRam == value) return; _alarmRam = value; Save(); }
        }

        private static int _alarmTemp = 85;
        public static int AlarmTempLimit
        {
            get => _alarmTemp;
            set { value = Math.Clamp(value, 40, 110); if (_alarmTemp == value) return; _alarmTemp = value; Save(); }
        }

        // ---------- ویجت شناور دسکتاپ ----------

        private static bool _widgetEnabled;
        /// <summary>پنجره‌ی کوچک always-on-top با گراف CPU/RAM/GPU</summary>
        public static bool WidgetEnabled
        {
            get => _widgetEnabled;
            set { if (_widgetEnabled == value) return; _widgetEnabled = value; Save(); WidgetChanged?.Invoke(); }
        }

        // ---------- زبان ----------

        private static int _language;
        /// <summary>زبان رابط: 0=English، 1=فارسی (RTL)، 2=Русский، 3=Azərbaycan، 4=Türkçe</summary>
        public static int Language
        {
            get => _language;
            set
            {
                value = Math.Clamp(value, 0, 4);
                if (_language == value) return;
                _language = value;
                Save();
                LanguageChanged?.Invoke();
            }
        }

        // ---------- صدا ----------

        private static bool _endTaskSound = true;
        /// <summary>صدای ظریف هنگام End Task</summary>
        public static bool EndTaskSound
        {
            get => _endTaskSound;
            set { if (_endTaskSound == value) return; _endTaskSound = value; Save(); }
        }

        // ---------- ذخیره و بازیابی ----------

        public static void Load()
        {
            try
            {
                var s = ApplicationData.Current.LocalSettings.Values;

                if (s.TryGetValue("RefreshMs", out var refresh) && refresh is int r)
                    _refreshIntervalMs = Math.Clamp(r, 500, 2000);

                if (s.TryGetValue("Theme", out var theme) && theme is int t &&
                    Enum.IsDefined(typeof(AppTheme), t))
                    _theme = (AppTheme)t;

                if (s.TryGetValue("Accent", out var accent) && accent is string a && a.StartsWith('#'))
                    _accentColor = a;

                if (s.TryGetValue("GraphFill", out var fill) && fill is bool f)
                    _graphFill = f;

                if (s.TryGetValue("AlwaysOnTop", out var top) && top is bool o)
                    _alwaysOnTop = o;

                if (s.TryGetValue("TrayOn", out var trayOn) && trayOn is bool tr)
                    _trayEnabled = tr;

                if (s.TryGetValue("TrayMetrics", out var tm) && tm is string tms && tms.Length > 0)
                    _trayMetricsCsv = tms;
                else if (s.TryGetValue("TrayMetric", out var tmOld) && tmOld is int tmi)
                    _trayMetricsCsv = Math.Clamp(tmi, 0, 3).ToString(); // سازگاری با نسخه‌ی 1.3

                if (s.TryGetValue("TrayStyle", out var ts) && ts is int tsi)
                    _trayStyle = Math.Clamp(tsi, 0, 2);

                if (s.TryGetValue("TrayScale", out var tsc) && tsc is int tsci)
                    _trayTextScale = Math.Clamp(tsci, 75, 150);

                if (s.TryGetValue("TrayColor", out var tc) && tc is string tcs && tcs.StartsWith('#'))
                    _trayColor = tcs;

                if (s.TryGetValue("HotkeyOn", out var hk) && hk is bool h)
                    _hotkeyEnabled = h;

                if (s.TryGetValue("AlarmOn", out var al) && al is bool ab) _alarmEnabled = ab;
                if (s.TryGetValue("AlarmCpu", out var ac) && ac is int aci) _alarmCpu = Math.Clamp(aci, 50, 100);
                if (s.TryGetValue("AlarmRam", out var ar) && ar is int ari) _alarmRam = Math.Clamp(ari, 50, 100);
                if (s.TryGetValue("AlarmTemp", out var at) && at is int ati) _alarmTemp = Math.Clamp(ati, 40, 110);
                if (s.TryGetValue("WidgetOn", out var wg) && wg is bool wb) _widgetEnabled = wb;
                if (s.TryGetValue("Lang", out var lg) && lg is int lgi) _language = Math.Clamp(lgi, 0, 4);
                if (s.TryGetValue("EndSound", out var es) && es is bool esb) _endTaskSound = esb;
            }
            catch
            {
                // اگر خواندن تنظیمات ممکن نبود، با مقادیر پیش‌فرض ادامه می‌دهیم
            }
        }

        private static void Save()
        {
            try
            {
                var s = ApplicationData.Current.LocalSettings.Values;
                s["RefreshMs"] = _refreshIntervalMs;
                s["Theme"] = (int)_theme;
                s["Accent"] = _accentColor;
                s["GraphFill"] = _graphFill;
                s["AlwaysOnTop"] = _alwaysOnTop;
                s["TrayOn"] = _trayEnabled;
                s["TrayMetrics"] = _trayMetricsCsv;
                s["TrayStyle"] = _trayStyle;
                s["TrayScale"] = _trayTextScale;
                s["TrayColor"] = _trayColor;
                s["HotkeyOn"] = _hotkeyEnabled;
                s["AlarmOn"] = _alarmEnabled;
                s["AlarmCpu"] = _alarmCpu;
                s["AlarmRam"] = _alarmRam;
                s["AlarmTemp"] = _alarmTemp;
                s["WidgetOn"] = _widgetEnabled;
                s["Lang"] = _language;
                s["EndSound"] = _endTaskSound;
            }
            catch
            {
            }
        }
    }
}
