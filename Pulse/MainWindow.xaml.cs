using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using TaskManagerPro.Controls;
using TaskManagerPro.Helpers;
using TaskManagerPro.Monitoring;
using TaskManagerPro.Views;
using Windows.Foundation;

namespace TaskManagerPro
{
    /// <summary>یک گزینه در جستجوی سراسری (Ctrl+K)</summary>
    public sealed class PaletteItem
    {
        public string Glyph { get; init; } = "";
        public string Title { get; init; } = "";
        public string Hint { get; init; } = "";
        public string Shortcut { get; init; } = "";
        internal Action? Run { get; init; }
        public override string ToString() => Title;
    }

    public sealed partial class MainWindow : Window
    {
        /// <summary>بخش‌های سایدبار: (سرتیتر گروه یا null، Tag، آیکون، عنوان انگلیسی)</summary>
        private static readonly (string? Section, string Tag, string Glyph, string Title)[] Sections =
        {
            ("Monitor", "overview", "", "Overview"),
            (null, "performance", "", "Performance"),
            (null, "processes", "", "Processes"),
            ("Manage", "startup", "", "Startup Apps"),
            (null, "services", "", "Services"),
            ("Tools", "memopt", "", "Memory Optimizer"),
            (null, "network", "", "Network Connections"),
            (null, "unlocker", "", "File Unlocker"),
        };

        private static readonly Dictionary<string, Type> Pages = new()
        {
            ["overview"] = typeof(OverviewPage),
            ["performance"] = typeof(PerformancePage),
            ["processes"] = typeof(ProcessesPage),
            ["startup"] = typeof(StartupPage),
            ["services"] = typeof(ServicesPage),
            ["memopt"] = typeof(MemoryOptimizerPage),
            ["network"] = typeof(NetworkConnectionsPage),
            ["unlocker"] = typeof(FileUnlockerPage),
            ["settings"] = typeof(SettingsPage),
        };

        private readonly List<RadioButton> _navItems = new();
        private readonly List<TextBlock> _sectionHeaders = new();
        private bool _sidebarVisible = true;
        private bool _userHidSidebar;

