using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TaskManagerPro.Helpers;
using Windows.Foundation;

namespace TaskManagerPro.Controls
{
    /// <summary>
    /// گیج حلقه‌ای (۰ تا ۱۰۰) با انیمیشن نرم بین مقادیر.
    /// رنگ پیش‌فرض همان Accent برنامه است؛ اگر UseLoadColors روشن باشد بر اساس بار
    /// سبز / نارنجی / قرمز می‌شود.
    /// </summary>
    public sealed partial class RingGauge : UserControl
    {
        private double _target;
        private double _shown;
        private bool _animating;
        private Brush? _customBrush;

        public RingGauge()
        {
            this.InitializeComponent();
            Loaded += (_, _) =>
            {
                AppSettings.AppearanceChanged += ApplyBrush;
                ApplyBrush();
                Redraw();
            };
            Unloaded += (_, _) =>
            {
                AppSettings.AppearanceChanged -= ApplyBrush;
                StopAnim();
            };
        }

        /// <summary>ضخامت حلقه</summary>
        public double Thickness { get; set; } = 8;

        /// <summary>رنگ بر اساس بار (سبز/نارنجی/قرمز) به‌جای Accent</summary>
        public bool UseLoadColors { get; set; }

        /// <summary>رنگ ثابت دلخواه</summary>
        public Brush? GaugeBrush
        {
            get => _customBrush;
            set { _customBrush = value; ApplyBrush(); }
        }

        /// <summary>مقدار فعلی (۰ تا ۱۰۰) — با انیمیشن به مقدار جدید می‌رسد</summary>
        public double Value
        {
            get => _target;
            set
            {
                if (double.IsNaN(value)) value = 0;
                _target = Math.Clamp(value, 0, 100);
                if (UseLoadColors) ApplyBrush();
                StartAnim();
            }
        }

        private void ApplyBrush()
        {
            if (_customBrush != null) { Arc.Stroke = _customBrush; return; }
            if (UseLoadColors)
            {
                Arc.Stroke = LoadPalette.BrushFor(_target);
                return;
            }
            Arc.Stroke = new SolidColorBrush(ColorUtil.FromHex(AppSettings.AccentColor));
        }

        private void StartAnim()
        {
            if (_animating) return;
            _animating = true;
            CompositionTarget.Rendering += OnRendering;
        }

        private void StopAnim()
        {
            if (!_animating) return;
            _animating = false;
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object? sender, object e)
        {
            // نزدیک شدن نمایی به مقدار هدف (حس روان و طبیعی)
            double diff = _target - _shown;
            if (Math.Abs(diff) < 0.15)
            {
                _shown = _target;
                Redraw();
                StopAnim();
                return;
            }
            _shown += diff * 0.14;
            Redraw();
        }

        private void Root_SizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

        private void Redraw()
        {
            double w = Root.ActualWidth, h = Root.ActualHeight;
            double size = Math.Min(w, h);
            if (size <= Thickness * 2) return;

            Track.StrokeThickness = Thickness;
            Track.Width = Track.Height = size;
            Arc.StrokeThickness = Thickness;

            double r = (size - Thickness) / 2;
            var c = new Point(w / 2, h / 2);
            double pct = Math.Clamp(_shown, 0, 100) / 100.0;

            if (pct <= 0.001)
            {
                Arc.Data = null;
                return;
            }
            if (pct >= 0.9999) pct = 0.9999; // کمان کامل با ArcSegment رسم نمی‌شود

            double startAngle = -90;
            double endAngle = startAngle + pct * 360;
            Point P(double deg)
            {
                double rad = deg * Math.PI / 180;
                return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
            }

            var fig = new PathFigure { StartPoint = P(startAngle), IsClosed = false, IsFilled = false };
            fig.Segments.Add(new ArcSegment
            {
                Point = P(endAngle),
                Size = new Size(r, r),
                IsLargeArc = pct > 0.5,
                SweepDirection = SweepDirection.Clockwise,
            });
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            Arc.Data = geo;
        }
    }

    /// <summary>رنگ‌های وضعیت بار سیستم (سالم / پرمصرف / بحرانی)</summary>
    public static class LoadPalette
    {
        public static readonly Windows.UI.Color Ok = Windows.UI.Color.FromArgb(255, 0x3D, 0xD6, 0x8C);
        public static readonly Windows.UI.Color Warn = Windows.UI.Color.FromArgb(255, 0xFF, 0xB5, 0x47);
        public static readonly Windows.UI.Color Danger = Windows.UI.Color.FromArgb(255, 0xFF, 0x5C, 0x6C);

        public static Windows.UI.Color ColorFor(double pct) =>
            pct >= 85 ? Danger : pct >= 60 ? Warn : Ok;

        public static SolidColorBrush BrushFor(double pct) => new(ColorFor(pct));

        /// <summary>برچسب کوتاه وضعیت</summary>
        public static string LabelFor(double pct) =>
            pct >= 85 ? L10n.T("High") : pct >= 60 ? L10n.T("Busy") : L10n.T("Normal");
    }
}
