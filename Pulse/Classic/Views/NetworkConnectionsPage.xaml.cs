using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TaskManagerPro.Helpers;
using TaskManagerPro.Models;
using TaskManagerPro.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace TaskManagerPro.Classic.Views
{
    /// <summary>
    /// صفحه‌ی اتصال‌ها و پورت‌های شبکه (TCP/UDP + پردازه‌ی مالک)
    /// منطق در NetworkConnectionsViewModel است؛ اینجا فقط رویدادهای UI.
    /// </summary>
    public sealed partial class NetworkConnectionsPage : Page
    {
        public NetworkConnectionsViewModel ViewModel { get; }

        // ردیفی که منوی راست‌کلیک برایش باز شده
        private ConnectionRow? _menuRow;

        public NetworkConnectionsPage()
        {
            ViewModel = new NetworkConnectionsViewModel(DispatcherQueue);
            this.InitializeComponent();
            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.Error)) ShowError(ViewModel.Error);
            };
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyL10n();
            AppSettings.LanguageChanged += OnLanguageChanged;
            ViewModel.Start();
        }

        // تایمر فقط وقتی صفحه دیده می‌شود کار کند
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Stop();
            AppSettings.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            ApplyL10n();
            ViewModel.RefreshTexts();
        }

        private void ApplyL10n()
        {
            PageTitle.Text = L10n.T("Network Connections");
            SearchBox.PlaceholderText = L10n.T("Search port, process or IP...  (Ctrl+F)");
            RefreshLabel.Text = L10n.T("Refresh");
            PauseLabel.Text = L10n.T("Pause");
            NoteBar.Title = L10n.T("Note");

            HdrProcess.Text = L10n.T("Process");
            HdrProto.Text = L10n.T("Protocol");
            HdrLocal.Text = L10n.T("Local address");
            HdrRemote.Text = L10n.T("Remote address");
            HdrState.Text = L10n.T("State");

            ((ComboBoxItem)FilterCombo.Items[0]).Content = L10n.T("All");
            ((ComboBoxItem)FilterCombo.Items[3]).Content = L10n.T("Listening");

            MenuEndProcess.Text = L10n.T("End process");
            MenuCopyLocal.Text = L10n.T("Copy local address");
            MenuCopyRemote.Text = L10n.T("Copy remote address");
            MenuCopyRow.Text = L10n.T("Copy details");
            MenuOpenLocation.Text = L10n.T("Open file location");
        }

        private void ShowError(string? message)
        {
            NoteBar.Message = message ?? "";
            NoteBar.IsOpen = !string.IsNullOrEmpty(message);
        }

        // ---- منوی راست‌کلیک ----

        private void RowMenu_Opening(object sender, object e)
        {
            var flyout = (MenuFlyout)sender;
            _menuRow = (flyout.Target as FrameworkElement)?.DataContext as ConnectionRow;
            if (_menuRow != null) ConnList.SelectedItem = _menuRow;

            bool system = _menuRow == null || _menuRow.Pid <= 4;
            MenuEndProcess.IsEnabled = !system;
            MenuOpenLocation.IsEnabled = !string.IsNullOrEmpty(_menuRow?.ProcessPath);
            MenuCopyRemote.IsEnabled = _menuRow?.RemotePort > 0;
        }

        private async void EndProcess_Click(object sender, RoutedEventArgs e)
        {
            var row = _menuRow;
            if (row == null || row.Pid <= 4) return;

            string title = $"{row.ProcessName} (PID {row.Pid})";
            if (!await KillDialog.ConfirmAsync(XamlRoot, title,
                    L10n.T("Ending this process will close all of its connections and any unsaved data may be lost."),
                    L10n.T("End process")))
                return;

            if (await KillDialog.EndProcessesAsync(XamlRoot, new[] { row.Pid }, title))
            {
                ViewModel.RemoveByPid(row.Pid);
                await ViewModel.RefreshAsync();
            }
        }

        private void CopyLocal_Click(object sender, RoutedEventArgs e) => Copy(_menuRow?.LocalEndpoint);
        private void CopyRemote_Click(object sender, RoutedEventArgs e) => Copy(_menuRow?.RemoteEndpoint);

        private void CopyRow_Click(object sender, RoutedEventArgs e)
        {
            var r = _menuRow;
            if (r == null) return;
            Copy($"{r.Protocol}\t{r.LocalEndpoint}\t{r.RemoteEndpoint}\t{r.State}\t{r.ProcessName}\t{r.Pid}\t{r.ProcessPath}");
        }

        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            var path = _menuRow?.ProcessPath;
            if (string.IsNullOrEmpty(path)) return;
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private static void Copy(string? text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }

        // ---- دکمه‌ها و هات‌کی‌ها ----

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshAsync();

        private void FindAccel_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            SearchBox.Focus(FocusState.Programmatic);
            args.Handled = true;
        }

        private async void RefreshAccel_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            await ViewModel.RefreshAsync();
        }
    }
}
