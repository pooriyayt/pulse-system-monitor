using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TaskManagerPro.Helpers;
using Windows.Foundation;

namespace TaskManagerPro.Controls
{
    /// <summary>
    /// گراف زنده‌ی روان و پیوسته (بدون کتابخانه‌ی جانبی).
    ///
    /// نحوه‌ی کار انیمیشن:
    /// به‌جای اینکه با هر داده‌ی جدید، گراف «پرشی» جابه‌جا شود، این کنترل در هر فریم
    /// (60 بار در ثانیه با CompositionTarget.Rendering) کل خط را کمی به چپ می‌لغزاند.
    /// نقطه‌ی جدید از لبه‌ی راست به‌آرامی وارد تصویر می‌شود؛ نتیجه یک اسکرول کاملاً
    /// نرم و پیوسته مثل Task Manager ویندوز است.
    /// </summary>
    public sealed partial class LiveGraph : UserControl
    {
        private readonly List<double> _values = new();

        /// <summary>تعداد نقاط نگه‌داشته‌شده روی گراف (پهنای تاریخچه)</summary>
        public int MaxPoints { get; set; } = 60;

        /// <summary>سقف محور عمودی (مثلاً 100 برای درصد)</summary>
        public double MaxValue { get; set; } = 100;

        /// <summary>اگر true باشد، سقف گراف خودکار با بزرگترین مقدار تنظیم می‌شود (برای سرعت شبکه)</summary>
        public bool AutoScale { get; set; } = false;

        private bool _customBrush;
        private bool _rendering;
        private DateTime _lastAdd = DateTime.MinValue;
        private double _intervalMs = 1000;

        public LiveGraph()
        {
            this.InitializeComponent();
            ApplyAppearance();

            Loaded += (_, _) =>
            {
                ApplyAppearance();
                AppSettings.AppearanceChanged += ApplyAppearance;
                StartRendering();
            };

            // وقتی صفحه عوض می‌شود، رندر متوقف شود تا هیچ منبعی هدر نرود.
            Unloaded += (_, _) =>
            {
                AppSettings.AppearanceChanged -= ApplyAppearance;
                StopRendering();
            };
        }

        /// <summary>رنگ خط و ناحیه‌ی پرشده (اختیاری — پیش‌فرض از تنظیمات برنامه می‌آید)</summary>
        public Brush GraphBrush
        {
            get => Line.Stroke;
            set
            {
                _customBrush = true;
                Line.Stroke = value;
                FillArea.Fill = value is SolidColorBrush s ? MakeGradient(s.Color) : value;
            }
        }

        // ---------- حالت تاریخچه (نمایش ثابت یک سری داده، بدون انیمیشن لغزش) ----------

        private IReadOnlyList<double>? _staticSeries;

        /// <summary>نمایش ثابت یک سری داده (مثلاً ۱۰ دقیقه‌ی گذشته). لغزش زنده متوقف می‌شود.</summary>
        public void SetStaticSeries(IReadOnlyList<double> values)
        {
            _staticSeries = values;
        }

        /// <summary>برگشت به حالت زنده</summary>
        public void ExitStatic()
        {
            _staticSeries = null;
        }

