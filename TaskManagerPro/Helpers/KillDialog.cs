using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TaskManagerPro.Helpers
{
    /// <summary>
    /// بستن پردازه از داخل UI: اول تلاش عادی، و اگر ویندوز اجازه نداد
    /// پیشنهاد بستن با دسترسی Administrator (یک درخواست UAC).
    /// </summary>
    public static class KillDialog
    {
        /// <summary>true یعنی همه‌ی پردازه‌ها بسته شدند</summary>
        public static async Task<bool> EndProcessesAsync(XamlRoot root, IReadOnlyCollection<int> pids, string title)
        {
            var denied = new List<int>();
            bool anyFailed = false;

            foreach (var pid in pids)
            {
                switch (await ProcessKiller.TryKillAsync(pid))
                {
                    case KillOutcome.AccessDenied: denied.Add(pid); break;
                    case KillOutcome.Failed: anyFailed = true; break;
                }
            }

            if (denied.Count > 0)
            {
                if (AdminHelper.IsAdmin)
                {
                    // حتی ادمین هم به پردازه‌های محافظت‌شده دسترسی ندارد
                    await ShowMessageAsync(root, title,
                        L10n.T("Windows denied access to this process. It may be a protected system process."));
                    return false;
                }

                var dialog = new ContentDialog
                {
                    XamlRoot = root,
                    Title = title,
                    Content = new TextBlock
                    {
                        Text = L10n.T("This process is running with higher privileges. End it as administrator?"),
                        TextWrapping = TextWrapping.Wrap,
                    },
                    PrimaryButtonText = L10n.T("End as administrator"),
                    CloseButtonText = L10n.T("Cancel"),
                    DefaultButton = ContentDialogButton.Primary,
                    FlowDirection = L10n.Direction,
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;

                var outcome = await ProcessKiller.KillElevatedAsync(denied);
                if (outcome == KillOutcome.Cancelled) return false;
                if (outcome != KillOutcome.Killed) anyFailed = true;
            }

            if (anyFailed)
            {
                await ShowMessageAsync(root, title, L10n.T("Some processes could not be ended."));
                return false;
            }

            if (AppSettings.EndTaskSound) SoundHelper.PlayEndTask();
            return true;
        }

        public static async Task ShowMessageAsync(XamlRoot root, string title, string message)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = root,
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = L10n.T("Close"),
                FlowDirection = L10n.Direction,
            };
            await dialog.ShowAsync();
        }

        /// <summary>تأیید قبل از بستن</summary>
        public static async Task<bool> ConfirmAsync(XamlRoot root, string title, string message, string primary)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = root,
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = primary,
                CloseButtonText = L10n.T("Cancel"),
                DefaultButton = ContentDialogButton.Close,
                FlowDirection = L10n.Direction,
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
    }
}