        public MainWindow()
        {
            this.InitializeComponent();

            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();

            // نوار عنوان یکپارچه با محتوا (مثل پنجره‌های macOS)
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            try { AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall; } catch { }

            this.Title = "Pulse";
            try
            {
                this.AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
            }
            catch { }

            try
            {
                var v = typeof(App).Assembly.GetName().Version;
                if (v != null) VersionText.Text = $"Version {v.Major}.{v.Minor}";
            }
            catch { }

            BuildSidebar();
            ApplyL10n();
            ApplyAccentVisuals();
            AppSettings.LanguageChanged += ApplyL10n;
            AppSettings.AppearanceChanged += ApplyAccentVisuals;

            HistoryStore.Sampled += OnSampled;
            Closed += (_, _) => HistoryStore.Sampled -= OnSampled;

            Toolbar.SizeChanged += (_, _) => UpdateTitleBarRegions();
            RootGrid.SizeChanged += (_, e) => AutoSidebar(e.NewSize.Width);
            RootGrid.Loaded += (_, _) =>
            {
                UpdateTitleBarRegions();
                _navItems[0].IsChecked = true;

                // اولین اجرای طراحی جدید: خوش‌آمدگویی + پیشنهاد تور
                if (!AppSettings.TourSeen)
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(900);
                        await ShowWelcomeAsync();
                    });
            };
        }

        // ---------------- سایدبار ----------------

        private void BuildSidebar()
        {
            foreach (var s in Sections)
            {
                if (s.Section != null)
                {
                    var h = new TextBlock
                    {
                        Tag = s.Section,
                        FontSize = 11,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = (Brush)Application.Current.Resources["PulseFaintTextBrush"],
                        Margin = new Thickness(10, NavPanel.Children.Count == 0 ? 4 : 16, 0, 6),
                    };
                    _sectionHeaders.Add(h);
                    NavPanel.Children.Add(h);
                }

                var rb = new RadioButton
                {
                    GroupName = "Nav",
                    Tag = s.Tag,
                    Style = (Style)Application.Current.Resources["SidebarItemStyle"],
                    Content = MakeItemContent(s.Glyph, s.Title),
                };
                rb.Resources["PulseSelectionBrush"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                rb.Checked += NavItem_Checked;
                _navItems.Add(rb);
                NavPanel.Children.Add(rb);
            }

            SettingsItem.Content = MakeItemContent("", "Settings");
        }

        private static StackPanel MakeItemContent(string glyph, string title)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            sp.Children.Add(new IconBadge { Glyph = glyph, Size = 24, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = title, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center });
            return sp;
        }

        private static void SetItemTitle(RadioButton rb, string title)
        {
            if (rb.Content is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is TextBlock tb)
                tb.Text = title;
        }

        private void NavItem_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton { Tag: string tag } rb || !Pages.TryGetValue(tag, out var page)) return;
            MoveDroplet(rb);
            if (ContentFrame.CurrentSourcePageType == page) return;
            ContentFrame.Navigate(page, null, new SuppressNavigationTransitionInfo());
            AnimatePageIn();
        }

        // ---------------- انیمیشن‌ها ----------------

        private Compositor Comp => ElementCompositionPreview.GetElementVisual(RootGrid).Compositor;
        private bool _dropletPlaced;

        /// <summary>
        /// قطره‌ی شیشه‌ای انتخاب با حرکت فنری به آیتم جدید می‌رود و در مسیر کمی کشیده
        /// و دوباره جمع می‌شود (حس قطره‌ی آب).
        /// </summary>
        private void MoveDroplet(RadioButton target)
        {
            if (target == SettingsItem)
            {
                FadeDroplet(0);
                return;
            }
            if (NavHost.ActualHeight <= 0)
            {
                // هنوز چیده نشده — بعد از اولین چیدمان
                DispatcherQueue.TryEnqueue(() => MoveDroplet(target));
                return;
            }

            var pos = target.TransformToVisual(NavHost).TransformPoint(new Point(0, 0));
            var v = ElementCompositionPreview.GetElementVisual(Droplet);
            v.CenterPoint = new Vector3((float)(Droplet.ActualWidth / 2), 19f, 0);
            var to = new Vector3(0, (float)pos.Y, 0);

            if (!_dropletPlaced)
            {
                v.Offset = to;
                _dropletPlaced = true;
                FadeDroplet(1);
                return;
            }

            float distance = Math.Abs(to.Y - v.Offset.Y);
            float dur = Math.Clamp(380 + distance * 0.6f, 380, 620);

            // حرکت سیال و بدون لرزش اضافه (مثل کپسول تب‌بار iOS)
            var spring = Comp.CreateSpringVector3Animation();
            spring.FinalValue = to;
            spring.DampingRatio = 0.78f;
            spring.Period = TimeSpan.FromMilliseconds(62);
            v.StartAnimation("Offset", spring);

            // «بلند شدن» شیشه مثل لنز هنگام حرکت: کمی بزرگ و کشیده در جهت حرکت، بعد نرم می‌نشیند
            float lift = Math.Min(distance / 300f, 0.22f);
            var ease = Comp.CreateCubicBezierEasingFunction(new Vector2(0.25f, 0.1f), new Vector2(0.25f, 1f));
            var settle = Comp.CreateCubicBezierEasingFunction(new Vector2(0.3f, 0f), new Vector2(0.2f, 1f));
            var scale = Comp.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0f, Vector3.One);
            scale.InsertKeyFrame(0.35f, new Vector3(1.035f, 1.06f + lift, 1f), ease);
            scale.InsertKeyFrame(1f, Vector3.One, settle);
            scale.Duration = TimeSpan.FromMilliseconds(dur);
            v.StartAnimation("Scale", scale);

            // لکه‌ی نور براق: هنگام حرکت روشن‌تر و کمی می‌لغزد، بعد آرام می‌شود
            var sv = ElementCompositionPreview.GetElementVisual(DropletSpecular);
            var so = Comp.CreateScalarKeyFrameAnimation();
            so.InsertKeyFrame(0f, 0.35f);
            so.InsertKeyFrame(0.3f, 1f, ease);
            so.InsertKeyFrame(1f, 0.35f, settle);
            so.Duration = TimeSpan.FromMilliseconds(dur);
            sv.StartAnimation("Opacity", so);
            var sx = Comp.CreateVector3KeyFrameAnimation();
            sx.InsertKeyFrame(0f, Vector3.Zero);
            sx.InsertKeyFrame(0.35f, new Vector3(40, 0, 0), ease);
            sx.InsertKeyFrame(1f, Vector3.Zero, settle);
            sx.Duration = TimeSpan.FromMilliseconds(dur);
            sv.StartAnimation("Offset", sx);

            FadeDroplet(1);
        }

        private void FadeDroplet(float to)
        {
            var v = ElementCompositionPreview.GetElementVisual(Droplet);
            Droplet.Opacity = 1;
            var a = Comp.CreateScalarKeyFrameAnimation();
            a.InsertKeyFrame(1f, to);
            a.Duration = TimeSpan.FromMilliseconds(220);
            v.StartAnimation("Opacity", a);
        }

        private void ApplyDropletColor()
        {
            // شیشه‌ی مایع بی‌رنگ است — رنگ Accent فقط روی آیکون‌ها
        }

        /// <summary>ورود صفحه: محو + بالا آمدن + بزرگ شدن ملایم (شیشه‌ای و نرم)</summary>
        private void AnimatePageIn()
        {
            if (ContentFrame.Content is not UIElement page) return;
            var v = ElementCompositionPreview.GetElementVisual(page);
            var ease = Comp.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));
            var dur = TimeSpan.FromMilliseconds(460);

            var op = Comp.CreateScalarKeyFrameAnimation();
            op.InsertKeyFrame(0f, 0f);
            op.InsertKeyFrame(1f, 1f, ease);
            op.Duration = TimeSpan.FromMilliseconds(320);

            var off = Comp.CreateVector3KeyFrameAnimation();
            off.InsertKeyFrame(0f, new Vector3(0, 26, 0));
            off.InsertKeyFrame(1f, Vector3.Zero, ease);
            off.Duration = dur;

            var sc = Comp.CreateVector3KeyFrameAnimation();
            sc.InsertKeyFrame(0f, new Vector3(0.985f, 0.985f, 1f));
            sc.InsertKeyFrame(1f, Vector3.One, ease);
            sc.Duration = dur;

            if (page is FrameworkElement fe)
                v.CenterPoint = new Vector3((float)(fe.ActualWidth / 2), 0, 0);
            v.StartAnimation("Opacity", op);
            v.StartAnimation("Offset", off);
            v.StartAnimation("Scale", sc);
        }

        private void GoTo(string tag)
        {
            var rb = tag == "settings" ? SettingsItem : _navItems.FirstOrDefault(n => (n.Tag as string) == tag);
            if (rb != null) rb.IsChecked = true;
        }

        private void SidebarToggle_Click(object sender, RoutedEventArgs e)
        {
            _userHidSidebar = _sidebarVisible;
            SetSidebar(!_sidebarVisible);
        }

        private void Sidebar_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            SidebarToggle_Click(sender, new RoutedEventArgs());
            args.Handled = true;
        }

        /// <summary>در پنجره‌ی باریک سایدبار خودکار جمع می‌شود</summary>
        private void AutoSidebar(double width)
        {
            if (_userHidSidebar) return;
            bool want = width >= 860;
            if (want != _sidebarVisible) SetSidebar(want);
        }

        private double _colFrom, _colTo;
        private DateTime _colStart;
        private bool _colAnimating;
        private const double SidebarWidth = 252;
        private const double SidebarAnimMs = 380;

        /// <summary>باز/بسته شدن سایدبار: ستون نرم جمع می‌شود و پنل شیشه‌ای کنار می‌لغزد و محو می‌شود</summary>
        private void SetSidebar(bool show)
        {
            _sidebarVisible = show;
            SidebarToggleIcon.Glyph = show ? "\uE76B" : "\uE700";
            ToolTipService.SetToolTip(SidebarToggle, show ? L10n.T("Hide sidebar (Ctrl+B)") : L10n.T("Show sidebar (Ctrl+B)"));

            if (show) Sidebar.Visibility = Visibility.Visible;

            var v = ElementCompositionPreview.GetElementVisual(Sidebar);
            var ease = Comp.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.9f), new Vector2(0.25f, 1f));
            var off = Comp.CreateVector3KeyFrameAnimation();
            off.InsertKeyFrame(1f, show ? Vector3.Zero : new Vector3(-60, 0, 0), ease);
            off.Duration = TimeSpan.FromMilliseconds(SidebarAnimMs);
            var op = Comp.CreateScalarKeyFrameAnimation();
            op.InsertKeyFrame(1f, show ? 1f : 0f, ease);
            op.Duration = TimeSpan.FromMilliseconds(show ? SidebarAnimMs : SidebarAnimMs * 0.6);
            var sc = Comp.CreateVector3KeyFrameAnimation();
            sc.InsertKeyFrame(1f, show ? Vector3.One : new Vector3(0.94f, 0.94f, 1f), ease);
            sc.Duration = TimeSpan.FromMilliseconds(SidebarAnimMs);
            v.CenterPoint = new Vector3(0, (float)(Sidebar.ActualHeight / 2), 0);
            v.StartAnimation("Offset", off);
            v.StartAnimation("Opacity", op);
            v.StartAnimation("Scale", sc);

            _colFrom = SidebarColumn.ActualWidth;
            _colTo = show ? SidebarWidth : 0;
            _colStart = DateTime.UtcNow;
            if (!_colAnimating)
            {
                _colAnimating = true;
                CompositionTarget.Rendering += AnimateColumn;
            }
        }

        private void AnimateColumn(object? sender, object e)
        {
            double t = Math.Min(1, (DateTime.UtcNow - _colStart).TotalMilliseconds / SidebarAnimMs);
            double k = 1 - Math.Pow(1 - t, 4); // ease-out quart
            SidebarColumn.Width = new GridLength(_colFrom + (_colTo - _colFrom) * k);
            Toolbar.Padding = new Thickness(12 + 4 * (1 - SidebarColumn.Width.Value / SidebarWidth), 0, 0, 0);
            if (t >= 1)
            {
                CompositionTarget.Rendering -= AnimateColumn;
                _colAnimating = false;
                if (!_sidebarVisible) Sidebar.Visibility = Visibility.Collapsed;
                UpdateTitleBarRegions();
            }
        }

        // ---------------- ظاهر ----------------

        private void ApplyAccentVisuals()
        {
            var c = ColorUtil.FromHex(AppSettings.AccentColor);
            var glow = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
            glow.GradientStops.Add(new GradientStop { Color = WithAlpha(c, 34), Offset = 0 });
            glow.GradientStops.Add(new GradientStop { Color = WithAlpha(c, 10), Offset = 0.4 });
            glow.GradientStops.Add(new GradientStop { Color = WithAlpha(c, 0), Offset = 0.85 });
            AmbientGlow.Fill = glow;
            // در تم‌های شیشه‌ای (Liquid Glass / Aurora) پس‌زمینه بی‌رنگ بماند؛ رنگ Accent فقط روی اجزا
            bool glass = AppSettings.Theme is AppTheme.LiquidGlass or AppTheme.Aurora;
            AmbientGlow.Visibility = glass ? Visibility.Collapsed : Visibility.Visible;
            ApplyDropletColor();
        }

        private static Windows.UI.Color WithAlpha(Windows.UI.Color c, byte a) =>
            Windows.UI.Color.FromArgb(a, c.R, c.G, c.B);

        /// <summary>دکمه‌ها و جستجوی داخل نوار عنوان باید کلیک بگیرند، نه درگ پنجره</summary>
        private void UpdateTitleBarRegions()
        {
            try
            {
                if (Toolbar.XamlRoot == null) return;
                double scale = Toolbar.XamlRoot.RasterizationScale;

                double inset = AppWindow.TitleBar.RightInset / scale;
                if (inset > 0) CaptionInsetColumn.Width = new GridLength(inset);

                var rects = new List<Windows.Graphics.RectInt32>();
                foreach (FrameworkElement el in new FrameworkElement[] { PaletteBox, SidebarToggle, Sidebar })
                {
                    if (el.ActualWidth <= 0 || el.Visibility != Visibility.Visible) continue;
                    var b = el.TransformToVisual(null).TransformBounds(new Rect(0, 0, el.ActualWidth, el.ActualHeight));
                    rects.Add(new Windows.Graphics.RectInt32(
                        (int)Math.Round(b.X * scale), (int)Math.Round(b.Y * scale),
                        (int)Math.Round(b.Width * scale), (int)Math.Round(b.Height * scale)));
                }
                InputNonClientPointerSource.GetForWindowId(AppWindow.Id)
                    .SetRegionRects(NonClientRegionKind.Passthrough, rects.ToArray());
            }
            catch { }
        }

        private void OnSampled(SystemSnapshot s)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                SetMini(CpuMini, CpuChipText, s.CpuTotal);
                SetMini(RamMini, RamChipText, s.MemPercent);
                if (s.GpuPercent >= 0) SetMini(GpuMini, GpuChipText, s.GpuPercent);
                else GpuChip.Visibility = Visibility.Collapsed;
            });
        }

        private static void SetMini(ProgressBar bar, TextBlock text, double pct)
        {
            bar.Value = pct;
            text.Text = $"{pct:F0}%";
            var c = LoadPalette.ColorFor(pct);
            if (bar.Foreground is not SolidColorBrush b || b.Color != c)
                bar.Foreground = new SolidColorBrush(c);
        }

        private void ApplyL10n()
        {
            int i = 0;
            foreach (var s in Sections) SetItemTitle(_navItems[i++], L10n.T(s.Title));
            SetItemTitle(SettingsItem, L10n.T("Settings"));
            foreach (var h in _sectionHeaders) h.Text = L10n.T((string)h.Tag);

            PaletteBox.PlaceholderText = L10n.T("Search") + "   Ctrl+K";
            NavMoreText.Text = L10n.T("More");
            ToolTipService.SetToolTip(NavMoreBtn, L10n.T("More"));
            AdminDot.Fill = (Brush)Application.Current.Resources[AdminHelper.IsAdmin ? "PulseOkBrush" : "PulseWarnBrush"];
            AdminTitle.Text = AdminHelper.IsAdmin ? L10n.T("Administrator") : L10n.T("Standard mode");
            L10n.ApplyDirection(this);
        }

        // ---------------- نشانگر اسکرول منو ----------------

        private void NavScroller_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e) => UpdateNavScrollHints();
        private void NavScroller_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateNavScrollHints();

        /// <summary>سایه‌ی لبه‌ها و دکمه‌ی «بیشتر» فقط وقتی که واقعاً گزینه‌ی پنهان بالا/پایین هست</summary>
        private void UpdateNavScrollHints()
        {
            double max = NavScroller.ScrollableHeight, off = NavScroller.VerticalOffset;
            bool moreBelow = max > 1 && off < max - 1;
            bool moreAbove = off > 1;
            FadeTo(NavFadeTop, moreAbove ? 1 : 0);
            FadeTo(NavFadeBottom, moreBelow ? 1 : 0);
            FadeTo(NavMoreBtn, moreBelow ? 1 : 0);
            NavMoreBtn.IsHitTestVisible = moreBelow;
        }

        private void FadeTo(UIElement el, float to)
        {
            var v = ElementCompositionPreview.GetElementVisual(el);
            if (el.Opacity != 1) el.Opacity = 1; // کنترل شفافیت با Composition
            var a = Comp.CreateScalarKeyFrameAnimation();
            a.InsertKeyFrame(1f, to);
            a.Duration = TimeSpan.FromMilliseconds(200);
            v.StartAnimation("Opacity", a);
        }

        private void NavMore_Click(object sender, RoutedEventArgs e)
        {
            double target = Math.Min(NavScroller.VerticalOffset + 120, NavScroller.ScrollableHeight);
            NavScroller.ChangeView(null, target, null, false);
        }

        // ---------------- خوش‌آمدگویی و تور ----------------

        private int _tourStep = -1;

        /// <summary>از تنظیمات صدا زده می‌شود: نمایش دوباره‌ی خوش‌آمدگویی و تور</summary>
        public void ReplayTour() => _ = ShowWelcomeAsync();

        private async System.Threading.Tasks.Task ShowWelcomeAsync()
        {
            if (RootGrid.XamlRoot == null) return;
            var muted = (Brush)Application.Current.Resources["PulseMutedTextBrush"];

            var panel = new StackPanel { Spacing = 14, Width = 400, Padding = new Thickness(0, 8, 0, 0) };
            panel.Children.Add(new IconBadge { Glyph = "\uE9D9", Size = 72, HorizontalAlignment = HorizontalAlignment.Center });
            panel.Children.Add(new TextBlock
            {
                Text = L10n.T("Welcome to the new Pulse"),
                FontSize = 24,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontFamily = (FontFamily)Application.Current.Resources["DisplayFont"],
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(new TextBlock
            {
                Text = L10n.T("Pulse has a brand-new look: a glass sidebar, live dashboards, smoother animations and a global search. Want a quick 30-second tour?"),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = muted,
            });
            panel.Children.Add(new TextBlock
            {
                Text = L10n.T("Prefer the old look? You can switch back to the classic design anytime in Settings."),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                FontSize = 12,
                Foreground = muted,
            });

            var dlg = new ContentDialog
            {
                Content = panel,
                PrimaryButtonText = L10n.T("Show me around"),
                SecondaryButtonText = L10n.T("Switch to classic"),
                CloseButtonText = L10n.T("Skip"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = RootGrid.XamlRoot,
                FlowDirection = L10n.Direction,
                RequestedTheme = RootGrid.ActualTheme,
            };

            ContentDialogResult result;
            try { result = await dlg.ShowAsync(); }
            catch { return; } // دیالوگ دیگری باز است

            AppSettings.TourSeen = true;
            if (result == ContentDialogResult.Primary)
            {
                _tourStep = -1;
                if (!_sidebarVisible) SetSidebar(true);
                TourNext();
            }
            else if (result == ContentDialogResult.Secondary)
            {
                AppSettings.UiStyle = 1;
                AppRestart.Restart();
            }
        }

        private (FrameworkElement Target, string Glyph, string Title, string Text)[] TourSteps() => new (FrameworkElement, string, string, string)[]
        {
            (NavPanel, "\uE700", L10n.T("Your sections"), L10n.T("Everything lives in this glass sidebar, grouped into Monitor, Manage and Tools. Tip: Ctrl+1 … Ctrl+8 jumps straight to a section.")),
            (PaletteBox, "\uE721", L10n.T("Search anything"), L10n.T("Press Ctrl+K to jump to any page or find a running process by name.")),
            (CpuMini, "\uE9D9", L10n.T("Live at a glance"), L10n.T("CPU, RAM and GPU usage stay visible here on every page.")),
            (SidebarToggle, "\uE76B", L10n.T("More room"), L10n.T("Hide or show the sidebar with this button or Ctrl+B.")),
            (SettingsItem, "\uE713", L10n.T("Make it yours"), L10n.T("Themes, accent color, tray icons, the desktop widget — and the switch back to the classic design — are all in Settings.")),
        };

        private void TourNext()
        {
            var steps = TourSteps();
            _tourStep++;
            if (_tourStep >= steps.Length)
            {
                TourTip.IsOpen = false;
                _tourStep = -1;
                HideSpotlight();
                return;
            }
            var st = steps[_tourStep];
            TourTip.IsOpen = false;
            MoveSpotlight(st.Target);
            TourTip.Target = st.Target;
            TourTip.Title = st.Title;
            TourTip.Subtitle = st.Text;
            TourTip.IconSource = new FontIconSource { Glyph = st.Glyph };
            TourTip.ActionButtonContent = _tourStep == steps.Length - 1
                ? L10n.T("Done")
                : $"{L10n.T("Next")}  ({_tourStep + 1}/{steps.Length})";
            TourTip.CloseButtonContent = _tourStep == steps.Length - 1 ? null : L10n.T("Skip tour");
            TourTip.IsOpen = true;
        }

        private void TourTip_Next(TeachingTip sender, object args) => TourNext();

        private void TourTip_Close(TeachingTip sender, object args)
        {
            _tourStep = -1;
            TourTip.IsOpen = false;
            HideSpotlight();
        }

        // ---- اسپات‌لایت ----

        private Rect _spotFrom, _spotTo, _spotNow;
        private DateTime _spotStart;
        private bool _spotAnimating;
        private const double SpotMs = 520;

        /// <summary>قاب روشن با حرکت نرم به بخش جدید می‌لغزد؛ بقیه‌ی صفحه تیره می‌ماند</summary>
        private void MoveSpotlight(FrameworkElement target)
        {
            var b = target.TransformToVisual(RootGrid).TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
            var to = new Rect(b.X - 6, b.Y - 6, b.Width + 12, b.Height + 12);

            var c = ColorUtil.FromHex(AppSettings.AccentColor);
            SpotRing.BorderBrush = new SolidColorBrush(ColorUtil.Shift(c, 0.3));
            SpotGlow.BorderBrush = new SolidColorBrush(c);

            if (Spotlight.Visibility != Visibility.Visible)
            {
                // شروع: از کل پنجره به سمت بخش جمع می‌شود
                Spotlight.Visibility = Visibility.Visible;
                _spotNow = new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight);
                var v = ElementCompositionPreview.GetElementVisual(Spotlight);
                var fade = Comp.CreateScalarKeyFrameAnimation();
                fade.InsertKeyFrame(0f, 0f);
                fade.InsertKeyFrame(1f, 1f);
                fade.Duration = TimeSpan.FromMilliseconds(300);
                v.StartAnimation("Opacity", fade);
                StartRingPulse();
            }
            _spotFrom = _spotNow;
            _spotTo = to;
            _spotStart = DateTime.UtcNow;
            if (!_spotAnimating)
            {
                _spotAnimating = true;
                CompositionTarget.Rendering += AnimateSpotlight;
            }
        }

        private void AnimateSpotlight(object? sender, object e)
        {
            double t = Math.Min(1, (DateTime.UtcNow - _spotStart).TotalMilliseconds / SpotMs);
            double k = 1 - Math.Pow(1 - t, 4); // ease-out
            _spotNow = new Rect(
                _spotFrom.X + (_spotTo.X - _spotFrom.X) * k,
                _spotFrom.Y + (_spotTo.Y - _spotFrom.Y) * k,
                Math.Max(0, _spotFrom.Width + (_spotTo.Width - _spotFrom.Width) * k),
                Math.Max(0, _spotFrom.Height + (_spotTo.Height - _spotFrom.Height) * k));
            DrawSpotlight(_spotNow);
            if (t >= 1)
            {
                CompositionTarget.Rendering -= AnimateSpotlight;
                _spotAnimating = false;
            }
        }

        private void DrawSpotlight(Rect r)
        {
            var g = new GeometryGroup { FillRule = FillRule.EvenOdd };
            g.Children.Add(new RectangleGeometry { Rect = new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight) });
            g.Children.Add(new RectangleGeometry { Rect = r });
            SpotDim.Data = g;

            Canvas.SetLeft(SpotRing, r.X);
            Canvas.SetTop(SpotRing, r.Y);
            SpotRing.Width = r.Width;
            SpotRing.Height = r.Height;
            Canvas.SetLeft(SpotGlow, r.X - 4);
            Canvas.SetTop(SpotGlow, r.Y - 4);
            SpotGlow.Width = r.Width + 8;
            SpotGlow.Height = r.Height + 8;
        }

        /// <summary>هاله‌ی دور قاب آرام نفس می‌کشد تا چشم کاربر به آن جلب شود</summary>
        private void StartRingPulse()
        {
            var v = ElementCompositionPreview.GetElementVisual(SpotGlow);
            var a = Comp.CreateScalarKeyFrameAnimation();
            a.InsertKeyFrame(0f, 0.15f);
            a.InsertKeyFrame(0.5f, 0.6f);
            a.InsertKeyFrame(1f, 0.15f);
            a.Duration = TimeSpan.FromMilliseconds(1600);
            a.IterationBehavior = AnimationIterationBehavior.Forever;
            v.StartAnimation("Opacity", a);
        }

        private void HideSpotlight()
        {
            if (Spotlight.Visibility != Visibility.Visible) return;
            var v = ElementCompositionPreview.GetElementVisual(Spotlight);
            var batch = Comp.CreateScopedBatch(CompositionBatchTypes.Animation);
            var fade = Comp.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(1f, 0f);
            fade.Duration = TimeSpan.FromMilliseconds(260);
            v.StartAnimation("Opacity", fade);
            batch.End();
            batch.Completed += (_, _) => DispatcherQueue.TryEnqueue(() =>
            {
                Spotlight.Visibility = Visibility.Collapsed;
                ElementCompositionPreview.GetElementVisual(SpotGlow).StopAnimation("Opacity");
            });
        }

        // ---------------- جستجوی سراسری (Ctrl+K) ----------------

        private List<PaletteItem> BuildCommands()
        {
            var list = new List<PaletteItem>();
            int n = 1;
            foreach (var s in Sections)
            {
                string tag = s.Tag;
                list.Add(new PaletteItem { Glyph = s.Glyph, Title = L10n.T(s.Title), Shortcut = $"Ctrl+{n++}", Run = () => GoTo(tag) });
            }
            list.Add(new PaletteItem { Glyph = "", Title = L10n.T("Settings"), Run = () => GoTo("settings") });
            return list;
        }

        private void Palette_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            PaletteBox.Focus(FocusState.Keyboard);
            args.Handled = true;
        }

        private void Palette_GotFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PaletteBox.Text))
            {
                PaletteBox.ItemsSource = BuildCommands();
                PaletteBox.IsSuggestionListOpen = true;
            }
        }

        private void Palette_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            string q = sender.Text.Trim();

            var list = BuildCommands()
                .Where(c => q.Length == 0 || c.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            if (q.Length > 0)
            {
                list.Add(new PaletteItem
                {
                    Glyph = "",
                    Title = string.Format(L10n.T("Find \"{0}\" in processes"), q),
                    Hint = L10n.T("Jump to Processes and filter the list"),
                    Shortcut = "Enter",
                    Run = () => SearchProcesses(q),
                });
            }
            sender.ItemsSource = list;
        }

        private void Palette_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            PaletteItem? item = args.ChosenSuggestion as PaletteItem;
            if (item == null && sender.ItemsSource is List<PaletteItem> list && list.Count > 0)
                item = list.Count == 2 ? list[0] : list[^1];
            if (item == null) return;

            sender.Text = "";
            sender.ItemsSource = null;
            item.Run?.Invoke();
            ContentFrame.Focus(FocusState.Programmatic);
        }

        private void SearchProcesses(string q)
        {
            ProcessesPage.PendingSearch = q;
            GoTo("processes");
            if (ContentFrame.Content is ProcessesPage p) p.ApplyPendingSearch();
        }

        // ---- Ctrl+1 تا Ctrl+8 ----

        private void SelectTab(int index)
        {
            if (index >= 0 && index < _navItems.Count) _navItems[index].IsChecked = true;
        }

        private void Tab1_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { SelectTab(0); args.Handled = true; }
        private void Tab2_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { SelectTab(1); args.Handled = true; }
        private void Tab3_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { SelectTab(2); args.Handled = true; }
        private void Tab4_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { SelectTab(3); args.Handled = true; }
        private void Tab5_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { SelectTab(4); args.Handled = true; }
        private void Tab6_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { SelectTab(5); args.Handled = true; }
        private void Tab7_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { SelectTab(6); args.Handled = true; }
        private void Tab8_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { SelectTab(7); args.Handled = true; }
    }
}