        private void ApplyAppearance()
        {
            if (!_customBrush)
            {
                var c = ColorUtil.FromHex(AppSettings.AccentColor);
                Line.Stroke = new SolidColorBrush(c);
                FillArea.Fill = MakeGradient(c);
            }

            FillArea.Visibility = AppSettings.GraphFill ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>گرادیان عمودی زیر خط گراف (پررنگ بالا، محو پایین) — حس پرمیوم</summary>
        internal static Brush MakeGradient(Windows.UI.Color c)
        {
            var g = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
            };
            g.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(110, c.R, c.G, c.B), Offset = 0 });
            g.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(10, c.R, c.G, c.B), Offset = 1 });
            return g;
        }

        /// <summary>یک مقدار جدید به گراف اضافه کن (مثلاً درصد CPU فعلی)</summary>
        public void AddValue(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) value = 0;

            // فاصله‌ی واقعی بین داده‌ها را یاد بگیر تا سرعت انیمیشن دقیقاً با آن هماهنگ شود.
            var now = DateTime.UtcNow;
            if (_lastAdd != DateTime.MinValue)
            {
                double ms = (now - _lastAdd).TotalMilliseconds;
                if (ms >= 100 && ms <= 5000)
                    _intervalMs = (_intervalMs * 0.7) + (ms * 0.3);
            }
            _lastAdd = now;

            _values.Add(value);
            // دو نقطه بیشتر نگه می‌داریم تا نقطه‌ی قدیمی هنگام خروج از لبه‌ی چپ ناگهان حذف نشود.
            while (_values.Count > MaxPoints + 2) _values.RemoveAt(0);
        }

        /// <summary>
        /// پاک کردن کامل گراف — وقتی در صفحه‌ی Performance قطعه‌ی انتخابی عوض می‌شود،
        /// داده‌های قطعه‌ی قبلی نباید روی گراف بماند.
        /// </summary>
        public void Clear()
        {
            _values.Clear();
            _lastAdd = DateTime.MinValue;
            Line.Points = new PointCollection();
            FillArea.Points = new PointCollection();
        }

        private void StartRendering()
        {
            if (_rendering) return;
            _rendering = true;
            CompositionTarget.Rendering += OnRendering;
        }

        private void StopRendering()
        {
            if (!_rendering) return;
            _rendering = false;
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object? sender, object e) => Redraw();

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ClipGeometry.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
        }

        private void Redraw()
        {
            double w = RootGrid.ActualWidth;
            double h = RootGrid.ActualHeight;
            if (w <= 0 || h <= 0) return;

            // حالت تاریخچه: کل سری داده ثابت و بدون لغزش کشیده می‌شود
            if (_staticSeries != null)
            {
                DrawStatic(w, h);
                return;
            }

            if (_values.Count < 2) return;

            double max = MaxValue;
            if (AutoScale)
            {
                max = 1;
                foreach (var v in _values)
                    if (v > max) max = v;
                max *= 1.2;
            }

            // پیشرفت زمانی از آخرین داده (0 تا 1) — عامل حرکت پیوسته‌ی گراف
            double t = 0;
            if (_lastAdd != DateTime.MinValue)
                t = Math.Clamp((DateTime.UtcNow - _lastAdd).TotalMilliseconds / _intervalMs, 0.0, 1.0);

            double stepX = w / (MaxPoints - 1);
            double slide = t * stepX;

            var linePoints = new PointCollection();
            var fillPoints = new PointCollection();

            int count = _values.Count;
            double firstX = 0, lastX = 0;

            for (int i = 0; i < count; i++)
            {
                // جدیدترین نقطه از سمت راست وارد می‌شود و همه‌چیز نرم به چپ می‌لغزد.
                double x = w + stepX - slide - ((count - 1 - i) * stepX);
                double ratio = Math.Min(_values[i] / max, 1.0);
                // 4 پیکسل حاشیه از بالا و پایین تا خط به لبه نچسبد
                double y = h - ratio * (h - 8) - 4;

                var p = new Point(x, y);
                linePoints.Add(p);
                fillPoints.Add(p);

                if (i == 0) firstX = x;
                lastX = x;
            }

            // بستن چندضلعی برای ناحیه‌ی پرشده
            fillPoints.Add(new Point(lastX, h));
            fillPoints.Add(new Point(firstX, h));

            Line.Points = linePoints;
            FillArea.Points = fillPoints;
        }

        private void DrawStatic(double w, double h)
        {
            var vals = _staticSeries!;
            if (vals.Count < 2)
            {
                Line.Points = new PointCollection();
                FillArea.Points = new PointCollection();
                return;
            }

            double max = MaxValue;
            if (AutoScale)
            {
                max = 1;
                foreach (var v in vals)
                    if (v > max) max = v;
                max *= 1.2;
            }

            var linePoints = new PointCollection();
            var fillPoints = new PointCollection();
            double stepX = w / (vals.Count - 1);

            for (int i = 0; i < vals.Count; i++)
            {
                double v = vals[i];
                if (double.IsNaN(v) || v < 0) v = 0;
                double ratio = Math.Min(v / max, 1.0);
                var p = new Point(i * stepX, h - ratio * (h - 8) - 4);
                linePoints.Add(p);
                fillPoints.Add(p);
            }

            fillPoints.Add(new Point(w, h));
            fillPoints.Add(new Point(0, h));

            Line.Points = linePoints;
            FillArea.Points = fillPoints;
        }
    }
}
