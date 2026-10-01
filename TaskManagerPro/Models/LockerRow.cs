using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using TaskManagerPro.Helpers;

namespace TaskManagerPro.Models
{
    /// <summary>یک پردازه‌ی قفل‌کننده در صفحه‌ی File Unlocker</summary>
    public class LockerRow
    {
        public int Pid { get; init; }
        public string Title { get; init; } = "";
        public string Subtitle { get; init; } = "";
        public string? Path { get; init; }
        public bool IsCritical { get; init; }
        public ImageSource? Icon { get; init; }

        public string PathText => Path ?? "";
        public bool CanEnd => Pid > 4 && !IsCritical;
        public string FallbackGlyph { get; init; } = "";
        public Visibility FallbackVisibility => Icon == null ? Visibility.Visible : Visibility.Collapsed;
        public string TxtEnd => L10n.T("End process");
    }
}
