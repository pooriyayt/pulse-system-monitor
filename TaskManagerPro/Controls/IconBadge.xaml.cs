using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TaskManagerPro.Helpers;
using Windows.Foundation;

namespace TaskManagerPro.Controls
{
    /// <summary>
    /// آیکون رنگی گرد-مربعی (squircle) با گرادیان، مثل آیکون‌های تنظیمات macOS.
    /// اگر Tint داده نشود، رنگ از روی خود آیکون انتخاب می‌شود تا هر بخش رنگ ثابت خودش را داشته باشد.
    /// </summary>
    public sealed partial class IconBadge : UserControl
    {
        // رنگ ثابت هر بخش (پالت سیستمی اپل)
        private static readonly Dictionary<string, string> GlyphTints = new()
        {
            [""] = "#0A84FF", // Overview
            [""] = "#30D158", // Performance
            [""] = "#5E5CE6", // Processes
            [""] = "#BF5AF2", // Startup
            [""] = "#8E8E93", // Services
            [""] = "#FF375F", // Memory optimizer
            [""] = "#40C8E0", // Network
            [""] = "#FF9F0A", // File unlocker
            [""] = "#8E8E93", // Settings
            [""] = "#0A84FF", // CPU
            [""] = "#FF375F", // Memory
            [""] = "#30D158", // GPU
            [""] = "#FF9F0A", // Disk
            [""] = "#40C8E0", // Network card
            [""] = "#8E8E93", // Hardware
            [""] = "#0A84FF", // Pulse
        };

        public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
            nameof(Glyph), typeof(string), typeof(IconBadge),
            new PropertyMetadata("", (d, e) => ((IconBadge)d).Apply()));

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(double), typeof(IconBadge),
            new PropertyMetadata(40.0, (d, e) => ((IconBadge)d).Apply()));

        public static readonly DependencyProperty TintProperty = DependencyProperty.Register(
            nameof(Tint), typeof(string), typeof(IconBadge),
            new PropertyMetadata("", (d, e) => ((IconBadge)d).Apply()));

        public IconBadge()
        {
            this.InitializeComponent();
            Loaded += (_, _) => { AppSettings.AppearanceChanged += Apply; Apply(); };
            Unloaded += (_, _) => AppSettings.AppearanceChanged -= Apply;
            ActualThemeChanged += (_, _) => Apply();
        }

        public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
        public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
        /// <summary>رنگ هگز دلخواه (خالی = خودکار)</summary>
        public string Tint { get => (string)GetValue(TintProperty); set => SetValue(TintProperty, value); }

        private void Apply()
        {
            double size = Size;
            Root.Width = Root.Height = size;
            var r = new CornerRadius(size * 0.27);
            Fill.CornerRadius = r;
            Shine.CornerRadius = r;
            Icon.FontSize = size * 0.46;
            Icon.Glyph = Glyph ?? "";

            string hex = !string.IsNullOrEmpty(Tint) ? Tint
                : (Glyph != null && GlyphTints.TryGetValue(Glyph, out var t) ? t : AppSettings.AccentColor);
            var c = ColorUtil.FromHex(hex);
            // شیشه‌ی رنگی: پس‌زمینه‌ی نیمه‌شفاف + آیکون روشن هم‌رنگ (ملایم‌تر و پرمیوم‌تر از رنگ تخت)
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            g.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(92, c.R, c.G, c.B), Offset = 0 });
            g.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(40, c.R, c.G, c.B), Offset = 1 });
            Fill.Background = g;
            Icon.Foreground = new SolidColorBrush(ColorUtil.Shift(c, ActualTheme == ElementTheme.Light ? -0.15 : 0.45));
        }
    }
}
