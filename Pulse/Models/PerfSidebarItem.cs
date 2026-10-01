using System.ComponentModel;

namespace TaskManagerPro.Models
{
    /// <summary>یک آیتم در سایدبار صفحه‌ی Performance (مثل سایدبار Task Manager ویندوز)</summary>
    public class PerfSidebarItem : INotifyPropertyChanged
    {
        public string Key { get; set; } = "";
        public string Glyph { get; set; } = "\uE950";

        private string _title = "";
        public string Title
        {
            get => _title;
            set
            {
                if (_title == value) return;
                _title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }

        private string _subtitle = "";
        /// <summary>مقدار زنده (مثلاً "12% • 3.4 GHz") که هر ثانیه به‌روز می‌شود</summary>
        public string Subtitle
        {
            get => _subtitle;
            set
            {
                if (_subtitle == value) return;
                _subtitle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subtitle)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
