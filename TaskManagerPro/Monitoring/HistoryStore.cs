using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Dispatching;
using TaskManagerPro.Helpers;

namespace TaskManagerPro.Monitoring
{
    /// <summary>
    /// تاریخچه‌ی یک‌ساعته‌ی متریک‌های اصلی سیستم (نمونه‌برداری هر ثانیه).
    ///
    /// هر نمونه به «ثانیه‌ی واقعی» (Unix epoch) گره خورده است؛ یعنی جای هر نمونه در بافر
    /// از روی زمان مطلق آن حساب می‌شود (epoch % Capacity). نتیجه:
    ///  - پنجره‌های ۱۰ دقیقه / ۱ ساعت روی مرزهای زمانی ثابت بریده می‌شوند، پس نمودار
    ///    با هر تیک کاملاً عوض نمی‌شود؛ فقط هر چند ثانیه یک ستون جدید اضافه می‌شود.
    ///  - داده روی دیسک ذخیره می‌شود و با باز شدن دوباره‌ی برنامه برمی‌گردد؛ مدتی که
    ///    برنامه خاموش بوده به‌صورت «شکاف» (NaN) نگه داشته می‌شود، نه داده‌ی جعلی.
    /// </summary>
    public static class HistoryStore
    {
        public const int Capacity = 3600; // یک ساعت با رزولوشن ۱ ثانیه

        private static readonly object Lock = new();
        private static readonly Dictionary<string, double[]> Buffers = new();
        private static DispatcherQueueTimer? _timer;
        private static bool _reading;

        /// <summary>ثانیه‌ی (epoch) جدیدترین نمونه — 0 یعنی هنوز داده‌ای نداریم</summary>
        private static long _lastEpoch;

        /// <summary>ثانیه‌ی (epoch) قدیمی‌ترین نمونه‌ای که تا حالا ثبت شده</summary>
        private static long _firstEpoch;

        private static readonly string[] Keys = { "cpu", "mem", "gpu", "disk", "netdown", "netup" };

        private const int FileMagic = 0x504C5348; // "PLSH"
        private const int FileVersion = 1;
        private static DateTime _lastSave = DateTime.MinValue;

        static HistoryStore()
        {
            foreach (var k in Keys)
            {
                var buf = new double[Capacity];
                for (int i = 0; i < Capacity; i++) buf[i] = double.NaN;
                Buffers[k] = buf;
            }
        }

        /// <summary>تعداد ثانیه‌های پوشش‌داده‌شده‌ی تاریخچه (حداکثر یک ساعت)</summary>
        public static int Count
        {
            get
            {
                lock (Lock)
                {
                    if (_lastEpoch == 0) return 0;
                    return (int)Math.Min(Capacity, _lastEpoch - _firstEpoch + 1);
                }
            }
        }

        /// <summary>شروع نمونه‌برداری سراسری (یک بار در OnLaunched)</summary>
        public static void Start(DispatcherQueue dq)
        {
            if (_timer != null) return;
            LoadFromDisk();
            _timer = dq.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => Sample();
            _timer.Start();
        }

