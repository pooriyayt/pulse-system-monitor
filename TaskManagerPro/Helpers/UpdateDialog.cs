using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TaskManagerPro.Controls;
using Windows.Foundation;

namespace TaskManagerPro.Helpers
{
    /// <summary>
    /// دیالوگ آپدیت مدرن: معرفی نسخه‌ی جدید ← دانلود با نوار گرادیانی، درصد و زمان باقی‌مانده ← آماده‌ی نصب.
    /// تا کاربر «دانلود» را نزند چیزی دانلود نمی‌شود.
    /// </summary>
    public static class UpdateDialog
    {
        public static async Task ShowAsync(UpdateInfo info, XamlRoot root)
        {
            var accent = ColorUtil.FromHex(AppSettings.AccentColor);
            var muted = (Brush)Application.Current.Resources["PulseMutedTextBrush"];

            // ---- سرتیتر ----
            var badge = new IconBadge { Glyph = "", Size = 64, HorizontalAlignment = HorizontalAlignment.Center };
            var title = new TextBlock
            {
                Text = string.Format(L10n.T("Pulse {0} is here"), info.LatestVersion),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                FontFamily = (FontFamily)Application.Current.Resources["DisplayFont"],
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            };

            // ---- قرص نسخه: فعلی ← جدید ----
            var versions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight,
            };
            versions.Children.Add(Chip("v" + UpdateChecker.CurrentVersion, null));
            versions.Children.Add(new FontIcon { Glyph = "", FontSize = 12, Foreground = muted, VerticalAlignment = VerticalAlignment.Center });
            versions.Children.Add(Chip("v" + info.LatestVersion, accent));

            var message = new TextBlock
            {
                Text = L10n.T("A new version of Pulse is ready to download. Your settings and history are kept."),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = muted,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 340,
            };

            // ---- نوار پیشرفت گرادیانی ----
            var fillGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            fillGrad.GradientStops.Add(new GradientStop { Color = ColorUtil.Shift(accent, -0.15), Offset = 0 });
            fillGrad.GradientStops.Add(new GradientStop { Color = ColorUtil.Shift(accent, 0.35), Offset = 1 });
            var fill = new Border { CornerRadius = new CornerRadius(5), Background = fillGrad, HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            var track = new Grid
            {
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = (Brush)Application.Current.Resources["PulseTrackBrush"],
                FlowDirection = FlowDirection.LeftToRight,
            };
            track.Children.Add(fill);

            var pctText = new TextBlock { FontWeight = FontWeights.SemiBold, FontSize = 13 };
            var etaText = new TextBlock { FontSize = 12, Foreground = muted, HorizontalAlignment = HorizontalAlignment.Right };
            var stats = new Grid();
            stats.Children.Add(pctText);
            stats.Children.Add(etaText);

            var progressPanel = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 6, 0, 0) };
            progressPanel.Children.Add(track);
            progressPanel.Children.Add(stats);

