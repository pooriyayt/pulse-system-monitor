using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TaskManagerPro.Helpers;
using TaskManagerPro.Models;
using TaskManagerPro.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace TaskManagerPro.Views
{
    /// <summary>
    /// «چه چیزی این فایل را قفل کرده؟» — پیدا کردن و بستن پردازه‌هایی که فایل/پوشه را باز نگه داشته‌اند
    /// </summary>
    public sealed partial class FileUnlockerPage : Page
    {
        private string? _path;
        private List<LockerRow> _rows = new();
        private bool _busy;

        public FileUnlockerPage()
        {
            this.InitializeComponent();
            Loaded += (_, _) =>
            {
                ApplyL10n();
                AppSettings.LanguageChanged += ApplyL10n;
            };
            Unloaded += (_, _) => AppSettings.LanguageChanged -= ApplyL10n;
        }

        private void ApplyL10n()
        {
            PageTitle.Text = L10n.T("File Unlocker");
            DropHint.Text = L10n.T("Drag a file or folder here to see which processes are locking it");
            BrowseFileLabel.Text = L10n.T("Browse file");
            BrowseFolderLabel.Text = L10n.T("Browse folder");
            PathBox.PlaceholderText = L10n.T("Or paste a path, e.g. C:\\Users\\me\\file.docx");
            ScanLabel.Text = L10n.T("Scan");
            UnlockAllLabel.Text = L10n.T("Unlock (end all)");
            UpdateResultHeader();
        }

        // ---- ورودی: Drag & Drop ----

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            e.AcceptedOperation = DataPackageOperation.Link;
            e.DragUIOverride.Caption = L10n.T("Scan");
            DropZone.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e) => ResetDropZone();

        private async void DropZone_Drop(object sender, DragEventArgs e)
        {
            ResetDropZone();
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var items = await e.DataView.GetStorageItemsAsync();
            var first = items.FirstOrDefault();
            if (first != null) await ScanAsync(first.Path);
        }

        private void ResetDropZone() => DropZone.ClearValue(Border.BorderBrushProperty);

        // ---- ورودی: Picker و مسیر دستی ----

        private async void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add("*");
                InitPicker(picker);
                StorageFile? file = await picker.PickSingleFileAsync();
                if (file != null) await ScanAsync(file.Path);
            }
            catch
            {
                ShowStatus(InfoBarSeverity.Warning, L10n.T("The file picker is unavailable while running as administrator. Paste the path instead."));
            }
        }

        private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FolderPicker();
                picker.FileTypeFilter.Add("*");
                InitPicker(picker);
                StorageFolder? folder = await picker.PickSingleFolderAsync();
                if (folder != null) await ScanAsync(folder.Path);
            }
            catch
            {
                ShowStatus(InfoBarSeverity.Warning, L10n.T("The file picker is unavailable while running as administrator. Paste the path instead."));
            }
        }

        private static void InitPicker(object picker)
        {
            if (App.MainAppWindow == null) return;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        private async void Scan_Click(object sender, RoutedEventArgs e) => await ScanAsync(PathBox.Text);

        private async void PathBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter) return;
            e.Handled = true;
            await ScanAsync(PathBox.Text);
        }

        // ---- اسکن ----

        private async Task ScanAsync(string? rawPath)
        {
            if (_busy) return;
            var path = rawPath?.Trim().Trim('"') ?? "";
            if (path.Length == 0) return;

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                ShowStatus(InfoBarSeverity.Error, L10n.T("File or folder not found."));
                return;
            }

            _path = path;
            PathBox.Text = path;
            SetBusy(true);
            StatusBar.IsOpen = false;

            try
            {
                var lockers = await Task.Run(() =>
                {
                    var list = FileLockService.FindLockers(path);
                    foreach (var l in list) IconCache.Preload(l.Path);
                    return list;
                });

                _rows = lockers.Select(l => new LockerRow
                {
                    Pid = l.Pid,
                    Title = string.IsNullOrEmpty(l.AppName) ? ProcessPathResolver.GetName(l.Pid, l.Path) : l.AppName,
                    Subtitle = l.IsService
                        ? $"PID {l.Pid} · {L10n.T("Service")}: {l.ServiceName}"
                        : $"PID {l.Pid}",
                    Path = l.Path,
                    IsCritical = l.IsCritical,
                    Icon = IconCache.Get(l.Path),
                    FallbackGlyph = l.IsService ? "\uE912" : "\uECAA",
                }).ToList();
                LockList.ItemsSource = _rows;

                if (_rows.Count == 0)
                {
                    ShowStatus(InfoBarSeverity.Success, L10n.T("No process is locking this item."));
                }
                else if (!AdminHelper.IsAdmin)
                {
                    ShowStatus(InfoBarSeverity.Informational,
                        L10n.T("Run Pulse as administrator to also see services and processes of other users."));
                }
            }
            catch (Exception ex)
            {
                _rows = new();
                LockList.ItemsSource = null;
                ShowStatus(InfoBarSeverity.Error, ex.Message);
            }
            finally
            {
                SetBusy(false);
                UpdateResultHeader();
            }
        }

        private void UpdateResultHeader()
        {
            ResultHeader.Visibility = _path == null ? Visibility.Collapsed : Visibility.Visible;
            ResultTitle.Text = string.Format(L10n.T("{0} locking process(es)"), _rows.Count);
            ResultPath.Text = _path ?? "";
            UnlockAllBtn.IsEnabled = !_busy && _rows.Any(r => r.CanEnd);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            Busy.IsActive = busy;
            ScanBtn.IsEnabled = !busy;
            UnlockAllBtn.IsEnabled = !busy && _rows.Any(r => r.CanEnd);
        }

        // ---- اقدام‌ها ----

        private async void EndProcess_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not LockerRow row || !row.CanEnd) return;

            string title = $"{row.Title} (PID {row.Pid})";
            if (!await KillDialog.ConfirmAsync(XamlRoot, title,
                    L10n.T("Ending this process releases the lock, but any unsaved data in it may be lost."),
                    L10n.T("End process")))
                return;

            await KillDialog.EndProcessesAsync(XamlRoot, new[] { row.Pid }, title);
            await ScanAsync(_path);
        }

        private async void UnlockAll_Click(object sender, RoutedEventArgs e)
        {
            var path = _path;
            if (path == null) return;
            var targets = _rows.Where(r => r.CanEnd).ToList();
            if (targets.Count == 0) return;

            if (!await KillDialog.ConfirmAsync(XamlRoot, L10n.T("Unlock (end all)"),
                    string.Format(L10n.T("{0} process(es) will be closed to release the lock. Unsaved data may be lost."), targets.Count),
                    L10n.T("Unlock (end all)")))
                return;

            SetBusy(true);
            try
            {
                // اول از Restart Manager بخواه برنامه‌ها را ببندد
                await Task.Run(() => FileLockService.ForceUnlock(path));
            }
            catch { /* اگر نشد، پایین‌تر یکی‌یکی بسته می‌شوند */ }
            finally
            {
                SetBusy(false);
            }

            // هر چه باقی مانده (مثلاً به‌خاطر دسترسی) مستقیم بسته شود
            var remaining = await Task.Run(() =>
            {
                try { return FileLockService.FindLockers(path).Where(l => l.Pid > 4 && !l.IsCritical).Select(l => l.Pid).ToList(); }
                catch { return new List<int>(); }
            });
            if (remaining.Count > 0)
                await KillDialog.EndProcessesAsync(XamlRoot, remaining, L10n.T("Unlock (end all)"));

            await ScanAsync(path);
        }

        private void ShowStatus(InfoBarSeverity severity, string message)
        {
            StatusBar.Severity = severity;
            StatusBar.Title = severity switch
            {
                InfoBarSeverity.Error => L10n.T("Error"),
                InfoBarSeverity.Success => L10n.T("Not locked"),
                _ => L10n.T("Note"),
            };
            StatusBar.Message = message;
            StatusBar.IsOpen = true;
        }
    }
}
