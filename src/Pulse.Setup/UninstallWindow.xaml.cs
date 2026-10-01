using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Pulse.Setup
{
    /// <summary>Graphical uninstaller (setup exe started with /uninstall).</summary>
    public partial class UninstallWindow : Window
    {
        enum Stage { Confirm, Working, Done, Error }

        Stage _stage;
        bool _busy;
        bool _removed;

        public UninstallWindow()
        {
            InitializeComponent();
            FlowDirection = Strings.Fa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            CaptionText.Text = Title = Strings.UCaption;
            TitleText.Text = Strings.UTitle;
            IntroText.Text = Strings.UText;
            Item1.Text = Strings.UItem1;
            Item2.Text = Strings.UItem2;
            Item3.Text = Strings.UItem3;
            MadeByText.Text = Strings.MadeBy;
            VersionText.Text = "v" + Installer.Version;

            SourceInitialized += (_, __) =>
            {
                if (Native.ApplyGlass(this))
                {
                    BackdropBrush.GradientStops[0].Color = Color.FromArgb(0xB8, 0x1F, 0x0D, 0x16);
                    BackdropBrush.GradientStops[1].Color = Color.FromArgb(0xC8, 0x12, 0x0A, 0x12);
                    BackdropBrush.GradientStops[2].Color = Color.FromArgb(0xD8, 0x0A, 0x07, 0x0D);
                }
            };
            Loaded += OnLoaded;
        }

        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            LogoFloat.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(4, -6, TimeSpan.FromSeconds(2.4))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                });

            Show(Stage.Confirm);

            // developer aid: /snap=<file.png> renders the window and exits
            var snap = App.Args.FirstOrDefault(a => a.StartsWith("/snap=", StringComparison.OrdinalIgnoreCase))?.Substring(6).Trim('"');
            if (snap != null)
            {
                if (App.Args.Any(a => a == "/snapdone")) Show(Stage.Done);
                if (App.Args.Any(a => a == "/snapprogress")) { Show(Stage.Working); SetProgress(0.4, Strings.UStepPackage); }
                await Task.Delay(1500);
                var rtb = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(Root);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(snap)) enc.Save(fs);
                Close();
            }
        }

        void Show(Stage s)
        {
            _stage = s;
            ConfirmPage.Visibility = s == Stage.Confirm ? Visibility.Visible : Visibility.Collapsed;
            ProgressPage.Visibility = s == Stage.Working ? Visibility.Visible : Visibility.Collapsed;
            DonePage.Visibility = s == Stage.Done || s == Stage.Error ? Visibility.Visible : Visibility.Collapsed;
            PrimaryButton.Visibility = SecondaryButton.Visibility = Visibility.Visible;
            PrimaryButton.Style = (Style)FindResource("DangerButton");

            switch (s)
            {
                case Stage.Confirm:
                    PrimaryButton.Content = Strings.UButton;
                    SecondaryButton.Content = Strings.Cancel;
                    break;
                case Stage.Working:
                    PrimaryButton.Visibility = SecondaryButton.Visibility = Visibility.Collapsed;
                    break;
                case Stage.Done:
                    DoneTitle.Text = Strings.UDoneTitle;
                    DoneText.Text = Strings.UDoneText;
                    PrimaryButton.Style = (Style)FindResource("PrimaryButton");
                    PrimaryButton.Content = Strings.Close;
                    SecondaryButton.Visibility = Visibility.Collapsed;
                    AnimateDone(true);
                    break;
                case Stage.Error:
                    DoneTitle.Text = Strings.ErrorTitle;
                    PrimaryButton.Content = Strings.Close;
                    SecondaryButton.Visibility = Visibility.Collapsed;
                    AnimateDone(false);
                    break;
            }
        }

        void AnimateDone(bool ok)
        {
            var c = ok ? Color.FromRgb(0x34, 0xD3, 0x99) : Color.FromRgb(0xFF, 0x45, 0x3A);
            DoneCircle.Stroke = new SolidColorBrush(c);
            DoneCircle.Fill = new SolidColorBrush(Color.FromArgb(0x26, c.R, c.G, c.B));
            DoneMark.Stroke = new SolidColorBrush(c);
            DoneMark.Data = Geometry.Parse(ok ? "M30,57 L48,74 L82,38" : "M36,36 L74,74 M74,36 L36,74");
            var grow = new DoubleAnimation(0.4, 1, TimeSpan.FromMilliseconds(520)) { EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut } };
            var st = new ScaleTransform();
            DoneCircle.RenderTransformOrigin = new Point(0.5, 0.5);
            DoneCircle.RenderTransform = st;
            st.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }

        void SetProgress(double p, string status)
        {
            Dispatcher.Invoke(() =>
            {
                p = Math.Max(0, Math.Min(1, p));
                var track = ((FrameworkElement)Bar.Parent).ActualWidth;
                Bar.BeginAnimation(WidthProperty, new DoubleAnimation(track * p, TimeSpan.FromMilliseconds(180)));
                PercentText.Text = (int)Math.Round(p * 100) + "%";
                StatusText.Text = status;
            });
        }

        async Task RunUninstall()
        {
            if (_busy) return;
            _busy = true;
            ProgressTitle.Text = Strings.Uninstalling;
            Show(Stage.Working);
            try
            {
                await Task.Run(() => Uninstaller.Uninstall(SetProgress));
                await Task.Delay(1500); // let the "finalizing" message be seen
                _removed = true;
                _busy = false;
                Show(Stage.Done);
            }
            catch (Exception ex)
            {
                _busy = false;
                Installer.Log("FAILED: " + ex.Message);
                DoneText.Text = ex.Message;
                Show(Stage.Error);
            }
        }

        async void Primary_Click(object sender, RoutedEventArgs e)
        {
            if (_stage == Stage.Confirm) await RunUninstall();
            else Close();
        }

        void Secondary_Click(object sender, RoutedEventArgs e) => Close();

        void Site_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://pouriyaparniyan.ir") { UseShellExecute = true }); } catch { }
        }

        void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        void Close_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_busy) e.Cancel = true;
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_removed) Uninstaller.ScheduleSelfDelete();
            base.OnClosed(e);
        }
    }
}
