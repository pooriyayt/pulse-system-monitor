using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace TaskManagerPro.Models
{
    /// <summary>
    /// یک ردیف در لیست پردازه‌ها: یا یک گروه (Apps / Background / Windows) یا یک پردازه.
    /// INotifyPropertyChanged باعث می‌شود اعداد زنده آپدیت شوند بدون اینکه ردیف‌ها از نو ساخته
    /// شوند — برای همین دیگر باز/بسته بودن گروه‌ها با هر رفرش از دست نمی‌رود.
    /// </summary>
    public class ProcessNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void On(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public int Pid { get; set; }
        /// <summary>نام خام فایل اجرایی (برای منطق برنامه؛ Name نام نمایشی است)</summary>
        public string ExeName { get; set; } = "";
        public string? Path { get; set; }

        /// <summary>true یعنی این ردیف یک گروه است (Apps / Background processes / Windows processes)</summary>
        public bool IsGroup { get; set; }

        /// <summary>عنوان پایه‌ی گروه (بدون شمارنده)</summary>
        public string BaseTitle { get; set; } = "";

        /// <summary>دسته‌بندی: 0=Apps  1=Background  2=Windows</summary>
        public int Category { get; set; }

        private string _name = "";
        public string Name
        {
            get => _name;
            set { if (_name == value) return; _name = value; On(nameof(Name)); }
        }

        private bool _isExpanded = true;
        /// <summary>باز/بسته بودن گروه — TwoWay بایند می‌شود تا با رفرش از دست نرود</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded == value) return; _isExpanded = value; On(nameof(IsExpanded)); }
        }

        private double _cpu;
        public double Cpu
        {
            get => _cpu;
            set { _cpu = value; On(nameof(CpuText)); On(nameof(CpuHeat)); }
        }

        private double _memoryMB;
        public double MemoryMB
        {
            get => _memoryMB;
            set { _memoryMB = value; On(nameof(MemText)); On(nameof(MemHeat)); }
        }

        private double _diskMBs;
        /// <summary>مصرف دیسک (خواندن + نوشتن) به مگابایت بر ثانیه</summary>
        public double DiskMBs
        {
            get => _diskMBs;
            set { _diskMBs = value; On(nameof(DiskText)); On(nameof(DiskHeat)); }
        }

        private double _netKBs = -1;
        /// <summary>مصرف شبکه به KB/s — مقدار منفی یعنی نیاز به Run as administrator (🔒)</summary>
        public double NetKBs
        {
            get => _netKBs;
            set { _netKBs = value; On(nameof(NetText)); }
        }

        private double _gpuPercent;
        /// <summary>مصرف GPU این پردازه به درصد</summary>
        public double GpuPercent
        {
            get => _gpuPercent;
            set { _gpuPercent = value; On(nameof(GpuText)); On(nameof(GpuHeat)); }
        }

        private bool _isEco;
        /// <summary>true یعنی پردازه در حالت Efficiency mode است (EcoQoS) — برگ سبز مثل Task Manager ویندوز</summary>
        public bool IsEco
        {
            get => _isEco;
            set { if (_isEco == value) return; _isEco = value; On(nameof(EcoVisibility)); }
        }

        public Visibility EcoVisibility => _isEco && !IsGroup ? Visibility.Visible : Visibility.Collapsed;

        private ImageSource? _icon;
        /// <summary>آیکون فایل اجرایی (اگر در دسترس باشد)</summary>
        public ImageSource? Icon
        {
            get => _icon;
            set
            {
                if (ReferenceEquals(_icon, value)) return;
                _icon = value;
                On(nameof(Icon));
                On(nameof(FallbackVisibility));
            }
        }

        /// <summary>آیکون جایگزین وقتی آیکون واقعی نداریم (برای گروه‌ها آیکون دسته است)</summary>
        public string FallbackGlyph { get; set; } = "\uECAA";

        public Visibility FallbackVisibility => Icon == null ? Visibility.Visible : Visibility.Collapsed;

        // ---------- نقشه‌ی حرارتی ستون‌ها (مثل Task Manager ویندوز) ----------

        private static SolidColorBrush[]? _heat;

        /// <summary>رنگ پس‌زمینه‌ی خانه بر اساس شدت مصرف (۰ تا ۱)</summary>
        private Brush? Heat(double level)
        {
            if (IsGroup || double.IsNaN(level) || level < 0.02) return null;
            _heat ??= BuildHeat();
            int i = (int)System.Math.Round(System.Math.Clamp(level, 0, 1) * (_heat.Length - 1));
            return _heat[i];
        }

        private static SolidColorBrush[] BuildHeat()
        {
            // تنت ملایم رنگ Accent (به‌جای قهوه‌ای) — هرچه مصرف بیشتر، پررنگ‌تر
            var c = Helpers.ColorUtil.FromHex(Helpers.AppSettings.AccentColor);
            var arr = new SolidColorBrush[12];
            for (int i = 0; i < arr.Length; i++)
            {
                double t = i / (double)(arr.Length - 1);
                arr[i] = new SolidColorBrush(Windows.UI.Color.FromArgb((byte)(18 + 90 * t), c.R, c.G, c.B));
            }
            return arr;
        }

        public Brush? CpuHeat => Heat(_cpu / 50.0);
        public Brush? MemHeat => Heat(_memoryMB / 2048.0);
        public Brush? DiskHeat => Heat(_diskMBs / 40.0);
        public Brush? GpuHeat => Heat(_gpuPercent / 60.0);

        /// <summary>گروه‌ها با فونت ضخیم‌تر نمایش داده می‌شوند</summary>
        public FontWeight NameWeight => IsGroup ? FontWeights.SemiBold : FontWeights.Normal;

        public ObservableCollection<ProcessNode> Children { get; } = new();

        // متن‌های آماده برای نمایش در UI (برای گروه‌ها خالی)
        public string PidText => IsGroup ? "" : Pid.ToString();
        public string CpuText => IsGroup ? "" : Cpu.ToString("F1") + " %";
        public string MemText => IsGroup ? "" : MemoryMB.ToString("F0") + " MB";
        public string DiskText => IsGroup ? "" : (DiskMBs >= 0.05 ? DiskMBs.ToString("F1") + " MB/s" : "0 MB/s");
        public string NetText => IsGroup ? "" : (NetKBs < 0 ? "\u2014" : FormatNet(NetKBs));
        public string GpuText => IsGroup ? "" : (GpuPercent >= 0.5 ? GpuPercent.ToString("F0") + " %" : "0 %");

        private static string FormatNet(double kbs) =>
            kbs >= 1024 ? (kbs / 1024.0).ToString("F1") + " MB/s"
            : kbs >= 0.5 ? kbs.ToString("F0") + " KB/s"
            : "0 KB/s";
    }
}
