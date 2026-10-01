using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TaskManagerPro.Helpers;
using Windows.Foundation;

namespace TaskManagerPro.Controls
{
    /// <summary>واحد اعداد محور گراف</summary>
    public enum GraphUnit
    {
        /// <summary>درصد (۰ تا ۱۰۰)</summary>
        Percent = 0,
        /// <summary>سرعت شبکه — ورودی بر حسب KB/s</summary>
        SpeedKBs = 1,
        /// <summary>سرعت دیسک — ورودی بر حسب MB/s</summary>
        MegaBytesPerSec = 2,
        /// <summary>عدد خام بدون واحد</summary>
        Raw = 3,
    }

    /// <summary>
    /// گراف زنده‌ی روان و پیوسته (بدون کتابخانه‌ی جانبی).
    ///
    /// نحوه‌ی کار انیمیشن:
    /// به‌جای اینکه با هر داده‌ی جدید، گراف «پرشی» جابه‌جا شود، این کنترل در هر فریم
    /// (60 بار در ثانیه با CompositionTarget.Rendering) کل خط را کمی به چپ می‌لغزاند.
    ///
    /// اعداد محور: سقف / وسط / کف محور عمودی و مقدار فعلی روی گراف نوشته می‌شوند تا
    /// معلوم باشد گراف دقیقاً چه چیزی و در چه مقیاسی را نشان می‌دهد.
    ///
    /// مقدار NaN یعنی «داده نداریم» (مثلاً مدتی که برنامه بسته بوده) و در نمودار
    /// به‌صورت شکاف واقعی دیده می‌شود، نه خط جعلی.
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

        /// <summary>واحد اعداد محور</summary>
        public GraphUnit Unit { get; set; } = GraphUnit.Percent;

        private bool _customBrush;
        private bool _rendering;
        private DateTime _lastAdd = DateTime.MinValue;
        private double _intervalMs = 1000;
        private double _shownMax = -1;
        private string _spanText = "";

