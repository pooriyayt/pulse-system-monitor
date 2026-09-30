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
    public partial class MainWindow : Window
    {
        enum Stage { Welcome, Working, Done, Error }

        Stage _stage;
        bool _busy;

        public MainWindow()
        {
            InitializeComponent();
            FlowDirection = Strings.Fa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            TaglineText.Text = Strings.Tagline;
            Feature1.Text = Strings.Feature1;
            Feature2.Text = Strings.Feature2;
            Feature3.Text = Strings.Feature3;
            DesktopBox.Content = Strings.OptDesktop;
            LaunchBox.Content = Strings.OptLaunch;
            MadeByText.Text = Strings.MadeBy;
            CaptionText.Text = Strings.Caption;
            Title = Strings.Caption;
            VersionText.Text = "v" + Installer.Version;

            SourceInitialized += (_, __) =>
            {
                // Windows 11: real acrylic behind a translucent tint; Windows 10: opaque gradient
                if (Native.ApplyGlass(this))
                {
                    BackdropBrush.GradientStops[0].Color = Color.FromArgb(0xB8, 0x0C, 0x1A, 0x22);
                    BackdropBrush.GradientStops[1].Color = Color.FromArgb(0xC8, 0x09, 0x11, 0x1A);
                    BackdropBrush.GradientStops[2].Color = Color.FromArgb(0xD8, 0x06, 0x0B, 0x12);
                }
            };
            Loaded += OnLoaded;
        }

        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            LogoFloat.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                new DoubleAnimation(4, -6, TimeSpan.FromSeconds(2.4))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                });

            Show(Stage.Welcome);

            // developer aid: /snap=<file.png> renders the window and exits
            var snap = App.Args.FirstOrDefault(a => a.StartsWith("/snap=", StringComparison.OrdinalIgnoreCase))?.Substring(6).Trim('"');
            if (snap != null)
            {
                if (App.Args.Any(a => a == "/snapdone")) Show(Stage.Done);
                if (App.Args.Any(a => a == "/snapprogress")) { Show(Stage.Working); SetProgress(0.62, Strings.StepApp); }
                await Task.Delay(1500);
                var rtb = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(Root);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(snap)) enc.Save(fs);
                Close();
            }
        }

        // ============================== stages ==============================

        void Show(Stage s)
        {
            _stage = s;
            WelcomePage.Visibility = s == Stage.Welcome ? Visibility.Visible : Visibility.Collapsed;
            ProgressPage.Visibility = s == Stage.Working ? Visibility.Visible : Visibility.Collapsed;
            DonePage.Visibility = s == Stage.Done || s == Stage.Error ? Visibility.Visible : Visibility.Collapsed;
            PrimaryButton.Visibility = SecondaryButton.Visibility = Visibility.Visible;

            switch (s)
            {
                case Stage.Welcome:
                    PrimaryButton.Content = Strings.Install;
                    SecondaryButton.Content = Strings.Cancel;
                    break;
                case Stage.Working:
                    PrimaryButton.Visibility = SecondaryButton.Visibility = Visibility.Collapsed;
                    break;
                case Stage.Done:
                    DoneTitle.Text = Strings.DoneTitle;
                    DoneText.Text = Strings.DoneText;
                    PrimaryButton.Content = Strings.LaunchNow;
                    SecondaryButton.Content = Strings.Close;
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

        // ============================== install ==============================

        async Task RunInstall()
        {
            if (_busy) return;
            _busy = true;
            ProgressTitle.Text = Strings.Installing;
            Show(Stage.Working);
            bool desktop = DesktopBox.IsChecked == true;
            try
            {
                await Task.Run(() => Installer.Install(desktop, SetProgress));
                _busy = false;
                Show(Stage.Done);
                if (LaunchBox.IsChecked == true) Installer.Launch();
            }
            catch (Exception ex)
            {
                _busy = false;
                Installer.Log("FAILED: " + ex.Message);
                DoneText.Text = ex.Message;
                Show(Stage.Error);
            }
        }

        // ============================== buttons ==============================

        async void Primary_Click(object sender, RoutedEventArgs e)
        {
            switch (_stage)
            {
                case Stage.Welcome: await RunInstall(); break;
                case Stage.Done:
                    if (LaunchBox.IsChecked != true) Installer.Launch();
                    Close();
                    break;
                default: Close(); break;
            }
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
    }
}
