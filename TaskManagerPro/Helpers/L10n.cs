using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace TaskManagerPro.Helpers
{
    public static class L10n
    {
        public static bool IsFa => AppSettings.Language == 1;

        public static FlowDirection Direction => IsFa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

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
            ["Download"] = "دانلود",
            ["Upload"] = "آپلود",
            ["Hardware"] = "سخت‌افزار",
            ["Open"] = "باز کردن",
            ["Run"] = "اجرا",
            ["Apply"] = "اعمال",
            ["Install"] = "نصب",
            ["Later"] = "بعداً",
            ["Note"] = "توجه",
            ["Start"] = "شروع",
            ["Stop"] = "توقف",
            ["Exit"] = "خروج",

            // Processes
            ["End task"] = "پایان کار",
            ["Restart"] = "ری‌استارت",
            ["Run new task"] = "اجرای کار جدید",
            ["Export CSV"] = "خروجی CSV",
            ["Copy details"] = "کپی جزئیات",
            ["Suspend"] = "فریز (Suspend)",
            ["Resume"] = "آنفریز (Resume)",
            ["Efficiency mode"] = "حالت بهره‌وری",
            ["Disable efficiency mode"] = "غیرفعال کردن حالت بهره‌وری",
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
            ["Sort: CPU"] = "مرتب‌سازی: CPU",
            ["Sort: Memory"] = "مرتب‌سازی: حافظه",
            ["Sort: Disk"] = "مرتب‌سازی: دیسک",
            ["Sort: Network"] = "مرتب‌سازی: شبکه",
            ["Sort: GPU"] = "مرتب‌سازی: GPU",
            ["Sort: Name"] = "مرتب‌سازی: نام",
            ["Sort: PID"] = "مرتب‌سازی: PID",
            ["Run as administrator"] = "اجرا به عنوان مدیر",
            ["e.g. notepad, cmd, mspaint"] = "مثال: notepad, cmd, mspaint",
            ["PID"] = "PID",
            ["Name column"] = "نام",
            ["GPU usage is not available on this system"] = "اطلاعات GPU در این سیستم در دسترس نیست",

            // Performance
            ["% Utilization"] = "٪ مصرف",
            ["Memory usage (%)"] = "مصرف حافظه (٪)",
            ["Active time (%)"] = "زمان فعال (٪)",
            ["Transfer rate — Read + Write (MB/s)"] = "نرخ انتقال — خواندن + نوشتن (MB/s)",
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
            // Performance stats
            ["Utilization"] = "مصرف",
            ["Speed"] = "سرعت",
            ["Threads"] = "رشته‌ها",
            ["Up time"] = "مدت روشن بودن",
            ["Temperature"] = "دما",
            ["In use"] = "در حال استفاده",
            ["Available"] = "آزاد",
            ["Total"] = "کل",
            ["Page file"] = "فایل صفحه",
            ["Dedicated memory"] = "حافظه‌ی اختصاصی",
            ["Active time"] = "زمان فعال",
            ["Read speed"] = "سرعت خواندن",
            ["Write speed"] = "سرعت نوشتن",
            ["Video Decode"] = "رمزگشایی ویدیو",
            ["Video Processing"] = "پردازش ویدیو",
            ["Full sensor data (fans, voltages, power) may need Run as administrator."] =
                "داده‌های کامل سنسور (فن‌ها، ولتاژ، توان) ممکن است به اجرا به عنوان مدیر نیاز داشته باشد.",

            // Startup
            ["Programs that start automatically with Windows"] = "برنامه‌هایی که با ویندوز اجرا می‌شوند",
            ["Startup impact"] = "تأثیر روی بوت",
            ["Estimated startup impact"] = "تأثیر تخمینی روی بوت",
            ["High"] = "زیاد",
            ["Medium"] = "متوسط",
            ["Low"] = "کم",
            ["Not measured"] = "نامشخص",

            // Services
            ["Search services...  (Ctrl+F)"] = "جستجوی سرویس‌ها  (Ctrl+F)",

            // Settings
            ["Appearance"] = "ظاهر",
            ["Choose the app theme"] = "تم برنامه را انتخاب کنید",
            ["Dark (Mica)"] = "تیره (Mica)",
            ["Light (Mica)"] = "روشن (Mica)",
            ["Liquid Glass (deep blur + transparency)"] = "شیشه‌ای (بلور + شفافیت)",
            ["Midnight (deeper dark, Mica Alt)"] = "نیمه‌شب (تیره‌تر، Mica Alt)",
            ["Aurora (light glass, Acrylic)"] = "آرورا (شیشه‌ی روشن، Acrylic)",
            ["OLED (pure black, no blur)"] = "OLED (مشکی خالص، بدون بلور)",
            ["Paper (flat light)"] = "کاغذ (روشن ساده)",
            ["Language"] = "زبان",
            ["Accent color"] = "رنگ اکسنت",
            ["Used for the whole app (buttons, highlights) and all live graphs"] = "برای کل برنامه (دکمه‌ها، برجسته‌سازی‌ها) و همه‌ی گراف‌های زنده",
            ["Custom color"] = "رنگ دلخواه",
            ["Custom color (wheel / hex)"] = "رنگ دلخواه (چرخ رنگ / هگز)",
            ["Fill area under the graph line"] = "پر کردن ناحیه‌ی زیر خط گراف",
            ["Refresh rate"] = "سرعت بروزرسانی",
            ["Graphs update every {0} ms"] = "گراف‌ها هر {0} میلی‌ثانیه بروزرسانی می‌شوند",
            ["Window"] = "پنجره",
            ["Always on top (keep this window above all others)"] = "همیشه روی همه‌ی پنجره‌ها",
            ["System tray"] = "سیستم تری",
            ["Live usage icon in the system tray — closing or minimizing hides the app to the tray"] =
                "آیکون زنده‌ی مصرف در System Tray — بستن یا کوچک کردن، برنامه را به Tray می‌فرستد",
            ["Tray items — pick one or more, each gets its own live icon"] =
                "آیتم‌های Tray — یک یا چند تا انتخاب کنید، هر کدام آیکون زنده‌ی خودش را دارد",
            ["Download / Upload speeds need the app to run as administrator"] =
                "سرعت دانلود / آپلود نیاز به اجرا به عنوان مدیر دارد",
            ["Icon style"] = "استایل آیکون",
            ["Colored badge"] = "نشان رنگی",
            ["Text only (transparent, colored text)"] = "فقط متن (پس‌زمینه شفاف، متن رنگی)",
            ["Mini live graph"] = "گراف زنده‌ی کوچک",
            ["Tray icon color"] = "رنگ آیکون تری",
            ["Per-icon customization — override color, style and size for each tray icon"] =
                "شخصی‌سازی هر آیکون — رنگ، استایل و اندازه‌ی هر آیکون Tray را تغییر دهید",
            ["Global hotkey Ctrl + Alt + T to show / hide the window"] =
                "میانبر جهانی Ctrl + Alt + T برای نمایش / پنهان کردن پنجره",
            ["Tray text size: {0}%"] = "اندازه‌ی متن تری: {0}٪",
            ["Color"] = "رنگ",
            ["Global"] = "کلی",
            ["Style"] = "استایل",
            ["Use global style"] = "استفاده از استایل کلی",
            ["Text only"] = "فقط متن",
            ["Custom text size"] = "اندازه‌ی متن دلخواه",
            ["{0} icon"] = "آیکون {0}",
            ["Keyboard shortcuts"] = "میانبرهای صفحه‌کلید",
            ["Ctrl + 1 ... 5 — switch between tabs"] = "Ctrl + ۱ ... ۵ — تغییر تب",
            ["Ctrl + F — focus search (Processes / Services)"] = "Ctrl + F — رفتن به جستجو (پردازه‌ها / سرویس‌ها)",
            ["F5 — refresh the current list"] = "F5 — بروزرسانی لیست",
            ["Delete — end the selected process"] = "Delete — پایان دادن به پردازه‌ی انتخابی",
            ["Ctrl + Alt + T — show / hide the window from anywhere in Windows"] =
                "Ctrl + Alt + T — نمایش / پنهان کردن پنجره از هر جایی در ویندوز",
            ["Updates"] = "بروزرسانی",
            ["Check for updates"] = "بررسی بروزرسانی",
            ["Checking for updates..."] = "در حال بررسی بروزرسانی...",
            ["Could not reach the update server. Check your internet connection."] =
                "سرور بروزرسانی در دسترس نیست. اتصال اینترنت را بررسی کنید.",
            ["You have the latest version."] = "آخرین نسخه را دارید.",
            ["Downloading version {0}..."] = "در حال دانلود نسخه‌ی {0}...",
            ["Download failed. Try again later."] = "دانلود ناموفق بود. بعداً دوباره امتحان کنید.",
            ["Version {0} downloaded. Install it to update."] = "نسخه‌ی {0} دانلود شد. برای بروزرسانی نصب کنید.",
            ["Update ready"] = "بروزرسانی آماده است",
            ["Version {0} has been downloaded. Install it now to update Pulse."] =
                "نسخه‌ی {0} دانلود شد. همین حالا نصب کنید تا Pulse بروزرسانی شود.",
            ["About"] = "درباره",
            ["A task manager for Windows 11, built with WinUI 3 and .NET 8."] =
                "یک تسک منیجر برای ویندوز ۱۱، ساخته‌شده با WinUI 3 و .NET 8.",
            ["Sound"] = "صدا",
            ["Play a subtle sound when ending a task"] = "پخش صدای ظریف هنگام پایان دادن به یک پردازه",
            ["Usage alarms"] = "آلارم مصرف",
            ["Windows notification when CPU / RAM / temperature exceeds a limit"] =
                "اعلان ویندوز وقتی CPU / RAM / دما از حد تعیین‌شده رد می‌شود",
            ["CPU alarm at {0}%"] = "آلارم CPU در {0}٪",
            ["RAM alarm at {0}%"] = "آلارم RAM در {0}٪",
            ["Temperature alarm at {0} °C"] = "آلارم دما در {0} درجه‌ی سانتی‌گراد",
            ["Desktop widget"] = "ویجت دسکتاپ",
            ["Small always-on-top window with live CPU / RAM / GPU graphs"] =
                "پنجره‌ی کوچک همیشه-روی-بقیه با گراف‌های زنده‌ی CPU / RAM / GPU",
            ["Hide widget"] = "پنهان کردن ویجت",
            ["Show Pulse"] = "نمایش Pulse",

            // Overview sections
            ["CPU"] = "CPU",
            ["RAM"] = "RAM",
            ["GPU"] = "GPU",
            ["Page File: {0}% used"] = "فایل صفحه: {0}٪ استفاده‌شده",
        };

        public static string T(string en) =>
            IsFa && Fa.TryGetValue(en, out var fa) ? fa : en;
    }
}
