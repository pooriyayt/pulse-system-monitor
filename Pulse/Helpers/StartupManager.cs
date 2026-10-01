using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace TaskManagerPro.Helpers
{
    /// <summary>
    /// اجرای خودکار خود Pulse هنگام بوت ویندوز.
    ///
    /// چون برنامه به‌صورت بسته‌ی MSIX نصب می‌شود، روش درست همان StartupTask ویندوز است
    /// (نه نوشتن در کلید Run). ویندوز اجازه‌ی نهایی را به کاربر می‌دهد؛ اگر کاربر آن را
    /// از Task Manager خاموش کرده باشد، برنامه دیگر نمی‌تواند خودش روشنش کند و باید
    /// همین را به کاربر بگوییم.
    /// </summary>
    public static class StartupManager
    {
        /// <summary>همان TaskId که در Package.appxmanifest تعریف شده</summary>
        public const string TaskId = "PulseStartupTask";

        /// <summary>true اگر برنامه با بوت ویندوز اجرا شود</summary>
        public static async Task<bool> IsEnabledAsync()
        {
            try
            {
                var task = await StartupTask.GetAsync(TaskId);
                return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            }
            catch { return false; }
        }

        /// <summary>
        /// روشن/خاموش کردن اجرای خودکار.
        /// خروجی: (موفق بود؟، پیام برای کاربر در صورت شکست)
        /// </summary>
        public static async Task<(bool Ok, string Message)> SetEnabledAsync(bool enable)
        {
            try
            {
                var task = await StartupTask.GetAsync(TaskId);

                if (!enable)
                {
                    task.Disable();
                    return (true, "");
                }

                if (task.State == StartupTaskState.DisabledByUser)
                    return (false, L10n.T("Windows has blocked this app from starting automatically. Turn \"Pulse\" back on in Windows Settings › Apps › Startup (or in Task Manager › Startup apps)."));

                if (task.State == StartupTaskState.DisabledByPolicy)
                    return (false, L10n.T("Your system policy does not allow apps to start automatically."));

                var state = await task.RequestEnableAsync();
                bool ok = state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
                return ok
                    ? (true, "")
                    : (false, L10n.T("Windows did not allow enabling automatic startup."));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
