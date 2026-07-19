using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace TaskManagerPro.Helpers
{
    public static class L10n
    {
        public static bool IsFa => AppSettings.Language == 1;

        public static FlowDirection Direction =>
            AppSettings.Language == 1 ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

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

            // گروه‌های پردازه‌ها
            ["Apps"] = "برنامه‌ها",
            ["Background processes"] = "پردازه‌های پس‌زمینه",
            ["Windows processes"] = "پردازه‌های ویندوز",
            ["{0} processes  •  updated {1}"] = "{0} پردازه  •  به‌روزرسانی {1}",
            ["Could not end \"{0}\" — access denied. Try running the app as administrator."] =
                "پردازه \"{0}\" پایان نیافت — دسترسی رد شد. برنامه را Run as administrator اجرا کنید.",
            ["Could not end \"{0}\": {1}"] = "پردازه \"{0}\" پایان نیافت: {1}",

            // عملکرد
            ["Not available"] = "در دسترس نیست",
            ["now"] = "الان",
            ["Core"] = "هسته",

            // دیسک
            ["Read"] = "خواندن",
            ["Write"] = "نوشتن",

            // سرویس‌ها — وضعیت
            ["Running"] = "در حال اجرا",
            ["Stopped"] = "متوقف",
            ["StartPending"] = "در حال شروع",
            ["StopPending"] = "در حال توقف",
            ["Paused"] = "مکث شده",
            ["PausePending"] = "در حال مکث",
            ["ContinuePending"] = "در حال از سرگیری",
            ["Could not {0} \"{1}\". Most service operations require running the app as Administrator. ({2})"] =
                "نمی‌توان \"{1}\" را {0} کرد. اکثر عملیات سرویس‌ها به اجرا به عنوان مدیر نیاز دارند. ({2})",

            // استارتاپ
            ["For \"All users\" startup items, run the app as administrator."] =
                "برای تغییر آیتم‌های «All users» برنامه را با Run as administrator اجرا کنید.",
        };

        private static readonly Dictionary<string, string> Ru = new()
        {
            // Навигация
            ["Overview"] = "Обзор",
            ["Performance"] = "Производительность",
            ["Processes"] = "Процессы",
            ["Startup Apps"] = "Автозагрузка",
            ["Services"] = "Службы",
            ["Settings"] = "Настройки",
            ["Task Manager Pro"] = "Диспетчер задач Pro",

            // Общее
            ["Refresh"] = "Обновить",
            ["Search"] = "Поиск",
            ["Error"] = "Ошибка",
            ["Close"] = "Закрыть",
            ["Copy"] = "Копировать",
            ["Name"] = "Имя",
            ["Memory"] = "Память",
            ["Disk"] = "Диск",
            ["Network"] = "Сеть",
            ["Loading processes..."] = "Загрузка процессов...",
            ["Download"] = "Загрузка",
            ["Upload"] = "Отправка",
            ["Hardware"] = "Оборудование",
            ["Open"] = "Открыть",
            ["Run"] = "Запустить",
            ["Apply"] = "Применить",
            ["Install"] = "Установить",
            ["Later"] = "Позже",
            ["Note"] = "Примечание",
            ["Start"] = "Запустить",
            ["Stop"] = "Остановить",
            ["Exit"] = "Выход",

            // Процессы
            ["End task"] = "Завершить задачу",
            ["Restart"] = "Перезапустить",
            ["Run new task"] = "Запустить новую задачу",
            ["Export CSV"] = "Экспорт CSV",
            ["Copy details"] = "Копировать сведения",
            ["Suspend"] = "Приостановить",
            ["Resume"] = "Возобновить",
            ["Efficiency mode"] = "Режим эффективности",
            ["Disable efficiency mode"] = "Отключить режим эффективности",
            ["Set priority"] = "Приоритет",
            ["Open file location"] = "Открыть расположение файла",
            ["Properties"] = "Свойства",
            ["Details"] = "Подробности",
            ["Search processes  (Ctrl+F)"] = "Поиск процессов  (Ctrl+F)",
            ["Filter: All"] = "Фильтр: Все",
            ["Filter: Apps only"] = "Фильтр: Только приложения",
            ["Filter: CPU > 1%"] = "Фильтр: CPU > 1%",
            ["Filter: Memory > 100 MB"] = "Фильтр: Память > 100 МБ",
            ["Filter: Active network"] = "Фильтр: Активная сеть",
            ["Sort: CPU"] = "Сортировка: CPU",
            ["Sort: Memory"] = "Сортировка: Память",
            ["Sort: Disk"] = "Сортировка: Диск",
            ["Sort: Network"] = "Сортировка: Сеть",
            ["Sort: GPU"] = "Сортировка: GPU",
            ["Sort: Name"] = "Сортировка: Имя",
            ["Sort: PID"] = "Сортировка: PID",
            ["Run as administrator"] = "Запуск от имени администратора",
            ["e.g. notepad, cmd, mspaint"] = "Например: notepad, cmd, mspaint",
            ["PID"] = "PID",
            ["Name column"] = "Имя",
            ["GPU usage is not available on this system"] = "Данные GPU недоступны на этой системе",

            // Производительность
            ["% Utilization"] = "% Использование",
            ["Memory usage (%)"] = "Использование памяти (%)",
            ["Active time (%)"] = "Активное время (%)",
            ["Transfer rate — Read + Write (MB/s)"] = "Скорость передачи — Чтение + Запись (МБ/с)",
            ["Top processes"] = "Топ процессов",
            ["Logical processors"] = "Логические процессоры",
            ["Live"] = "Текущий",
            ["Last 10 minutes"] = "Последние 10 минут",
            ["Last hour"] = "Последний час",
            ["History"] = "История",
            ["Sensors"] = "Датчики",
            ["Temperature / Fan / Voltage / Power"] = "Температура / Вентилятор / Напряжение / Мощность",
            ["Opening hardware sensors..."] = "Открытие датчиков оборудования...",
            ["Your system does not support this section. The hardware or its driver does not expose temperature/fan/voltage sensors."] =
                "Ваша система не поддерживает этот раздел. Оборудование или его драйвер не предоставляет датчики температуры/вентилятора/напряжения.",
            ["No sensors available. Try running the app as administrator — if it still shows nothing, your system does not support this section."] =
                "Датчики недоступны. Попробуйте запустить приложение от имени администратора — если по-прежнему ничего нет, ваша система не поддерживает этот раздел.",
            ["Utilization"] = "Использование",
            ["Speed"] = "Скорость",
            ["Threads"] = "Потоки",
            ["Up time"] = "Время работы",
            ["Temperature"] = "Температура",
            ["In use"] = "Используется",
            ["Available"] = "Доступно",
            ["Total"] = "Всего",
            ["Page file"] = "Файл подкачки",
            ["Dedicated memory"] = "Выделенная память",
            ["Active time"] = "Активное время",
            ["Read speed"] = "Скорость чтения",
            ["Write speed"] = "Скорость записи",
            ["Video Decode"] = "Декодирование видео",
            ["Video Processing"] = "Обработка видео",
            ["Full sensor data (fans, voltages, power) may need Run as administrator."] =
                "Полные данные датчиков (вентиляторы, напряжение, мощность) могут потребовать запуска от имени администратора.",

            // Автозагрузка
            ["Programs that start automatically with Windows"] = "Программы, запускаемые вместе с Windows",
            ["Startup impact"] = "Влияние на запуск",
            ["Estimated startup impact"] = "Примерное влияние на запуск",
            ["High"] = "Высокое",
            ["Medium"] = "Среднее",
            ["Low"] = "Низкое",
            ["Not measured"] = "Не измерено",

            // Службы
            ["Search services...  (Ctrl+F)"] = "Поиск служб...  (Ctrl+F)",

            // Настройки
            ["Appearance"] = "Внешний вид",
            ["Choose the app theme"] = "Выберите тему приложения",
            ["Dark (Mica)"] = "Тёмная (Mica)",
            ["Light (Mica)"] = "Светлая (Mica)",
            ["Liquid Glass (deep blur + transparency)"] = "Жидкое стекло (сильное размытие + прозрачность)",
            ["Midnight (deeper dark, Mica Alt)"] = "Полночь (темнее, Mica Alt)",
            ["Aurora (light glass, Acrylic)"] = "Аврора (светлое стекло, Acrylic)",
            ["OLED (pure black, no blur)"] = "OLED (чистый чёрный, без размытия)",
            ["Paper (flat light)"] = "Бумага (плоский светлый)",
            ["Language"] = "Язык",
            ["Accent color"] = "Акцентный цвет",
            ["Used for the whole app (buttons, highlights) and all live graphs"] = "Используется для всего приложения (кнопки, выделение) и всех графиков",
            ["Custom color"] = "Произвольный цвет",
            ["Custom color (wheel / hex)"] = "Произвольный цвет (палитра / hex)",
            ["Fill area under the graph line"] = "Заливка под линией графика",
            ["Refresh rate"] = "Частота обновления",
            ["Graphs update every {0} ms"] = "Графики обновляются каждые {0} мс",
            ["Window"] = "Окно",
            ["Always on top (keep this window above all others)"] = "Поверх всех окон",
            ["System tray"] = "Системный трей",
            ["Live usage icon in the system tray — closing or minimizing hides the app to the tray"] =
                "Живой значок в системном трее — закрытие или сворачивание скрывает приложение в трей",
            ["Tray items — pick one or more, each gets its own live icon"] =
                "Элементы трея — выберите один или несколько, каждый получит свой живой значок",
            ["Download / Upload speeds need the app to run as administrator"] =
                "Скорость загрузки/отправки требует запуска от имени администратора",
            ["Icon style"] = "Стиль значка",
            ["Colored badge"] = "Цветной значок",
            ["Text only (transparent, colored text)"] = "Только текст (прозрачный фон, цветной текст)",
            ["Mini live graph"] = "Мини живой график",
            ["Tray icon color"] = "Цвет значка трея",
            ["Per-icon customization — override color, style and size for each tray icon"] =
                "Настройка каждого значка — задайте цвет, стиль и размер для каждого значка трея",
            ["Global hotkey Ctrl + Alt + T to show / hide the window"] =
                "Глобальная горячая клавиша Ctrl + Alt + T для показа/скрытия окна",
            ["Tray text size: {0}%"] = "Размер текста трея: {0}%",
            ["Color"] = "Цвет",
            ["Global"] = "Глобально",
            ["Style"] = "Стиль",
            ["Use global style"] = "Использовать глобальный стиль",
            ["Text only"] = "Только текст",
            ["Custom text size"] = "Произвольный размер текста",
            ["{0} icon"] = "Значок {0}",
            ["Keyboard shortcuts"] = "Горячие клавиши",
            ["Ctrl + 1 ... 5 — switch between tabs"] = "Ctrl + 1 ... 5 — переключение вкладок",
            ["Ctrl + F — focus search (Processes / Services)"] = "Ctrl + F — фокус поиска (Процессы / Службы)",
            ["F5 — refresh the current list"] = "F5 — обновить текущий список",
            ["Delete — end the selected process"] = "Delete — завершить выбранный процесс",
            ["Ctrl + Alt + T — show / hide the window from anywhere in Windows"] =
                "Ctrl + Alt + T — показать/скрыть окно из любого места в Windows",
            ["Updates"] = "Обновления",
            ["Check for updates"] = "Проверить обновления",
            ["Checking for updates..."] = "Проверка обновлений...",
            ["Could not reach the update server. Check your internet connection."] =
                "Не удалось связаться с сервером обновлений. Проверьте подключение к интернету.",
            ["You have the latest version."] = "У вас последняя версия.",
            ["Downloading version {0}..."] = "Загрузка версии {0}...",
            ["Download failed. Try again later."] = "Загрузка не удалась. Попробуйте позже.",
            ["Version {0} downloaded. Install it to update."] = "Версия {0} загружена. Установите для обновления.",
            ["Update ready"] = "Обновление готово",
            ["Version {0} has been downloaded. Install it now to update Pulse."] =
                "Версия {0} загружена. Установите сейчас, чтобы обновить Pulse.",
            ["About"] = "О программе",
            ["A task manager for Windows 11, built with WinUI 3 and .NET 8."] =
                "Диспетчер задач для Windows 11, созданный на WinUI 3 и .NET 8.",
            ["Sound"] = "Звук",
            ["Play a subtle sound when ending a task"] = "Воспроизводить звук при завершении задачи",
            ["Usage alarms"] = "Оповещения нагрузки",
            ["Windows notification when CPU / RAM / temperature exceeds a limit"] =
                "Уведомление Windows при превышении лимита CPU / ОЗУ / температуры",
            ["CPU alarm at {0}%"] = "Оповещение CPU при {0}%",
            ["RAM alarm at {0}%"] = "Оповещение ОЗУ при {0}%",
            ["Temperature alarm at {0} °C"] = "Оповещение температуры при {0} °C",
            ["Desktop widget"] = "Виджет рабочего стола",
            ["Small always-on-top window with live CPU / RAM / GPU graphs"] =
                "Маленькое окно поверх всех с живыми графиками CPU / ОЗУ / GPU",
            ["Hide widget"] = "Скрыть виджет",
            ["Show Pulse"] = "Показать Pulse",

            ["CPU"] = "CPU",
            ["RAM"] = "ОЗУ",
            ["GPU"] = "GPU",
            ["Page File: {0}% used"] = "Файл подкачки: {0}% использовано",

            ["Apps"] = "Приложения",
            ["Background processes"] = "Фоновые процессы",
            ["Windows processes"] = "Процессы Windows",
            ["{0} processes  •  updated {1}"] = "{0} процессов  •  обновлено {1}",
            ["Could not end \"{0}\" — access denied. Try running the app as administrator."] =
                "Не удалось завершить \"{0}\" — доступ запрещён. Попробуйте запустить от имени администратора.",
            ["Could not end \"{0}\": {1}"] = "Не удалось завершить \"{0}\": {1}",

            ["Not available"] = "Недоступно",
            ["now"] = "сейчас",
            ["Core"] = "Ядро",
            ["Read"] = "Чтение",
            ["Write"] = "Запись",

            ["Running"] = "Работает",
            ["Stopped"] = "Остановлена",
            ["StartPending"] = "Запускается",
            ["StopPending"] = "Останавливается",
            ["Paused"] = "Приостановлена",
            ["PausePending"] = "Приостанавливается",
            ["ContinuePending"] = "Возобновляется",
            ["Could not {0} \"{1}\". Most service operations require running the app as Administrator. ({2})"] =
                "Не удалось {0} \"{1}\". Большинство операций со службами требуют прав администратора. ({2})",

            ["For \"All users\" startup items, run the app as administrator."] =
                "Для элементов «All users» запустите приложение от имени администратора.",
        };

        private static readonly Dictionary<string, string> Az = new()
        {
            // Naviqasiya
            ["Overview"] = "İcmal",
            ["Performance"] = "Performans",
            ["Processes"] = "Proseslər",
            ["Startup Apps"] = "Başlanğıc Proqramları",
            ["Services"] = "Xidmətlər",
            ["Settings"] = "Parametrlər",
            ["Task Manager Pro"] = "Tapşırıq İdarəçisi Pro",

            // Ümumi
            ["Refresh"] = "Yenilə",
            ["Search"] = "Axtar",
            ["Error"] = "Xəta",
            ["Close"] = "Bağla",
            ["Copy"] = "Kopyala",
            ["Name"] = "Ad",
            ["Memory"] = "Yaddaş",
            ["Disk"] = "Disk",
            ["Network"] = "Şəbəkə",
            ["Loading processes..."] = "Proseslər yüklənir...",
            ["Download"] = "Yükləmə",
            ["Upload"] = "Göndərmə",
            ["Hardware"] = "Avadanlıq",
            ["Open"] = "Aç",
            ["Run"] = "İşlət",
            ["Apply"] = "Tətbiq et",
            ["Install"] = "Quraşdır",
            ["Later"] = "Sonra",
            ["Note"] = "Qeyd",
            ["Start"] = "Başlat",
            ["Stop"] = "Dayandır",
            ["Exit"] = "Çıx",

            // Proseslər
            ["End task"] = "Tapşırığı bitir",
            ["Restart"] = "Yenidən başlat",
            ["Run new task"] = "Yeni tapşırıq işlət",
            ["Export CSV"] = "CSV ixrac et",
            ["Copy details"] = "Təfərrüatları kopyala",
            ["Suspend"] = "Dayandır (Suspend)",
            ["Resume"] = "Davam et (Resume)",
            ["Efficiency mode"] = "Səmərəlilik rejimi",
            ["Disable efficiency mode"] = "Səmərəlilik rejimini söndür",
            ["Set priority"] = "Prioritet",
            ["Open file location"] = "Fayl yerini aç",
            ["Properties"] = "Xüsusiyyətlər",
            ["Details"] = "Təfərrüatlar",
            ["Search processes  (Ctrl+F)"] = "Prosesləri axtar  (Ctrl+F)",
            ["Filter: All"] = "Filtr: Hamısı",
            ["Filter: Apps only"] = "Filtr: Yalnız proqramlar",
            ["Filter: CPU > 1%"] = "Filtr: CPU > 1%",
            ["Filter: Memory > 100 MB"] = "Filtr: Yaddaş > 100 MB",
            ["Filter: Active network"] = "Filtr: Aktiv şəbəkə",
            ["Sort: CPU"] = "Sırala: CPU",
            ["Sort: Memory"] = "Sırala: Yaddaş",
            ["Sort: Disk"] = "Sırala: Disk",
            ["Sort: Network"] = "Sırala: Şəbəkə",
            ["Sort: GPU"] = "Sırala: GPU",
            ["Sort: Name"] = "Sırala: Ad",
            ["Sort: PID"] = "Sırala: PID",
            ["Run as administrator"] = "Administrator kimi işlət",
            ["e.g. notepad, cmd, mspaint"] = "Məs: notepad, cmd, mspaint",
            ["PID"] = "PID",
            ["Name column"] = "Ad",
            ["GPU usage is not available on this system"] = "Bu sistemdə GPU məlumatı mövcud deyil",

            // Performans
            ["% Utilization"] = "% İstifadə",
            ["Memory usage (%)"] = "Yaddaş istifadəsi (%)",
            ["Active time (%)"] = "Aktiv vaxt (%)",
            ["Transfer rate — Read + Write (MB/s)"] = "Ötürmə sürəti — Oxu + Yaz (MB/s)",
            ["Top processes"] = "Ən çox istifadə edən proseslər",
            ["Logical processors"] = "Məntiqi prosessorlar",
            ["Live"] = "Canlı",
            ["Last 10 minutes"] = "Son 10 dəqiqə",
            ["Last hour"] = "Son 1 saat",
            ["History"] = "Tarixçə",
            ["Sensors"] = "Sensorlar",
            ["Temperature / Fan / Voltage / Power"] = "Temperatur / Fan / Gərginlik / Güc",
            ["Opening hardware sensors..."] = "Avadanlıq sensorları açılır...",
            ["Your system does not support this section. The hardware or its driver does not expose temperature/fan/voltage sensors."] =
                "Sisteminiz bu bölməni dəstəkləmir. Avadanlıq və ya drayveri temperatur/fan/gərginlik sensorlarını təqdim etmir.",
            ["No sensors available. Try running the app as administrator — if it still shows nothing, your system does not support this section."] =
                "Sensor mövcud deyil. Proqramı administrator kimi işlətməyə cəhd edin — hələ də heç nə göstərmirsə, sisteminiz bu bölməni dəstəkləmir.",
            ["Utilization"] = "İstifadə",
            ["Speed"] = "Sürət",
            ["Threads"] = "Axınlar",
            ["Up time"] = "İşləmə vaxtı",
            ["Temperature"] = "Temperatur",
            ["In use"] = "İstifadədə",
            ["Available"] = "Mövcud",
            ["Total"] = "Cəmi",
            ["Page file"] = "Səhifə faylı",
            ["Dedicated memory"] = "Ayrılmış yaddaş",
            ["Active time"] = "Aktiv vaxt",
            ["Read speed"] = "Oxuma sürəti",
            ["Write speed"] = "Yazma sürəti",
            ["Video Decode"] = "Video dekodlaşdırma",
            ["Video Processing"] = "Video emalı",
            ["Full sensor data (fans, voltages, power) may need Run as administrator."] =
                "Tam sensor məlumatları (fanlar, gərginlik, güc) administrator kimi işlətməyi tələb edə bilər.",

            // Başlanğıc
            ["Programs that start automatically with Windows"] = "Windows ilə avtomatik başlayan proqramlar",
            ["Startup impact"] = "Başlanğıca təsir",
            ["Estimated startup impact"] = "Təxmini başlanğıc təsiri",
            ["High"] = "Yüksək",
            ["Medium"] = "Orta",
            ["Low"] = "Aşağı",
            ["Not measured"] = "Ölçülməyib",

            // Xidmətlər
            ["Search services...  (Ctrl+F)"] = "Xidmətləri axtar...  (Ctrl+F)",

            // Parametrlər
            ["Appearance"] = "Görünüş",
            ["Choose the app theme"] = "Proqram temasını seçin",
            ["Dark (Mica)"] = "Tünd (Mica)",
            ["Light (Mica)"] = "Açıq (Mica)",
            ["Liquid Glass (deep blur + transparency)"] = "Maye Şüşə (dərin bulanıqlıq + şəffaflıq)",
            ["Midnight (deeper dark, Mica Alt)"] = "Gecəyarısı (daha tünd, Mica Alt)",
            ["Aurora (light glass, Acrylic)"] = "Aurora (açıq şüşə, Acrylic)",
            ["OLED (pure black, no blur)"] = "OLED (xalis qara, bulanıqlıq yox)",
            ["Paper (flat light)"] = "Kağız (düz açıq)",
            ["Language"] = "Dil",
            ["Accent color"] = "Vurğu rəngi",
            ["Used for the whole app (buttons, highlights) and all live graphs"] = "Bütün proqram üçün (düymələr, vurğular) və bütün canlı qrafiklər",
            ["Custom color"] = "Xüsusi rəng",
            ["Custom color (wheel / hex)"] = "Xüsusi rəng (çarx / hex)",
            ["Fill area under the graph line"] = "Qrafik xəttinin altını doldur",
            ["Refresh rate"] = "Yeniləmə sürəti",
            ["Graphs update every {0} ms"] = "Grafiklər hər {0} ms yenilənir",
            ["Window"] = "Pəncərə",
            ["Always on top (keep this window above all others)"] = "Həmişə üstdə (bu pəncərəni digərlərinin üstündə saxla)",
            ["System tray"] = "Sistem tepsisi",
            ["Live usage icon in the system tray — closing or minimizing hides the app to the tray"] =
                "Sistem tepsisindəki canlı istifadə işarəsi — bağlama və ya kiçiltmə proqramı tepsiyə gizlədir",
            ["Tray items — pick one or more, each gets its own live icon"] =
                "Tepsi elementləri — bir və ya daha çox seçin, hər biri öz canlı işarəsini alır",
            ["Download / Upload speeds need the app to run as administrator"] =
                "Yükləmə/Göndərmə sürətləri proqramın administrator kimi işləməsini tələb edir",
            ["Icon style"] = "İşarə stili",
            ["Colored badge"] = "Rəngli nişan",
            ["Text only (transparent, colored text)"] = "Yalnız mətn (şəffaf fon, rəngli mətn)",
            ["Mini live graph"] = "Mini canlı qrafik",
            ["Tray icon color"] = "Tepsi işarəsi rəngi",
            ["Per-icon customization — override color, style and size for each tray icon"] =
                "Hər işarə üçün fərdiləşdirmə — hər tepsi işarəsinin rəngini, stilini və ölçüsünü dəyişin",
            ["Global hotkey Ctrl + Alt + T to show / hide the window"] =
                "Pəncərəni göstərmək/gizlətmək üçün qlobal qısa yol Ctrl + Alt + T",
            ["Tray text size: {0}%"] = "Tepsi mətn ölçüsü: {0}%",
            ["Color"] = "Rəng",
            ["Global"] = "Qlobal",
            ["Style"] = "Stil",
            ["Use global style"] = "Qlobal stildən istifadə et",
            ["Text only"] = "Yalnız mətn",
            ["Custom text size"] = "Xüsusi mətn ölçüsü",
            ["{0} icon"] = "{0} işarəsi",
            ["Keyboard shortcuts"] = "Klaviatura qısa yolları",
            ["Ctrl + 1 ... 5 — switch between tabs"] = "Ctrl + 1 ... 5 — nişanlar arasında keçid",
            ["Ctrl + F — focus search (Processes / Services)"] = "Ctrl + F — axtarışa fokuslan (Proseslər / Xidmətlər)",
            ["F5 — refresh the current list"] = "F5 — cari siyahını yenilə",
            ["Delete — end the selected process"] = "Delete — seçilmiş prosesi bitir",
            ["Ctrl + Alt + T — show / hide the window from anywhere in Windows"] =
                "Ctrl + Alt + T — Windows-da istənilən yerdən pəncərəni göstər/gizlət",
            ["Updates"] = "Yeniləmələr",
            ["Check for updates"] = "Yeniləmələri yoxla",
            ["Checking for updates..."] = "Yeniləmələr yoxlanılır...",
            ["Could not reach the update server. Check your internet connection."] =
                "Yeniləmə serverinə çatmaq mümkün olmadı. İnternet bağlantınızı yoxlayın.",
            ["You have the latest version."] = "Ən son versiyaya sahibsiniz.",
            ["Downloading version {0}..."] = "{0} versiyası yüklənir...",
            ["Download failed. Try again later."] = "Yükləmə uğursuz oldu. Sonra yenidən cəhd edin.",
            ["Version {0} downloaded. Install it to update."] = "{0} versiyası yükləndi. Yeniləmək üçün quraşdırın.",
            ["Update ready"] = "Yeniləmə hazırdır",
            ["Version {0} has been downloaded. Install it now to update Pulse."] =
                "{0} versiyası yükləndi. Pulse-u yeniləmək üçün indi quraşdırın.",
            ["About"] = "Haqqında",
            ["A task manager for Windows 11, built with WinUI 3 and .NET 8."] =
                "WinUI 3 və .NET 8 ilə hazırlanmış Windows 11 üçün tapşırıq idarəçisi.",
            ["Sound"] = "Səs",
            ["Play a subtle sound when ending a task"] = "Tapşırıq bitirərkən incə bir səs çal",
            ["Usage alarms"] = "İstifadə xəbərdarlıqları",
            ["Windows notification when CPU / RAM / temperature exceeds a limit"] =
                "CPU / RAM / temperatur həddini aşdıqda Windows bildirişi",
            ["CPU alarm at {0}%"] = "{0}%-də CPU xəbərdarlığı",
            ["RAM alarm at {0}%"] = "{0}%-də RAM xəbərdarlığı",
            ["Temperature alarm at {0} °C"] = "{0} °C-də temperatur xəbərdarlığı",
            ["Desktop widget"] = "Masaüstü vidceti",
            ["Small always-on-top window with live CPU / RAM / GPU graphs"] =
                "Canlı CPU / RAM / GPU qrafiklərindən ibarət kiçik həmişə-üstdə pəncərə",
            ["Hide widget"] = "Vidceti gizlət",
            ["Show Pulse"] = "Pulse-u göstər",

            ["CPU"] = "CPU",
            ["RAM"] = "RAM",
            ["GPU"] = "GPU",
            ["Page File: {0}% used"] = "Səhifə faylı: {0}% istifadə edilib",

            ["Apps"] = "Proqramlar",
            ["Background processes"] = "Arxa plan prosesləri",
            ["Windows processes"] = "Windows prosesləri",
            ["{0} processes  •  updated {1}"] = "{0} proses  •  yeniləndi {1}",
            ["Could not end \"{0}\" — access denied. Try running the app as administrator."] =
                "\"{0}\" bitirilə bilmədi — giriş rədd edildi. Proqramı administrator kimi işlətməyə cəhd edin.",
            ["Could not end \"{0}\": {1}"] = "\"{0}\" bitirilə bilmədi: {1}",

            ["Not available"] = "Mövcud deyil",
            ["now"] = "indi",
            ["Core"] = "Nüvə",
            ["Read"] = "Oxu",
            ["Write"] = "Yaz",

            ["Running"] = "İşləyir",
            ["Stopped"] = "Dayandırılıb",
            ["StartPending"] = "Başlayır",
            ["StopPending"] = "Dayandırılır",
            ["Paused"] = "Dayandırılmış",
            ["PausePending"] = "Dayandırılmağa gözləyir",
            ["ContinuePending"] = "Davam edilir",
            ["Could not {0} \"{1}\". Most service operations require running the app as Administrator. ({2})"] =
                "\"{1}\" {0} edilə bilmədi. Xidmət əməliyyatlarının çoxu administrator kimi işlətməyi tələb edir. ({2})",

            ["For \"All users\" startup items, run the app as administrator."] =
                "«All users» elementlərini dəyişmək üçün proqramı administrator kimi işlədin.",
        };

        private static readonly Dictionary<string, string> Tr = new()
        {
            // Gezinme
            ["Overview"] = "Genel Bakış",
            ["Performance"] = "Performans",
            ["Processes"] = "İşlemler",
            ["Startup Apps"] = "Başlangıç Uygulamaları",
            ["Services"] = "Hizmetler",
            ["Settings"] = "Ayarlar",
            ["Task Manager Pro"] = "Görev Yöneticisi Pro",

            // Genel
            ["Refresh"] = "Yenile",
            ["Search"] = "Ara",
            ["Error"] = "Hata",
            ["Close"] = "Kapat",
            ["Copy"] = "Kopyala",
            ["Name"] = "Ad",
            ["Memory"] = "Bellek",
            ["Disk"] = "Disk",
            ["Network"] = "Ağ",
            ["Loading processes..."] = "İşlemler yükleniyor...",
            ["Download"] = "İndirme",
            ["Upload"] = "Yükleme",
            ["Hardware"] = "Donanım",
            ["Open"] = "Aç",
            ["Run"] = "Çalıştır",
            ["Apply"] = "Uygula",
            ["Install"] = "Yükle",
            ["Later"] = "Sonra",
            ["Note"] = "Not",
            ["Start"] = "Başlat",
            ["Stop"] = "Durdur",
            ["Exit"] = "Çıkış",

            // İşlemler
            ["End task"] = "Görevi sonlandır",
            ["Restart"] = "Yeniden başlat",
            ["Run new task"] = "Yeni görev çalıştır",
            ["Export CSV"] = "CSV'ye aktar",
            ["Copy details"] = "Ayrıntıları kopyala",
            ["Suspend"] = "Askıya al (Suspend)",
            ["Resume"] = "Devam et (Resume)",
            ["Efficiency mode"] = "Verimlilik modu",
            ["Disable efficiency mode"] = "Verimlilik modunu devre dışı bırak",
            ["Set priority"] = "Öncelik",
            ["Open file location"] = "Dosya konumunu aç",
            ["Properties"] = "Özellikler",
            ["Details"] = "Ayrıntılar",
            ["Search processes  (Ctrl+F)"] = "İşlemleri ara  (Ctrl+F)",
            ["Filter: All"] = "Filtre: Tümü",
            ["Filter: Apps only"] = "Filtre: Yalnızca uygulamalar",
            ["Filter: CPU > 1%"] = "Filtre: CPU > %1",
            ["Filter: Memory > 100 MB"] = "Filtre: Bellek > 100 MB",
            ["Filter: Active network"] = "Filtre: Aktif ağ",
            ["Sort: CPU"] = "Sırala: CPU",
            ["Sort: Memory"] = "Sırala: Bellek",
            ["Sort: Disk"] = "Sırala: Disk",
            ["Sort: Network"] = "Sırala: Ağ",
            ["Sort: GPU"] = "Sırala: GPU",
            ["Sort: Name"] = "Sırala: Ad",
            ["Sort: PID"] = "Sırala: PID",
            ["Run as administrator"] = "Yönetici olarak çalıştır",
            ["e.g. notepad, cmd, mspaint"] = "Örn: notepad, cmd, mspaint",
            ["PID"] = "PID",
            ["Name column"] = "Ad",
            ["GPU usage is not available on this system"] = "Bu sistemde GPU bilgisi mevcut değil",

            // Performans
            ["% Utilization"] = "% Kullanım",
            ["Memory usage (%)"] = "Bellek kullanımı (%)",
            ["Active time (%)"] = "Etkin süre (%)",
            ["Transfer rate — Read + Write (MB/s)"] = "Aktarım hızı — Okuma + Yazma (MB/s)",
            ["Top processes"] = "En çok kullanan işlemler",
            ["Logical processors"] = "Mantıksal işlemciler",
            ["Live"] = "Canlı",
            ["Last 10 minutes"] = "Son 10 dakika",
            ["Last hour"] = "Son 1 saat",
            ["History"] = "Geçmiş",
            ["Sensors"] = "Sensörler",
            ["Temperature / Fan / Voltage / Power"] = "Sıcaklık / Fan / Voltaj / Güç",
            ["Opening hardware sensors..."] = "Donanım sensörleri açılıyor...",
            ["Your system does not support this section. The hardware or its driver does not expose temperature/fan/voltage sensors."] =
                "Sisteminiz bu bölümü desteklemiyor. Donanım veya sürücüsü sıcaklık/fan/voltaj sensörlerini sağlamıyor.",
            ["No sensors available. Try running the app as administrator — if it still shows nothing, your system does not support this section."] =
                "Sensör bulunamadı. Uygulamayı yönetici olarak çalıştırmayı deneyin — hâlâ hiçbir şey göstermiyorsa sisteminiz bu bölümü desteklemiyor.",
            ["Utilization"] = "Kullanım",
            ["Speed"] = "Hız",
            ["Threads"] = "İş Parçacıkları",
            ["Up time"] = "Çalışma süresi",
            ["Temperature"] = "Sıcaklık",
            ["In use"] = "Kullanımda",
            ["Available"] = "Kullanılabilir",
            ["Total"] = "Toplam",
            ["Page file"] = "Sayfa dosyası",
            ["Dedicated memory"] = "Ayrılmış bellek",
            ["Active time"] = "Etkin süre",
            ["Read speed"] = "Okuma hızı",
            ["Write speed"] = "Yazma hızı",
            ["Video Decode"] = "Video kod çözme",
            ["Video Processing"] = "Video işleme",
            ["Full sensor data (fans, voltages, power) may need Run as administrator."] =
                "Tam sensör verisi (fanlar, voltajlar, güç) yönetici olarak çalıştırmayı gerektirebilir.",

            // Başlangıç
            ["Programs that start automatically with Windows"] = "Windows ile otomatik başlayan programlar",
            ["Startup impact"] = "Başlangıç etkisi",
            ["Estimated startup impact"] = "Tahmini başlangıç etkisi",
            ["High"] = "Yüksek",
            ["Medium"] = "Orta",
            ["Low"] = "Düşük",
            ["Not measured"] = "Ölçülmedi",

            // Hizmetler
            ["Search services...  (Ctrl+F)"] = "Hizmetleri ara...  (Ctrl+F)",

            // Ayarlar
            ["Appearance"] = "Görünüm",
            ["Choose the app theme"] = "Uygulama temasını seçin",
            ["Dark (Mica)"] = "Koyu (Mica)",
            ["Light (Mica)"] = "Açık (Mica)",
            ["Liquid Glass (deep blur + transparency)"] = "Sıvı Cam (derin bulanıklık + şeffaflık)",
            ["Midnight (deeper dark, Mica Alt)"] = "Gece Yarısı (daha koyu, Mica Alt)",
            ["Aurora (light glass, Acrylic)"] = "Aurora (açık cam, Acrylic)",
            ["OLED (pure black, no blur)"] = "OLED (saf siyah, bulanıklık yok)",
            ["Paper (flat light)"] = "Kağıt (düz açık)",
            ["Language"] = "Dil",
            ["Accent color"] = "Vurgu rengi",
            ["Used for the whole app (buttons, highlights) and all live graphs"] = "Tüm uygulama için (düğmeler, vurgular) ve tüm canlı grafikler",
            ["Custom color"] = "Özel renk",
            ["Custom color (wheel / hex)"] = "Özel renk (çark / hex)",
            ["Fill area under the graph line"] = "Grafik çizgisinin altını doldur",
            ["Refresh rate"] = "Yenileme hızı",
            ["Graphs update every {0} ms"] = "Grafikler her {0} ms'de güncellenir",
            ["Window"] = "Pencere",
            ["Always on top (keep this window above all others)"] = "Her zaman üstte (bu pencereyi diğerlerinin üzerinde tut)",
            ["System tray"] = "Sistem tepsisi",
            ["Live usage icon in the system tray — closing or minimizing hides the app to the tray"] =
                "Sistem tepsisindeki canlı kullanım simgesi — kapatma veya küçültme uygulamayı tepsiye gizler",
            ["Tray items — pick one or more, each gets its own live icon"] =
                "Tepsi öğeleri — bir veya daha fazla seçin, her biri kendi canlı simgesini alır",
            ["Download / Upload speeds need the app to run as administrator"] =
                "İndirme/Yükleme hızları uygulamanın yönetici olarak çalıştırılmasını gerektirir",
            ["Icon style"] = "Simge stili",
            ["Colored badge"] = "Renkli rozet",
            ["Text only (transparent, colored text)"] = "Yalnızca metin (şeffaf arka plan, renkli metin)",
            ["Mini live graph"] = "Mini canlı grafik",
            ["Tray icon color"] = "Tepsi simgesi rengi",
            ["Per-icon customization — override color, style and size for each tray icon"] =
                "Her simge için özelleştirme — her tepsi simgesinin rengini, stilini ve boyutunu değiştirin",
            ["Global hotkey Ctrl + Alt + T to show / hide the window"] =
                "Pencereyi göstermek/gizlemek için genel kısayol Ctrl + Alt + T",
            ["Tray text size: {0}%"] = "Tepsi metin boyutu: %{0}",
            ["Color"] = "Renk",
            ["Global"] = "Genel",
            ["Style"] = "Stil",
            ["Use global style"] = "Genel stili kullan",
            ["Text only"] = "Yalnızca metin",
            ["Custom text size"] = "Özel metin boyutu",
            ["{0} icon"] = "{0} simgesi",
            ["Keyboard shortcuts"] = "Klavye kısayolları",
            ["Ctrl + 1 ... 5 — switch between tabs"] = "Ctrl + 1 ... 5 — sekmeler arasında geçiş",
            ["Ctrl + F — focus search (Processes / Services)"] = "Ctrl + F — aramaya odaklan (İşlemler / Hizmetler)",
            ["F5 — refresh the current list"] = "F5 — mevcut listeyi yenile",
            ["Delete — end the selected process"] = "Delete — seçili işlemi sonlandır",
            ["Ctrl + Alt + T — show / hide the window from anywhere in Windows"] =
                "Ctrl + Alt + T — Windows'ta her yerden pencereyi göster/gizle",
            ["Updates"] = "Güncellemeler",
            ["Check for updates"] = "Güncellemeleri denetle",
            ["Checking for updates..."] = "Güncellemeler denetleniyor...",
            ["Could not reach the update server. Check your internet connection."] =
                "Güncelleme sunucusuna ulaşılamadı. İnternet bağlantınızı kontrol edin.",
            ["You have the latest version."] = "En son sürüme sahipsiniz.",
            ["Downloading version {0}..."] = "{0} sürümü indiriliyor...",
            ["Download failed. Try again later."] = "İndirme başarısız oldu. Daha sonra tekrar deneyin.",
            ["Version {0} downloaded. Install it to update."] = "{0} sürümü indirildi. Güncellemek için yükleyin.",
            ["Update ready"] = "Güncelleme hazır",
            ["Version {0} has been downloaded. Install it now to update Pulse."] =
                "{0} sürümü indirildi. Pulse'u güncellemek için şimdi yükleyin.",
            ["About"] = "Hakkında",
            ["A task manager for Windows 11, built with WinUI 3 and .NET 8."] =
                "WinUI 3 ve .NET 8 ile oluşturulmuş Windows 11 için görev yöneticisi.",
            ["Sound"] = "Ses",
            ["Play a subtle sound when ending a task"] = "Görev sonlandırılırken ince bir ses çal",
            ["Usage alarms"] = "Kullanım uyarıları",
            ["Windows notification when CPU / RAM / temperature exceeds a limit"] =
                "CPU / RAM / sıcaklık belirli bir sınırı aştığında Windows bildirimi",
            ["CPU alarm at {0}%"] = "%{0}'de CPU uyarısı",
            ["RAM alarm at {0}%"] = "%{0}'de RAM uyarısı",
            ["Temperature alarm at {0} °C"] = "{0} °C'de sıcaklık uyarısı",
            ["Desktop widget"] = "Masaüstü pencere öğesi",
            ["Small always-on-top window with live CPU / RAM / GPU graphs"] =
                "Canlı CPU / RAM / GPU grafikleriyle küçük her zaman üstte pencere",
            ["Hide widget"] = "Pencere öğesini gizle",
            ["Show Pulse"] = "Pulse'u göster",

            ["CPU"] = "CPU",
            ["RAM"] = "RAM",
            ["GPU"] = "GPU",
            ["Page File: {0}% used"] = "Sayfa Dosyası: %{0} kullanıldı",

            ["Apps"] = "Uygulamalar",
            ["Background processes"] = "Arka plan işlemleri",
            ["Windows processes"] = "Windows işlemleri",
            ["{0} processes  •  updated {1}"] = "{0} işlem  •  güncellendi {1}",
            ["Could not end \"{0}\" — access denied. Try running the app as administrator."] =
                "\"{0}\" sonlandırılamadı — erişim reddedildi. Uygulamayı yönetici olarak çalıştırmayı deneyin.",
            ["Could not end \"{0}\": {1}"] = "\"{0}\" sonlandırılamadı: {1}",

            ["Not available"] = "Kullanılamıyor",
            ["now"] = "şimdi",
            ["Core"] = "Çekirdek",
            ["Read"] = "Okuma",
            ["Write"] = "Yazma",

            ["Running"] = "Çalışıyor",
            ["Stopped"] = "Durduruldu",
            ["StartPending"] = "Başlatılıyor",
            ["StopPending"] = "Durduruluyor",
            ["Paused"] = "Duraklatıldı",
            ["PausePending"] = "Duraklatılıyor",
            ["ContinuePending"] = "Devam ettiriliyor",
            ["Could not {0} \"{1}\". Most service operations require running the app as Administrator. ({2})"] =
                "\"{1}\" {0} edilemedi. Çoğu hizmet işlemi yönetici olarak çalıştırmayı gerektirir. ({2})",

            ["For \"All users\" startup items, run the app as administrator."] =
                "«All users» öğelerini değiştirmek için uygulamayı yönetici olarak çalıştırın.",
        };

        public static string T(string en)
        {
            var dict = AppSettings.Language switch
            {
                1 => Fa,
                2 => Ru,
                3 => Az,
                4 => Tr,
                _ => null,
            };
            return dict != null && dict.TryGetValue(en, out var tr) ? tr : en;
        }
    }
}
