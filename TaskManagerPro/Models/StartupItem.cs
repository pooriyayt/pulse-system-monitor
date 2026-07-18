using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace TaskManagerPro.Models
{
    /// <summary>
    /// یک برنامه‌ی اجرای خودکار (Startup) خوانده‌شده از رجیستری ویندوز
    /// </summary>
    public class StartupItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public bool Enabled { get; set; }

        /// <summary>مسیر فایل اجرایی (برای آیکون)</summary>
        public string ExePath { get; set; } = "";

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

        /// <summary>true = برای همه‌ی کاربران (HKLM)، false = فقط کاربر فعلی (HKCU)</summary>
        public bool IsMachine { get; set; }

        public string LocationText => IsMachine ? "All users (HKLM)" : "Current user (HKCU)";

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