        public LiveGraph()
        {
            this.InitializeComponent();
            // گراف همیشه چپ‌به‌راست کشیده می‌شود، حتی در زبان‌های راست‌به‌چپ
            RootGrid.FlowDirection = FlowDirection.LeftToRight;
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

        private bool _showAxis = true;
        /// <summary>نمایش اعداد محور و خطوط راهنما (برای گراف‌های خیلی کوچک خاموش می‌شود)</summary>
        public bool ShowAxis
        {
            get => _showAxis;
            set
            {
                _showAxis = value;
                var vis = value ? Visibility.Visible : Visibility.Collapsed;
                AxisLayer.Visibility = vis;
                GridLines.Visibility = vis;
            }
        }

        /// <summary>برچسب بازه‌ی زمانی گوشه‌ی پایین-راست گراف (مثلاً «۶۰ ثانیه» یا «۱ ساعت»)</summary>
        public string SpanText
        {
            get => _spanText;
            set
            {
                _spanText = value ?? "";
                SpanLabel.Text = _spanText;
            }
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
            _staticDirty = true;
        }

        /// <summary>برگشت به حالت زنده</summary>
        public void ExitStatic()
        {
            _staticSeries = null;
        }

        // در حالت تاریخچه داده ثابت است؛ فقط وقتی سری یا اندازه عوض شد دوباره رسم می‌کنیم
        private bool _staticDirty;

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
            g.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(170, c.R, c.G, c.B), Offset = 0 });
            g.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(0, c.R, c.G, c.B), Offset = 1 });
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
            Line.Data = null;
            FillArea.Data = null;
            _shownMax = -1;
            CurrentLabel.Text = "";
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
            _staticDirty = true; // با تغییر اندازه، نمودار تاریخچه باید دوباره کشیده شود
        }

        // ---------- اعداد محور ----------

        /// <summary>یک عدد را با واحد گراف قالب‌بندی می‌کند</summary>
        public string Format(double v)
        {
            switch (Unit)
            {
                case GraphUnit.SpeedKBs:
                    if (v >= 1024 * 1024) return $"{v / (1024.0 * 1024.0):F1} GB/s";
                    if (v >= 1024) return $"{v / 1024.0:F1} MB/s";
                    return $"{v:F0} KB/s";
                case GraphUnit.MegaBytesPerSec:
                    return v >= 100 ? $"{v:F0} MB/s" : $"{v:F1} MB/s";
                case GraphUnit.Raw:
                    return v >= 100 ? $"{v:F0}" : $"{v:F1}";
                default:
                    return $"{v:F0}%";
            }
        }

        /// <summary>سقف «گرد» برای محور خودکار (۱ / ۲ / ۵ ضربدر توان ۱۰) تا عدد محور نپرد</summary>
        private static double NiceMax(double raw)
        {
            if (raw <= 0 || double.IsNaN(raw) || double.IsInfinity(raw)) return 1;
            double exp = Math.Floor(Math.Log10(raw));
            double p = Math.Pow(10, exp);
            double f = raw / p;
            double nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10;
            return nice * p;
        }

        private void UpdateAxisLabels(double max, double? current)
        {
            if (Math.Abs(max - _shownMax) > 0.0001)
            {
                _shownMax = max;
                MaxLabel.Text = Format(max);
                MidLabel.Text = Format(max / 2);
                MinLabel.Text = Format(0);
            }

            string cur = current.HasValue && !double.IsNaN(current.Value) ? Format(current.Value) : "";
            if (CurrentLabel.Text != cur) CurrentLabel.Text = cur;
        }

        // ---------- رسم ----------

        private void Redraw()
        {
            double w = RootGrid.ActualWidth;
            double h = RootGrid.ActualHeight;
            if (w <= 0 || h <= 0) return;

            // حالت تاریخچه: کل سری داده ثابت و بدون لغزش کشیده می‌شود
            if (_staticSeries != null)
            {
                if (_staticDirty)
                {
                    _staticDirty = false;
                    DrawStatic(w, h);
                }
                return;
            }

            if (_values.Count < 2)
            {
                UpdateAxisLabels(AutoScale ? NiceMax(MaxValue) : MaxValue, null);
                return;
            }

            double max = MaxValue;
            if (AutoScale)
            {
                double peak = 0;
                foreach (var v in _values)
                    if (v > peak) peak = v;
                max = NiceMax(Math.Max(peak * 1.15, 1));
            }

            // پیشرفت زمانی از آخرین داده (0 تا 1) — عامل حرکت پیوسته‌ی گراف
            double t = 0;
            if (_lastAdd != DateTime.MinValue)
                t = Math.Clamp((DateTime.UtcNow - _lastAdd).TotalMilliseconds / _intervalMs, 0.0, 1.0);

            double stepX = w / (MaxPoints - 1);
            double slide = t * stepX;

            int count = _values.Count;
            var pts = new List<Point?>(count);
            for (int i = 0; i < count; i++)
            {
                // جدیدترین نقطه از سمت راست وارد می‌شود و همه‌چیز نرم به چپ می‌لغزد.
                double x = w + stepX - slide - ((count - 1 - i) * stepX);
                pts.Add(new Point(x, ValueToY(_values[i], max, h)));
            }

            Draw(pts, h);
            UpdateAxisLabels(max, _values[count - 1]);
        }

        private void DrawStatic(double w, double h)
        {
            var vals = _staticSeries!;
            if (vals.Count < 2)
            {
                Line.Data = null;
                FillArea.Data = null;
                UpdateAxisLabels(AutoScale ? NiceMax(MaxValue) : MaxValue, null);
                return;
            }

            double max = MaxValue;
            if (AutoScale)
            {
                double peak = 0;
                foreach (var v in vals)
                    if (!double.IsNaN(v) && v > peak) peak = v;
                max = NiceMax(Math.Max(peak * 1.15, 1));
            }

            double stepX = w / (vals.Count - 1);
            var pts = new List<Point?>(vals.Count);
            double? last = null;

            for (int i = 0; i < vals.Count; i++)
            {
                double v = vals[i];
                if (double.IsNaN(v))
                {
                    pts.Add(null); // شکاف واقعی در داده
                    continue;
                }
                if (v < 0) v = 0;
                last = v;
                pts.Add(new Point(i * stepX, ValueToY(v, max, h)));
            }

            Draw(pts, h);
            UpdateAxisLabels(max, last);
        }

        private static double ValueToY(double v, double max, double h)
        {
            double ratio = max > 0 ? Math.Min(v / max, 1.0) : 0;
            // 4 پیکسل حاشیه از بالا و پایین تا خط به لبه نچسبد
            return h - ratio * (h - 8) - 4;
        }

        /// <summary>
        /// رسم خط و ناحیه‌ی زیر آن. مقدار null در لیست یعنی شکاف — خط قطع می‌شود
        /// و از نقطه‌ی بعدی دوباره شروع می‌شود.
        /// </summary>
        private void Draw(List<Point?> pts, double h)
        {
            var lineGeo = new PathGeometry();
            var fillGeo = new PathGeometry();

            var segment = new List<Point>();
            void Flush()
            {
                if (segment.Count >= 2)
                {
                    var lf = new PathFigure { StartPoint = segment[0], IsClosed = false, IsFilled = false };
                    var ls = new PolyLineSegment();
                    for (int i = 1; i < segment.Count; i++) ls.Points.Add(segment[i]);
                    lf.Segments.Add(ls);
                    lineGeo.Figures.Add(lf);

                    var ff = new PathFigure { StartPoint = new Point(segment[0].X, h), IsClosed = true, IsFilled = true };
                    var fs = new PolyLineSegment();
                    foreach (var p in segment) fs.Points.Add(p);
                    fs.Points.Add(new Point(segment[^1].X, h));
                    ff.Segments.Add(fs);
                    fillGeo.Figures.Add(ff);
                }
                segment.Clear();
            }

            foreach (var p in pts)
            {
                if (p == null) Flush();
                else segment.Add(p.Value);
            }
            Flush();

            Line.Data = lineGeo;
            FillArea.Data = fillGeo;
        }
    }
}