        private static long NowEpoch() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static void Sample()
        {
            if (_reading) return;
            _reading = true;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var s = SystemMonitor.Instance.Read();
                    long e = NowEpoch();
                    lock (Lock)
                    {
                        if (e > _lastEpoch)
                        {
                            FillGap(e);
                            int i = (int)(e % Capacity);
                            Buffers["cpu"][i] = s.CpuTotal;
                            Buffers["mem"][i] = s.MemPercent;
                            Buffers["gpu"][i] = Math.Max(s.GpuPercent, 0);
                            Buffers["disk"][i] = s.DiskPercent;
                            Buffers["netdown"][i] = s.NetRecvKBs;
                            Buffers["netup"][i] = s.NetSentKBs;

                            _lastEpoch = e;
                            if (_firstEpoch == 0) _firstEpoch = e;
                        }
                    }
                    AlarmManager.Check(s);
                    MaybeSave();
                }
                catch { }
                finally { _reading = false; }
            });
        }

        /// <summary>
        /// ثانیه‌هایی که نمونه نگرفتیم (برنامه بسته بوده یا سیستم Sleep بوده) را NaN می‌کند
        /// تا در نمودار به‌صورت شکاف دیده شوند و داده‌ی قدیمی جای آن‌ها را نگیرد.
        /// </summary>
        private static void FillGap(long newEpoch)
        {
            if (_lastEpoch == 0) return;
            long start = Math.Max(_lastEpoch + 1, newEpoch - Capacity + 1);
            for (long t = start; t < newEpoch; t++)
            {
                int i = (int)(t % Capacity);
                foreach (var k in Keys) Buffers[k][i] = double.NaN;
            }
        }

        /// <summary>آیا برای این ثانیه داده‌ی معتبر داریم؟ (داخل پنجره‌ی بافر و ثبت‌شده)</summary>
        private static bool InWindow(long epoch) =>
            _lastEpoch != 0 && epoch <= _lastEpoch && epoch >= _firstEpoch && epoch > _lastEpoch - Capacity;

        /// <summary>
        /// یک پنجره از تاریخچه: durationSec ثانیه که offsetSec ثانیه قبل تمام می‌شود
        /// (offset = 0 یعنی تا همین الان).
        ///
        /// ستون‌ها روی مرزهای زمانی مطلق بریده می‌شوند (مثلاً هر ۵ ثانیه برای پنجره‌ی
        /// ۱۰ دقیقه‌ای)، پس با گذر زمان فقط ستون جدید اضافه می‌شود و بقیه‌ی نمودار تکان نمی‌خورد.
        /// ستون بدون داده مقدار NaN می‌گیرد (در نمودار به‌صورت شکاف نمایش داده می‌شود).
        /// </summary>
        public static double[] GetSeries(string key, int durationSec, int offsetSec, int points = 120)
        {
            lock (Lock)
            {
                if (!Buffers.TryGetValue(key, out var buf) || _lastEpoch == 0)
                    return Array.Empty<double>();

                durationSec = Math.Clamp(durationSec, 2, Capacity);
                points = Math.Clamp(points, 2, durationSec);
                offsetSec = Math.Max(0, offsetSec);

                int bucket = Math.Max(1, durationSec / points);
                int n = Math.Max(2, durationSec / bucket);

                long endEpoch = _lastEpoch - offsetSec;
                long lastBucket = endEpoch / bucket;

                var result = new double[n];
                for (int i = 0; i < n; i++)
                {
                    long b = lastBucket - (n - 1 - i);
                    long s0 = b * bucket;
                    double sum = 0;
                    int cnt = 0;
                    for (long t = s0; t < s0 + bucket; t++)
                    {
                        if (t > endEpoch || !InWindow(t)) continue;
                        double v = buf[(int)(t % Capacity)];
                        if (double.IsNaN(v)) continue;
                        sum += v;
                        cnt++;
                    }
                    result[i] = cnt > 0 ? sum / cnt : double.NaN;
                }
                return result;
            }
        }

        /// <summary>آخرین n نمونه‌ی یک متریک (برای mini-گراف Tray) — شکاف‌ها صفر می‌شوند</summary>
        public static double[] GetRecent(string key, int n)
        {
            var s = GetSeries(key, n, 0, n);
            for (int i = 0; i < s.Length; i++)
                if (double.IsNaN(s[i])) s[i] = 0;
            return s;
        }

        // ---------- ذخیره‌ی واقعی روی دیسک ----------

        private static string FilePath()
        {
            string dir = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            return Path.Combine(dir, "history.dat");
        }

        private static void MaybeSave()
        {
            if ((DateTime.UtcNow - _lastSave).TotalSeconds < 30) return;
            _lastSave = DateTime.UtcNow;
            SaveToDisk();
        }

        /// <summary>نوشتن کل بافر روی دیسک (هر ۳۰ ثانیه)</summary>
        public static void SaveToDisk()
        {
            try
            {
                string path = FilePath();
                string tmp = path + ".tmp";

                lock (Lock)
                {
                    if (_lastEpoch == 0) return;
                    using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var w = new BinaryWriter(fs))
                    {
                        w.Write(FileMagic);
                        w.Write(FileVersion);
                        w.Write(Capacity);
                        w.Write(Keys.Length);
                        w.Write(_lastEpoch);
                        w.Write(_firstEpoch);
                        foreach (var k in Keys)
                        {
                            w.Write(k);
                            var buf = Buffers[k];
                            for (int i = 0; i < Capacity; i++) w.Write((float)buf[i]);
                        }
                    }
                }

                File.Copy(tmp, path, overwrite: true);
                File.Delete(tmp);
            }
            catch { }
        }

        /// <summary>خواندن تاریخچه‌ی ذخیره‌شده هنگام باز شدن برنامه</summary>
        private static void LoadFromDisk()
        {
            try
            {
                string path = FilePath();
                if (!File.Exists(path)) return;

                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var r = new BinaryReader(fs);

                if (r.ReadInt32() != FileMagic) return;
                if (r.ReadInt32() != FileVersion) return;
                if (r.ReadInt32() != Capacity) return;
                int keyCount = r.ReadInt32();
                if (keyCount != Keys.Length) return;

                long lastEpoch = r.ReadInt64();
                long firstEpoch = r.ReadInt64();
                long now = NowEpoch();

                // داده‌ی کاملاً کهنه (بیشتر از یک ساعت) دیگر به درد پنجره‌ها نمی‌خورد
                if (lastEpoch <= 0 || now - lastEpoch >= Capacity) return;

                var loaded = new Dictionary<string, double[]>();
                for (int k = 0; k < keyCount; k++)
                {
                    string name = r.ReadString();
                    var buf = new double[Capacity];
                    for (int i = 0; i < Capacity; i++)
                    {
                        float v = r.ReadSingle();
                        buf[i] = float.IsNaN(v) ? double.NaN : v;
                    }
                    loaded[name] = buf;
                }

                lock (Lock)
                {
                    foreach (var k in Keys)
                        if (loaded.TryGetValue(k, out var buf))
                            Array.Copy(buf, Buffers[k], Capacity);

                    _lastEpoch = lastEpoch;
                    _firstEpoch = firstEpoch > 0 ? firstEpoch : lastEpoch;
                }
            }
            catch { }
        }
    }
}
