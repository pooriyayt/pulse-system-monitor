using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace TaskManagerPro.Helpers
{
    /// <summary>
    /// ترجمه‌ی رابط کاربری (English / فارسی) + جهت راست‌به‌چپ.
    /// هر صفحه در Loaded خودش L10n.T را روی متن‌های ثابت اعمال می‌کند.
    /// </summary>
    public static class L10n
    {
        public static bool IsFa => AppSettings.Language == 1;

        /// <summary>جهت چیدمان بر اساس زبان</summary>
        public static FlowDirection Direction => IsFa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        /// <summary>روی ریشه‌ی پنجره اعمال می‌شود تا کل برنامه RTL/LTR شود</summary>
        public static void ApplyDirection(Window? window = null)
        {
            window ??= App.MainAppWindow;
            if (window?.Content is FrameworkElement root)
                root.FlowDirection = Direction;
        }

        private static readonly Dictionary<string, string> Fa = new()
        {
            // ناوبری
            ["Overview"] = "نمای کلی",
            ["Performance"] = "عملکرد",
            ["Processes"] = "پردازه‌ها",
            ["Startup Apps"] = "برنامه‌های استارتاپ",
            ["Services"] = "سرویس‌ها",
            ["Settings"] = "تنظیمات",
            ["Task Manager Pro"] = "تسک منیجر پرو",

            // عمومی
            ["Refresh"] = "بروزرسانی",
            ["Search"] = "جستجو",
            ["Error"] = "خطا",
            ["Close"] = "بستن",
            ["Copy"] = "کپی",
            ["Name"] = "نام",
            ["Memory"] = "حافظه",
            ["Disk"] = "دیسک",
            ["Network"] = "شبکه",
            ["Loading processes..."] = "در حال بارگذاری پردازه‌ها...",

            // Processes
            ["End task"] = "پایان کار",
            ["Restart"] = "ری‌استارت",
            ["Run new task"] = "اجرای کار جدید",
            ["Export CSV"] = "خروجی CSV",
            ["Copy details"] = "کپی جزئیات",
            ["Suspend"] = "فریز (Suspend)",
            ["Resume"] = "آنفریز (Resume)",
            ["Efficiency mode"] = "حالت بهره‌وری",
            ["Set priority"] = "اولویت",
            ["Open file location"] = "باز کردن محل فایل",
            ["Properties"] = "جزئیات",
            ["Details"] = "جزئیات",
            ["Search processes  (Ctrl+F)"] = "جستجوی پردازه‌ها  (Ctrl+F)",
            ["Filter: All"] = "فیلتر: همه",
            ["Filter: Apps only"] = "فیلتر: فقط برنامه‌ها",
            ["Filter: CPU > 1%"] = "فیلتر: CPU بیش از ۱٪",
            ["Filter: Memory > 100 MB"] = "فیلتر: حافظه بیش از ۱۰۰MB",
            ["Filter: Active network"] = "فیلتر: شبکه‌ی فعال",

            // Performance
            ["% Utilization"] = "٪ مصرف",
            ["Memory usage (%)"] = "مصرف حافظه (٪)",
            ["Active time (%)"] = "زمان فعال (٪)",
            ["Download"] = "دانلود",
            ["Upload"] = "آپلود",
            ["Top processes"] = "پرمصرف‌ترین پردازه‌ها",
            ["Logical processors"] = "پردازنده‌های منطقی",
            ["Live"] = "زنده",
            ["Last 10 minutes"] = "۱۰ دقیقه‌ی اخیر",
            ["Last hour"] = "۱ ساعت اخیر",
            ["History"] = "تاریخچه",
            ["Sensors"] = "سنسورها",
            ["Temperature / Fan / Voltage / Power"] = "دما / فن / ولتاژ / توان",
            ["Opening hardware sensors..."] = "در حال باز کردن سنسورهای سخت‌افزار...",
            ["Your system does not support this section. The hardware or its driver does not expose temperature/fan/voltage sensors."] =
                "سیستم شما این بخش را پشتیبانی نمی‌کند. سخت‌افزار یا درایور آن، سنسور دما/فن/ولتاژ ارائه نمی‌دهد.",
            ["No sensors available. Try running the app as administrator — if it still shows nothing, your system does not support this section."] =
                "سنسوری در دسترس نیست. برنامه را Run as administrator اجرا کنید — اگر باز هم چیزی نمایش داده نشد، سیستم شما این بخش را پشتیبانی نمی‌کند.",

            // Startup
            ["Programs that start automatically with Windows"] = "برنامه‌هایی که با ویندوز اجرا می‌شوند",
            ["Startup impact"] = "تأثیر روی بوت",
            ["High"] = "زیاد",
            ["Medium"] = "متوسط",
            ["Low"] = "کم",
            ["Not measured"] = "نامشخص",

            // Settings
            ["Appearance"] = "ظاهر",
            ["Choose the app theme"] = "تم برنامه را انتخاب کن",
            ["Accent color"] = "رنگ اکسنت",
            ["Custom color"] = "رنگ دلخواه",
            ["Refresh rate"] = "سرعت بروزرسانی",
            ["Window"] = "پنجره",
            ["System tray"] = "سیستم تری",
            ["Keyboard shortcuts"] = "میانبرهای صفحه‌کلید",
            ["About"] = "درباره",
            ["Usage alarms"] = "آلارم مصرف",
            ["Desktop widget"] = "ویجت دسکتاپ",
            ["Language"] = "زبان",
            ["Fill area under the graph line"] = "پر کردن ناحیه‌ی زیر خط گراف",
            ["Always on top (keep this window above all others)"] = "همیشه روی همه‌ی پنجره‌ها",
            ["Tray icon color"] = "رنگ آیکون تری",
            ["Icon style"] = "استایل آیکون",
            ["Sound"] = "صدا",
            ["Play a subtle sound when ending a task"] = "پخش صدای ظریف هنگام پایان دادن به یک پردازه",
        };

        /// <summary>برگردان یک متن — اگر زبان انگلیسی باشد همان متن برمی‌گردد</summary>
        public static string T(string en) =>
            IsFa && Fa.TryGetValue(en, out var fa) ? fa : en;
    }
}
