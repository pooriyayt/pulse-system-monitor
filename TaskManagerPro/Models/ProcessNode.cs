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
            set { _cpu = value; On(nameof(CpuText)); }
        }

        private double _memoryMB;
        public double MemoryMB
        {
            get => _memoryMB;
            set { _memoryMB = value; On(nameof(MemText)); }
        }

        private double _diskMBs;
        /// <summary>مصرف دیسک (خواندن + نوشتن) به مگابایت بر ثانیه</summary>
        public double DiskMBs
        {
            get => _diskMBs;
            set { _diskMBs = value; On(nameof(DiskText)); }
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
            set { _gpuPercent = value; On(nameof(GpuText)); }
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

        /// <summary>گروه‌ها با فونت ضخیم‌تر نمایش داده می‌شوند</summary>
        public FontWeight NameWeight => IsGroup ? FontWeights.SemiBold : FontWeights.Normal;

        public ObservableCollection<ProcessNode> Children { get; } = new();

        // متن‌های آماده برای نمایش در UI (برای گروه‌ها خالی)
        public string PidText => IsGroup ? "" : Pid.ToString();
        public string CpuText => IsGroup ? "" : Cpu.ToString("F1") + " %";
        public string MemText => IsGroup ? "" : MemoryMB.ToString("F0") + " MB";
        public string DiskText => IsGroup ? "" : (DiskMBs >= 0.05 ? DiskMBs.ToString("F1") + " MB/s" : "0 MB/s");
        public string NetText => IsGroup ? "" : (NetKBs < 0 ? "\uD83D\uDD12" : FormatNet(NetKBs));
        public string GpuText => IsGroup ? "" : (GpuPercent >= 0.5 ? GpuPercent.ToString("F0") + " %" : "0 %");

        private static string FormatNet(double kbs) =>
            kbs >= 1024 ? (kbs / 1024.0).ToString("F1") + " MB/s"
            : kbs >= 0.5 ? kbs.ToString("F0") + " KB/s"
            : "0 KB/s";
    }
}
