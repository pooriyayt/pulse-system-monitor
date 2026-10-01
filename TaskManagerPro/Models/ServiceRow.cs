namespace TaskManagerPro.Models
{
    /// <summary>
    /// یک سرویس ویندوز برای نمایش در لیست
    /// </summary>
    public class ServiceRow
    {
        /// <summary>نام سیستمی سرویس (مثلاً wuauserv)</summary>
        public string Name { get; set; } = "";

        /// <summary>نام نمایشی (مثلاً Windows Update)</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>وضعیت فعلی: Running / Stopped / ...</summary>
        public string Status { get; set; } = "";

        /// <summary>در حال اجرا؟ (برای رنگ نشانگر وضعیت)</summary>
        public bool Running { get; set; }

        public Microsoft.UI.Xaml.Media.Brush StatusBrush => new Microsoft.UI.Xaml.Media.SolidColorBrush(Running
            ? Controls.LoadPalette.Ok
            : Windows.UI.Color.FromArgb(255, 0x8A, 0x8A, 0x8A));

        public string TxtStart => TaskManagerPro.Helpers.L10n.T("Start");
        public string TxtStop => TaskManagerPro.Helpers.L10n.T("Stop");
        public string TxtRestart => TaskManagerPro.Helpers.L10n.T("Restart");
    }
}
