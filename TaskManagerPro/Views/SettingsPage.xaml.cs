using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TaskManagerPro.Helpers;

namespace TaskManagerPro.Views
{
    /// <summary>
    /// صفحه‌ی تنظیمات: تم، رنگ گراف‌ها، سرعت رفرش، پنجره، System Tray و هات‌کی
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        // وقتی در حال پر کردن مقدارهای اولیه هستیم، رویدادها نباید چیزی را ذخیره کنند
        private bool _initializing = true;

        public SettingsPage()
        {
            this.InitializeComponent();
            Loaded += OnLoaded;
        }

        // ---------- آپدیت ----------

        private bool _updating;

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_updating) return;
            _updating = true;
            CheckUpdateBtn.IsEnabled = false;
            UpdateRing.IsActive = true;
            UpdateProgress.Visibility = Visibility.Collapsed;
            UpdateStatus.Text = L10n.T("Checking for updates...");

            try
            {
                var info = await UpdateChecker.CheckAsync();

                if (info == null)
                {
                    UpdateStatus.Text = L10n.T("Could not reach the update server. Check your internet connection.");
                    return;
                }

                if (!info.UpdateAvailable)
                {
                    UpdateStatus.Text = L10n.T("You have the latest version.") + $"  (v{UpdateChecker.CurrentVersion})";
                    return;
                }

                // نسخه‌ی جدید موجود است — دانلود
                UpdateStatus.Text = string.Format(L10n.T("Downloading version {0}..."), info.LatestVersion);
                UpdateProgress.Visibility = Visibility.Visible;
                UpdateProgress.Value = 0;

                var progress = new Progress<double>(p => UpdateProgress.Value = p);
                string? path = await UpdateChecker.DownloadAsync(info, progress);

                UpdateProgress.Visibility = Visibility.Collapsed;

                if (path == null)
                {
                    UpdateStatus.Text = L10n.T("Download failed. Try again later.");
                    return;
                }

                UpdateStatus.Text = string.Format(L10n.T("Version {0} downloaded. Install it to update."), info.LatestVersion);
                await ShowInstallDialog(info.LatestVersion, path);
            }
            finally
            {
                _updating = false;
                CheckUpdateBtn.IsEnabled = true;
                UpdateRing.IsActive = false;
            }
        }

        /// <summary>دیالوگ «نسخه‌ی جدید دانلود شد — نصبش کن»</summary>
        public async System.Threading.Tasks.Task ShowInstallDialog(string version, string path)
        {
            var dlg = new ContentDialog
            {
                Title = L10n.T("Update ready"),
                Content = string.Format(L10n.T("Version {0} has been downloaded. Install it now to update Pulse."), version),
                PrimaryButtonText = L10n.T("Install"),
                CloseButtonText = L10n.T("Later"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };

            try
            {
                if (await dlg.ShowAsync() == ContentDialogResult.Primary)
                {
                    // اجرای فایل نصب — ویندوز خودش نصاب MSIX/EXE را باز می‌کند
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
            catch { }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _initializing = true;

            ThemeRadios.SelectedIndex = (int)AppSettings.Theme;
            RefreshSlider.Value = AppSettings.RefreshIntervalMs;
            FillToggle.IsOn = AppSettings.GraphFill;
            TopToggle.IsOn = AppSettings.AlwaysOnTop;

            TrayToggle.IsOn = AppSettings.TrayEnabled;
            var metrics = AppSettings.TrayMetricsList;
            TrayCpuCheck.IsChecked = metrics.Contains(0);
            TrayRamCheck.IsChecked = metrics.Contains(1);
            TrayGpuCheck.IsChecked = metrics.Contains(2);
            TrayDiskCheck.IsChecked = metrics.Contains(3);
            TrayDownCheck.IsChecked = metrics.Contains(4);
            TrayUpCheck.IsChecked = metrics.Contains(5);
            TrayStyleCombo.SelectedIndex = AppSettings.TrayStyle;
            TraySizeSlider.Value = AppSettings.TrayTextScale;
            UpdateTraySizeLabel();
            HotkeyToggle.IsOn = AppSettings.HotkeyEnabled;

            LangCombo.SelectedIndex = AppSettings.Language;
            AlarmToggle.IsOn = AppSettings.AlarmEnabled;
            AlarmCpuSlider.Value = AppSettings.AlarmCpuLimit;
            AlarmRamSlider.Value = AppSettings.AlarmRamLimit;
            AlarmTempSlider.Value = AppSettings.AlarmTempLimit;
            UpdateAlarmLabels();
            WidgetToggle.IsOn = AppSettings.WidgetEnabled;
            SoundToggle.IsOn = AppSettings.EndTaskSound;

            AccentPicker.Color = ColorUtil.FromHex(AppSettings.AccentColor);
            TrayPicker.Color = ColorUtil.FromHex(AppSettings.TrayColor);

            UpdateRefreshLabel();
            HighlightSwatch(AccentPanel, AppSettings.AccentColor);
            HighlightSwatch(TrayColorPanel, AppSettings.TrayColor);
            BuildPerIconPanel();
            ApplyL10n();

            _initializing = false;
        }

        // ---- ظاهر ----

        private void Theme_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing || ThemeRadios.SelectedIndex < 0) return;
            AppSettings.Theme = (AppTheme)ThemeRadios.SelectedIndex;
            if (App.MainAppWindow != null) ThemeManager.Apply(App.MainAppWindow);
        }

        private void Accent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string hex) return;
            AppSettings.AccentColor = hex;
            HighlightSwatch(AccentPanel, hex);

            // رنگ اکسنت کل برنامه هم همین لحظه عوض می‌شود
            if (App.MainAppWindow != null) ThemeManager.Apply(App.MainAppWindow);
        }

        private void Fill_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            AppSettings.GraphFill = FillToggle.IsOn;
        }

        // ---- سرعت رفرش ----

        private void Refresh_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing) return;
            AppSettings.RefreshIntervalMs = (int)RefreshSlider.Value;
            UpdateRefreshLabel();
        }

        private void UpdateRefreshLabel()
        {
            RefreshLabel.Text = string.Format(L10n.T("Graphs update every {0} ms"), (int)RefreshSlider.Value);
        }

        // ---- پنجره ----

        private void Top_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            AppSettings.AlwaysOnTop = TopToggle.IsOn;
            if (App.MainAppWindow != null) ThemeManager.ApplyAlwaysOnTop(App.MainAppWindow);
        }

        // ---- System Tray ----

        private void Tray_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            AppSettings.TrayEnabled = TrayToggle.IsOn;
        }

        private void TrayItems_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            var ids = new System.Collections.Generic.List<int>();
            if (TrayCpuCheck.IsChecked == true) ids.Add(0);
            if (TrayRamCheck.IsChecked == true) ids.Add(1);
            if (TrayGpuCheck.IsChecked == true) ids.Add(2);
            if (TrayDiskCheck.IsChecked == true) ids.Add(3);
            if (TrayDownCheck.IsChecked == true) ids.Add(4);
            if (TrayUpCheck.IsChecked == true) ids.Add(5);
            AppSettings.TrayMetricsCsv = ids.Count > 0 ? string.Join(",", ids) : "0";
            BuildPerIconPanel();
        }

        private void TrayStyle_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing || TrayStyleCombo.SelectedIndex < 0) return;
            AppSettings.TrayStyle = TrayStyleCombo.SelectedIndex;
        }

        private void TraySize_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing) return;
            AppSettings.TrayTextScale = (int)TraySizeSlider.Value;
            UpdateTraySizeLabel();
        }

        private void UpdateTraySizeLabel()
        {
            TraySizeLabel.Text = string.Format(L10n.T("Tray text size: {0}%"), (int)TraySizeSlider.Value);
        }

        private void TrayColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string hex) return;
            AppSettings.TrayColor = hex;
            HighlightSwatch(TrayColorPanel, hex);
        }

        private void Hotkey_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            AppSettings.HotkeyEnabled = HotkeyToggle.IsOn;
        }

        // ---- رنگ دلخواه (چرخ رنگ / هگز) ----

        private void AccentCustom_Apply(object sender, RoutedEventArgs e)
        {
            string hex = ColorUtil.ToHex(AccentPicker.Color);
            AppSettings.AccentColor = hex;
            HighlightSwatch(AccentPanel, hex);
            if (App.MainAppWindow != null) ThemeManager.Apply(App.MainAppWindow);
        }

        private void TrayCustom_Apply(object sender, RoutedEventArgs e)
        {
            string hex = ColorUtil.ToHex(TrayPicker.Color);
            AppSettings.TrayColor = hex;
            HighlightSwatch(TrayColorPanel, hex);
        }

        // ---- زبان ----

        private void Lang_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing || LangCombo.SelectedIndex < 0) return;
            AppSettings.Language = LangCombo.SelectedIndex;
        }

        // ---- آلارم مصرف ----

        private void Alarm_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            AppSettings.AlarmEnabled = AlarmToggle.IsOn;
        }

        private void AlarmCpu_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing) return;
            AppSettings.AlarmCpuLimit = (int)AlarmCpuSlider.Value;
            UpdateAlarmLabels();
        }

        private void AlarmRam_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing) return;
            AppSettings.AlarmRamLimit = (int)AlarmRamSlider.Value;
            UpdateAlarmLabels();
        }

        private void AlarmTemp_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing) return;
            AppSettings.AlarmTempLimit = (int)AlarmTempSlider.Value;
            UpdateAlarmLabels();
        }

        private void UpdateAlarmLabels()
        {
            AlarmCpuLabel.Text = string.Format(L10n.T("CPU alarm at {0}%"), (int)AlarmCpuSlider.Value);
            AlarmRamLabel.Text = string.Format(L10n.T("RAM alarm at {0}%"), (int)AlarmRamSlider.Value);
            AlarmTempLabel.Text = string.Format(L10n.T("Temperature alarm at {0} °C"), (int)AlarmTempSlider.Value);
        }

        // ---- ویجت و صدا ----

        private void Widget_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            AppSettings.WidgetEnabled = WidgetToggle.IsOn;
        }

        private void Sound_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            AppSettings.EndTaskSound = SoundToggle.IsOn;
        }

        // ---- کاستوم‌سازی جداگانه‌ی هر آیکون Tray ----

        private static readonly string[] MetricNames = { "CPU", "RAM", "GPU", "Disk", "Download", "Upload" };
        private static readonly string[] SwatchColors =
            { "#61AFFE", "#6CCB5F", "#B180F0", "#FF9F45", "#FF6B6B", "#4DD8E6", "#FF7EB6", "#E7C664" };

        /// <summary>برای هر متریک انتخاب‌شده یک Expander با رنگ/استایل/اندازه‌ی مخصوص خودش می‌سازد</summary>
        private void BuildPerIconPanel()
        {
            TrayPerIconPanel.Children.Clear();

            foreach (var m in AppSettings.TrayMetricsList)
            {
                var content = new StackPanel { Spacing = 8 };

                // --- رنگ ---
                content.Children.Add(new TextBlock { Text = L10n.T("Color"), Opacity = 0.7, FontSize = 12 });
                var colorRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                string current = AppSettings.GetTrayIconColor(m);

                var globalBtn = new Button
                {
                    Content = L10n.T("Global"),
                    Padding = new Thickness(10, 4, 10, 4),
                };
                int metric = m;
                globalBtn.Click += (s, e) => { AppSettings.SetTrayIconColor(metric, ""); BuildPerIconPanel(); };
                colorRow.Children.Add(globalBtn);

                foreach (var hex in SwatchColors)
                {
                    var b = new Button
                    {
                        Width = 24,
                        Height = 24,
                        CornerRadius = new CornerRadius(12),
                        Background = new SolidColorBrush(ColorUtil.FromHex(hex)),
                        BorderThickness = new Thickness(string.Equals(hex, current, StringComparison.OrdinalIgnoreCase) ? 2 : 0),
                        BorderBrush = new SolidColorBrush(Colors.White),
                    };
                    string h = hex;
                    b.Click += (s, e) => { AppSettings.SetTrayIconColor(metric, h); BuildPerIconPanel(); };
                    colorRow.Children.Add(b);
                }

                // رنگ کاملاً دلخواه با چرخ رنگ / هگز
                var picker = new ColorPicker
                {
                    IsColorSpectrumVisible = true,
                    IsHexInputVisible = true,
                    IsAlphaEnabled = false,
                    Color = ColorUtil.FromHex(current.Length > 0 ? current : AppSettings.TrayColor),
                };
                var apply = new Button { Content = "Apply", HorizontalAlignment = HorizontalAlignment.Right };
                var flyPanel = new StackPanel { Spacing = 8 };
                flyPanel.Children.Add(picker);
                flyPanel.Children.Add(apply);
                var customBtn = new Button
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(12),
                    Content = new FontIcon { Glyph = "", FontSize = 10 },
                    Padding = new Thickness(0),
                    Flyout = new Flyout { Content = flyPanel },
                };
                apply.Click += (s, e) => { AppSettings.SetTrayIconColor(metric, ColorUtil.ToHex(picker.Color)); BuildPerIconPanel(); };
                colorRow.Children.Add(customBtn);
                content.Children.Add(colorRow);

                // --- استایل ---
                content.Children.Add(new TextBlock { Text = L10n.T("Style"), Opacity = 0.7, FontSize = 12 });
                var styleCombo = new ComboBox { Width = 240 };
                styleCombo.Items.Add(new ComboBoxItem { Content = L10n.T("Use global style") });
                styleCombo.Items.Add(new ComboBoxItem { Content = L10n.T("Colored badge") });
                styleCombo.Items.Add(new ComboBoxItem { Content = L10n.T("Text only") });
                styleCombo.Items.Add(new ComboBoxItem { Content = L10n.T("Mini live graph") });
                styleCombo.SelectedIndex = AppSettings.GetTrayIconStyle(m) + 1;
                styleCombo.SelectionChanged += (s, e) =>
                {
                    if (styleCombo.SelectedIndex >= 0)
                        AppSettings.SetTrayIconStyle(metric, styleCombo.SelectedIndex - 1);
                };
                content.Children.Add(styleCombo);

                // --- اندازه ---
                int scale = AppSettings.GetTrayIconScale(m);
                var sizeCheck = new CheckBox { Content = L10n.T("Custom text size"), IsChecked = scale > 0, MinWidth = 0 };
                var sizeSlider = new Slider
                {
                    Minimum = 75,
                    Maximum = 150,
                    StepFrequency = 5,
                    Width = 280,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Value = scale > 0 ? scale : AppSettings.TrayTextScale,
                    IsEnabled = scale > 0,
                };
                sizeCheck.Checked += (s, e) => { sizeSlider.IsEnabled = true; AppSettings.SetTrayIconScale(metric, (int)sizeSlider.Value); };
                sizeCheck.Unchecked += (s, e) => { sizeSlider.IsEnabled = false; AppSettings.SetTrayIconScale(metric, -1); };
                sizeSlider.ValueChanged += (s, e) =>
                {
                    if (sizeCheck.IsChecked == true)
                        AppSettings.SetTrayIconScale(metric, (int)sizeSlider.Value);
                };
                content.Children.Add(sizeCheck);
                content.Children.Add(sizeSlider);

                TrayPerIconPanel.Children.Add(new Expander
                {
                    Header = string.Format(L10n.T("{0} icon"), MetricNames[m]),
                    Content = content,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                });
            }
        }

        // ---- ترجمه ----

        private void ApplyL10n()
        {
            AlarmHeader.Text = L10n.T("Usage alarms");
            WidgetHeader.Text = L10n.T("Desktop widget");
            SoundHeader.Text = L10n.T("Sound");
            LangHeader.Text = L10n.T("Language");
            SoundToggle.Header = L10n.T("Play a subtle sound when ending a task");
            UpdateHeader.Text = L10n.T("Updates");
            CheckUpdateLabel.Text = L10n.T("Check for updates");
            AlarmToggle.Header = L10n.T("Windows notification when CPU / RAM / temperature exceeds a limit");
            WidgetToggle.Header = L10n.T("Small always-on-top window with live CPU / RAM / GPU graphs");
            TopToggle.Header = L10n.T("Always on top (keep this window above all others)");
            TrayToggle.Header = L10n.T("Live usage icon in the system tray — closing or minimizing hides the app to the tray");
            PerIconHeader.Text = L10n.T("Per-icon customization — override color, style and size for each tray icon");
            HotkeyToggle.Header = L10n.T("Global hotkey Ctrl + Alt + T to show / hide the window");
            PageTitle.Text = L10n.T("Settings");
            AppearanceHeader.Text = L10n.T("Appearance");
            ThemeSubtitle.Text = L10n.T("Choose the app theme");
            AccentHeader.Text = L10n.T("Accent color");
            AccentSubtitle.Text = L10n.T("Used for the whole app (buttons, highlights) and all live graphs");
            FillToggle.Header = L10n.T("Fill area under the graph line");
            RefreshHeader.Text = L10n.T("Refresh rate");
            WindowHeader.Text = L10n.T("Window");
            TrayHeader.Text = L10n.T("System tray");
            TrayItemsLabel.Text = L10n.T("Tray items — pick one or more, each gets its own live icon");
            TrayAdminNote.Text = L10n.T("Download / Upload speeds need the app to run as administrator");
            IconStyleLabel.Text = L10n.T("Icon style");
            TrayColorLabel.Text = L10n.T("Tray icon color");
            KeyboardHeader.Text = L10n.T("Keyboard shortcuts");
            Shortcut1.Text = L10n.T("Ctrl + 1 ... 5 — switch between tabs");
            Shortcut2.Text = L10n.T("Ctrl + F — focus search (Processes / Services)");
            Shortcut3.Text = L10n.T("F5 — refresh the current list");
            Shortcut4.Text = L10n.T("Delete — end the selected process");
            Shortcut5.Text = L10n.T("Ctrl + Alt + T — show / hide the window from anywhere in Windows");
            AboutHeader.Text = L10n.T("About");
            AboutDesc.Text = L10n.T("A task manager for Windows 11, built with WinUI 3 and .NET 8.");

            // TrayStyleCombo items
            if (TrayStyleCombo.Items.Count >= 3)
            {
                ((ComboBoxItem)TrayStyleCombo.Items[0]).Content = L10n.T("Colored badge");
                ((ComboBoxItem)TrayStyleCombo.Items[1]).Content = L10n.T("Text only (transparent, colored text)");
                ((ComboBoxItem)TrayStyleCombo.Items[2]).Content = L10n.T("Mini live graph");
            }
        }

        /// <summary>دور دکمه‌ی رنگ انتخاب‌شده یک حاشیه می‌کشد تا معلوم باشد کدام انتخاب شده</summary>
        private static void HighlightSwatch(StackPanel panel, string selectedHex)
        {
            foreach (var child in panel.Children)
            {
                if (child is Button b && b.Tag is string hex)
                {
                    bool selected = string.Equals(hex, selectedHex, StringComparison.OrdinalIgnoreCase);
                    b.BorderThickness = new Thickness(selected ? 3 : 0);
                    b.BorderBrush = new SolidColorBrush(Colors.White);
                }
            }
        }
    }
}
