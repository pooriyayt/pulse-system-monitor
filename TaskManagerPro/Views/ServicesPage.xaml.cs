using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TaskManagerPro.Helpers;
using TaskManagerPro.Models;

namespace TaskManagerPro.Views
{
    /// <summary>
    /// صفحه‌ی سرویس‌های ویندوز: لیست، جستجو و Start/Stop/Restart
    /// (بیشتر عملیات سرویس‌ها نیاز به اجرای برنامه به صورت Administrator دارد)
    /// </summary>
    public sealed partial class ServicesPage : Page
    {
        private List<ServiceRow> _all = new();

        public ServicesPage()
        {
            this.InitializeComponent();
            Loaded += async (s, e) =>
            {
                ApplyL10n();
                await LoadServicesAsync();
            };
            AppSettings.LanguageChanged += async () => { ApplyL10n(); await LoadServicesAsync(); };
        }

        private void ApplyL10n()
        {
            ServicesTitle.Text = L10n.T("Services");
            SearchBox.PlaceholderText = L10n.T("Search services...  (Ctrl+F)");
            RefreshLabel.Text = L10n.T("Refresh");
            ErrorBar.Title = L10n.T("Note");
        }

        private async Task LoadServicesAsync()
        {
            try
            {
                _all = await Task.Run(() =>
                {
                    var rows = new List<ServiceRow>();
                    foreach (var sc in ServiceController.GetServices())
                    {
                        using (sc)
                        {
                            try
                            {
                                rows.Add(new ServiceRow
                                {
                                    Name = sc.ServiceName,
                                    DisplayName = sc.DisplayName,
                                    Status = L10n.T(sc.Status.ToString()),
                                });
                            }
                            catch { }
                        }
                    }
                    return rows.OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
                });

                ApplyFilter();
            }
            catch (Exception ex)
            {
                ShowNote(ex.Message);
            }
        }

        private void ApplyFilter()
        {
            var q = SearchBox.Text?.Trim() ?? "";
            var filtered = string.IsNullOrEmpty(q)
                ? _all
                : _all.Where(r =>
                    r.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            ServicesList.ItemsSource = filtered;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadServicesAsync();

        private async void Start_Click(object sender, RoutedEventArgs e) => await DoActionAsync(sender, "start");
        private async void Stop_Click(object sender, RoutedEventArgs e) => await DoActionAsync(sender, "stop");
        private async void Restart_Click(object sender, RoutedEventArgs e) => await DoActionAsync(sender, "restart");

        private async Task DoActionAsync(object sender, string action)
        {
            if ((sender as FrameworkElement)?.DataContext is not ServiceRow row) return;
            try
            {
                await Task.Run(() =>
                {
                    using var sc = new ServiceController(row.Name);
                    var timeout = TimeSpan.FromSeconds(15);
                    switch (action)
                    {
                        case "start":
                            sc.Start();
                            sc.WaitForStatus(ServiceControllerStatus.Running, timeout);
                            break;
                        case "stop":
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                            break;
                        case "restart":
                            if (sc.Status == ServiceControllerStatus.Running)
                            {
                                sc.Stop();
                                sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                            }
                            sc.Start();
                            sc.WaitForStatus(ServiceControllerStatus.Running, timeout);
                            break;
                    }
                });

                await LoadServicesAsync();
            }
            catch (Exception ex)
            {
                var actionText = action switch { "start" => L10n.T("Start"), "stop" => L10n.T("Stop"), "restart" => L10n.T("Restart"), _ => action };
                ShowNote(string.Format(L10n.T("Could not {0} \"{1}\". Most service operations require running the app as Administrator. ({2})"), actionText, row.DisplayName, ex.Message));
            }
        }

        // ---- هات‌کی‌ها ----

        private void FindAccel_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            SearchBox.Focus(FocusState.Programmatic);
            args.Handled = true;
        }

        private async void RefreshAccel_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            await LoadServicesAsync();
        }

        private void ShowNote(string message)
        {
            ErrorBar.Message = message;
            ErrorBar.IsOpen = true;
        }
    }
}
