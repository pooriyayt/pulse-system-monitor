using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskManagerPro.Helpers;
using TaskManagerPro.Models;

namespace TaskManagerPro.Views
{
    /// <summary>
    /// مدیریت برنامه‌های Startup ویندوز.
    /// لیست از همه‌ی منابع واقعی خوانده می‌شود (کلیدهای Run در هر دو نمای ۳۲/۶۴ بیتی
    /// و پوشه‌های Startup کاربر و همه‌ی کاربران) و خاموش کردن کلید، آیتم را واقعاً
    /// از مسیر اجرای بوت خارج می‌کند — جزئیات در <see cref="StartupScanner"/>.
    ///
    /// بالای صفحه هم کلید «اجرای Pulse هنگام بوت ویندوز» است.
    /// </summary>
    public sealed partial class StartupPage : Page
    {
        public ObservableCollection<StartupItem> Items { get; } = new();

        private bool _initializing;

        public StartupPage()
        {
            this.InitializeComponent();
            Loaded += async (s, e) =>
            {
                ApplyL10n();
                LoadItems();
                await LoadPulseStartupStateAsync();
            };
        }

        private void ApplyL10n()
        {
            StartupTitle.Text = L10n.T("Startup Apps");
            HdrApp.Text = L10n.T("App");
            HdrSource.Text = L10n.T("Registered in");
            HdrImpact.Text = L10n.T("Boot impact");
            HdrEnabled.Text = L10n.T("Enabled");
            RefreshBtnLabel.Text = L10n.T("Refresh");
            StartupSubtitle.Text = L10n.T("Programs that start automatically with Windows");
            PulseStartupToggle.Header = L10n.T("Start Pulse automatically when Windows starts");
            PulseStartupNote.Text = L10n.T("When the system tray is enabled, Pulse starts silently in the tray.");
        }

        // ---------- اجرای خودکار خود برنامه ----------

        private async System.Threading.Tasks.Task LoadPulseStartupStateAsync()
        {
            _initializing = true;
            PulseStartupToggle.IsOn = await StartupManager.IsEnabledAsync();
            _initializing = false;
        }

        private async void PulseStartup_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;

            bool want = PulseStartupToggle.IsOn;
            var (ok, message) = await StartupManager.SetEnabledAsync(want);

            if (!ok)
            {
                // وضعیت واقعی را برگردان تا کلید «ظاهری» نماند
                _initializing = true;
                PulseStartupToggle.IsOn = await StartupManager.IsEnabledAsync();
                _initializing = false;
                ShowError(message);
            }
            else
            {
                ErrorBar.IsOpen = false;
            }
        }

        // ---------- لیست برنامه‌های استارتاپ ویندوز ----------

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadItems();

        private void LoadItems()
        {
            Items.Clear();
            foreach (var item in StartupScanner.Scan())
                Items.Add(item);

            StartupList.ItemsSource = Items;
            _ = LoadIconsAsync();
        }

        /// <summary>استخراج آیکون‌ها در پس‌زمینه و ست کردن روی ترد UI</summary>
        private async System.Threading.Tasks.Task LoadIconsAsync()
        {
            var paths = new List<string>();
            foreach (var item in Items)
                if (item.ExePath.Length > 0 && System.IO.File.Exists(item.ExePath))
                    paths.Add(item.ExePath);

            await System.Threading.Tasks.Task.Run(() =>
            {
                foreach (var p in paths)
                    IconCache.Preload(p);
            });

            foreach (var item in Items)
                if (item.ExePath.Length > 0)
                    item.Icon = IconCache.Get(item.ExePath);
        }

        private void Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch toggle) return;
            if (toggle.DataContext is not StartupItem item) return;

            bool wantEnabled = toggle.IsOn;
            if (wantEnabled == item.Enabled) return; // تغییری رخ نداده (مثلاً هنگام ساخت لیست)

            try
            {
                StartupScanner.SetEnabled(item, wantEnabled);
                item.Enabled = wantEnabled;
                ErrorBar.IsOpen = false;
            }
            catch (Exception ex)
            {
                // برگرداندن کلید به حالت قبل
                toggle.IsOn = item.Enabled;
                ShowError($"{ex.Message} — {L10n.T("For \"All users\" startup items, run the app as administrator.")}");
            }
        }

        private void ShowError(string message)
        {
            if (message.Length == 0) return;
            ErrorBar.Message = message;
            ErrorBar.IsOpen = true;
        }
    }
}
