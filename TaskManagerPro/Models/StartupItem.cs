using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace TaskManagerPro.Models
{
    /// <summary>محل تعریف یک آیتم استارتاپ</summary>
    public enum StartupKind
    {
        /// <summary>کلید Run در رجیستری</summary>
        Registry = 0,
        /// <summary>پوشه‌ی Startup ویندوز (میانبرها)</summary>
        Folder = 1,
    }

    /// <summary>
    /// یک برنامه‌ی اجرای خودکار (Startup) — از کلیدهای Run رجیستری (هر دو نمای ۳۲/۶۴ بیتی)
    /// و از پوشه‌های Startup کاربر و همه‌ی کاربران خوانده می‌شود.
    /// </summary>
    public class StartupItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name { get; set; } = "";
        public string Command { get; set; } = "";

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
            }
        }

        /// <summary>مسیر فایل اجرایی (برای آیکون)</summary>
        public string ExePath { get; set; } = "";

        /// <summary>رجیستری یا پوشه‌ی Startup</summary>
        public StartupKind Kind { get; set; }

        /// <summary>برای آیتم‌های رجیستری: نمای ۳۲ بیتی (Wow6432Node)</summary>
        public bool Is32BitView { get; set; }

        /// <summary>برای آیتم‌های پوشه‌ای: مسیر فعلی فایل میانبر (چه فعال چه غیرفعال)</summary>
        public string FilePath { get; set; } = "";

        private ImageSource? _icon;
        /// <summary>آیکون فایل اجرایی — بعد از استخراج در پس‌زمینه ست می‌شود</summary>
        public ImageSource? Icon
        {
            get => _icon;
            set
            {
                if (ReferenceEquals(_icon, value)) return;
                _icon = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FallbackVisibility)));
            }
        }

        /// <summary>آیکون جایگزین وقتی آیکون واقعی نداریم</summary>
        public Visibility FallbackVisibility => _icon == null ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>true = برای همه‌ی کاربران (HKLM / پوشه‌ی مشترک)، false = فقط کاربر فعلی</summary>
        public bool IsMachine { get; set; }

        public string LocationText => Kind == StartupKind.Folder
            ? (IsMachine ? Helpers.L10n.T("Startup folder (all users)") : Helpers.L10n.T("Startup folder"))
            : (IsMachine ? "All users (HKLM)" : "Current user (HKCU)") + (Is32BitView ? " · 32-bit" : "");

        /// <summary>تأثیر تخمینی روی زمان بوت: 0 = نامشخص، 1 = کم، 2 = متوسط، 3 = زیاد</summary>
        public int Impact { get; set; }

        public string ImpactText => Impact switch
        {
            3 => Helpers.L10n.T("High"),
            2 => Helpers.L10n.T("Medium"),
            1 => Helpers.L10n.T("Low"),
            _ => Helpers.L10n.T("Not measured"),
        };
    }
}
