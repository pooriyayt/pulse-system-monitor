using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using TaskManagerPro.Helpers;
using TaskManagerPro.Views;

namespace TaskManagerPro
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // پس‌زمینه‌ی پیش‌فرض: Mica (تم نهایی را ThemeManager اعمال می‌کند)
            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();

            // نوار عنوان سفارشی
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);

            this.Title = "Pulse";

            // آیکون تسک‌بار و نوار عنوان (WinUI 3 خودش از پکیج نمی‌گیرد)
            try
            {
                this.AppWindow.SetIcon(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
            }
            catch { }

            ApplyL10n();
            AppSettings.LanguageChanged += ApplyL10n;
        }

        /// <summary>ترجمه‌ی آیتم‌های ناوبری و اعمال جهت متن</summary>
        private void ApplyL10n()
        {
            foreach (var item in NavView.MenuItems)
            {
                if (item is NavigationViewItem nvi && nvi.Tag is string tag)
                {
                    nvi.Content = tag switch
                    {
                        "overview" => L10n.T("Overview"),
                        "performance" => L10n.T("Performance"),
                        "processes" => L10n.T("Processes"),
                        "startup" => L10n.T("Startup Apps"),
                        "services" => L10n.T("Services"),
                        _ => nvi.Content,
                    };
                }
            }
            if (NavView.SettingsItem is NavigationViewItem settingsNvi)
                settingsNvi.Content = L10n.T("Settings");
            L10n.ApplyDirection(this);
        }

        // وقتی Sidebar برای اولین بار بارگذاری شد، صفحه‌ی Overview را باز کن.
        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
            ContentFrame.Navigate(typeof(OverviewPage));
        }

        // وقتی کاربر روی یکی از آیتم‌های Sidebar کلیک کرد:
        private void NavView_SelectionChanged(
            NavigationView sender,
            NavigationViewSelectionChangedEventArgs args)
        {
            var transition = new DrillInNavigationTransitionInfo();

            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage), null, transition);
                return;
            }

            var selectedItem = args.SelectedItem as NavigationViewItem;
            if (selectedItem == null) return;

            switch (selectedItem.Tag as string)
            {
                case "overview":
                    ContentFrame.Navigate(typeof(OverviewPage), null, transition);
                    break;
                case "performance":
                    ContentFrame.Navigate(typeof(PerformancePage), null, transition);
                    break;
                case "processes":
                    ContentFrame.Navigate(typeof(ProcessesPage), null, transition);
                    break;
                case "startup":
                    ContentFrame.Navigate(typeof(StartupPage), null, transition);
                    break;
                case "services":
                    ContentFrame.Navigate(typeof(ServicesPage), null, transition);
                    break;
            }
        }

        // ---- هات‌کی‌های Ctrl+1 تا Ctrl+5 برای جابه‌جایی بین تب‌ها ----

        private void SelectTab(int index)
        {
            if (index >= 0 && index < NavView.MenuItems.Count)
                NavView.SelectedItem = NavView.MenuItems[index];
        }

        private void Tab1_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { SelectTab(0); args.Handled = true; }
        private void Tab2_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { SelectTab(1); args.Handled = true; }
        private void Tab3_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { SelectTab(2); args.Handled = true; }
        private void Tab4_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { SelectTab(3); args.Handled = true; }
        private void Tab5_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { SelectTab(4); args.Handled = true; }
    }
}