            // «چه چیزی جدید است» از متن Release گیت‌هاب
            Border? notesCard = null;
            if (info.ReleaseNotes.Length > 0)
            {
                var notes = new StackPanel { Spacing = 6 };
                notes.Children.Add(new TextBlock
                {
                    Text = L10n.T("What's new"),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13,
                });
                notes.Children.Add(new TextBlock
                {
                    Text = CleanMarkdown(info.ReleaseNotes),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12.5,
                    LineHeight = 20,
                    Foreground = muted,
                    IsTextSelectionEnabled = true,
                });
                notesCard = new Border
                {
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(14, 12, 14, 12),
                    Background = (Brush)Application.Current.Resources["PulseSurfaceBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["PulseDividerBrush"],
                    BorderThickness = new Thickness(1),
                    Child = new ScrollViewer { Content = notes, MaxHeight = 170 },
                };
            }
            if (info.SizeBytes > 0)
                versions.Children.Add(Chip($"{info.SizeBytes / 1048576.0:F0} MB", null));

            var panel = new StackPanel { Spacing = 14, Width = 380, Padding = new Thickness(0, 8, 0, 0) };
            panel.Children.Add(badge);
            panel.Children.Add(title);
            panel.Children.Add(versions);
            panel.Children.Add(message);
            if (notesCard != null) panel.Children.Add(notesCard);
            panel.Children.Add(progressPanel);

            var dlg = new ContentDialog
            {
                Content = panel,
                PrimaryButtonText = L10n.T("Download"),
                CloseButtonText = L10n.T("Later"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = root,
                FlowDirection = L10n.Direction,
                RequestedTheme = (root.Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Default,
            };

            string? downloadedPath = null;
            bool busy = false;

            void SetProgress(double p)
            {
                double w = track.ActualWidth > 0 ? track.ActualWidth : 380;
                fill.Width = Math.Max(0, Math.Min(1, p / 100.0)) * w;
                pctText.Text = $"{p:0}%";
            }

            dlg.PrimaryButtonClick += async (sender, args) =>
            {
                if (downloadedPath != null) return; // «نصب»
                args.Cancel = true;
                if (busy) return;
                busy = true;

                var deferral = args.GetDeferral();
                try
                {
                    sender.IsPrimaryButtonEnabled = false;
                    sender.CloseButtonText = L10n.T("Hide");
                    message.Text = L10n.T("Downloading update…");
                    progressPanel.Visibility = Visibility.Visible;
                    SetProgress(0);
                    etaText.Text = L10n.T("Starting…");

                    var started = DateTime.UtcNow;
                    var progress = new Progress<double>(p =>
                    {
                        SetProgress(p);
                        double secs = (DateTime.UtcNow - started).TotalSeconds;
                        if (p > 1 && secs > 0.5)
                        {
                            double left = secs * (100 - p) / p;
                            etaText.Text = left >= 60
                                ? string.Format(L10n.T("About {0} min left"), Math.Ceiling(left / 60))
                                : string.Format(L10n.T("About {0} s left"), Math.Ceiling(left));
                        }
                    });

                    string? path = await UpdateChecker.DownloadAsync(info, progress);
                    if (path == null)
                    {
                        message.Text = L10n.T("Download failed. Check your connection and try again.");
                        progressPanel.Visibility = Visibility.Collapsed;
                        sender.PrimaryButtonText = L10n.T("Try again");
                        sender.CloseButtonText = L10n.T("Later");
                        sender.IsPrimaryButtonEnabled = true;
                        return;
                    }

                    downloadedPath = path;
                    SetProgress(100);
                    etaText.Text = "";
                    badge.Glyph = ""; // تیک
                    badge.Tint = "#30D158";
                    title.Text = L10n.T("Ready to install");
                    message.Text = string.Format(L10n.T("Version {0} has been downloaded. Install it now to update Pulse."), info.LatestVersion);
                    sender.PrimaryButtonText = L10n.T("Install now");
                    sender.CloseButtonText = L10n.T("Later");
                    sender.IsPrimaryButtonEnabled = true;
                }
                catch
                {
                    sender.IsPrimaryButtonEnabled = true;
                }
                finally
                {
                    busy = false;
                    deferral.Complete();
                }
            };

            var result = await dlg.ShowAsync();
            if (result == ContentDialogResult.Primary && downloadedPath != null)
            {
                try { Process.Start(new ProcessStartInfo(downloadedPath) { UseShellExecute = true }); }
                catch { }
            }
        }

        /// <summary>تبدیل ساده‌ی Markdown به متن خوانا (سرتیترها، بولت‌ها، **ضخیم**)</summary>
        private static string CleanMarkdown(string md)
        {
            var lines = md.Replace("\r", "").Split('\n');
            var sb = new System.Text.StringBuilder();
            foreach (var raw in lines)
            {
                string l = raw.TrimEnd();
                if (l.StartsWith("#")) l = l.TrimStart('#').Trim();
                else if (l.StartsWith("- ") || l.StartsWith("* ")) l = "•  " + l.Substring(2);
                l = l.Replace("**", "").Replace("`", "");
                sb.AppendLine(l);
            }
            return sb.ToString().Trim();
        }

        private static Border Chip(string text, Windows.UI.Color? tint)
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3),
                Background = tint is Windows.UI.Color c
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(60, c.R, c.G, c.B))
                    : (Brush)Application.Current.Resources["PulseSurfaceBrush"],
                BorderBrush = (Brush)Application.Current.Resources["PulseDividerBrush"],
                BorderThickness = new Thickness(1),
            };
            b.Child = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold };
            return b;
        }
    }
}
