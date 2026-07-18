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
    }
}
