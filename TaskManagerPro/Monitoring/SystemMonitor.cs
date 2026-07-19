using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace TaskManagerPro.Monitoring
{
    /// <summary>وضعیت یک کارت گرافیک (مجزا برای هر GPU، مثلاً Intel داخلی + NVIDIA)</summary>
    public class GpuStat
    {
        public int PhysIndex;
        public string Name = "GPU";
        public double UsagePercent;
        public double DedicatedMB;

        /// <summary>مصرف موتورهای این GPU به درصد (3D، Copy، VideoDecode، VideoProcessing و ...)</summary>
        public Dictionary<string, double> Engines = new();
    }

    /// <summary>یک عکس لحظه‌ای از وضعیت سیستم در یک لحظه‌ی مشخص</summary>
    public class SystemSnapshot
    {
        public double CpuTotal;
        public double[] CpuCores = Array.Empty<double>();
        /// <summary>سرعت لحظه‌ای CPU به مگاهرتز (-1 = ناموجود)</summary>
        public double CpuMhz = -1;
        /// <summary>دمای CPU/سیستم به سانتی‌گراد (-1 = ناموجود)</summary>
        public double CpuTempC = -1;
        /// <summary>دمای GPU اگر ویندوز گزارش کند (-1 = ناموجود)</summary>
        public double GpuTempC = -1;

        public double MemUsedGB;
        public double MemTotalGB;
        public double MemAvailableGB;
        public double MemPercent;
        public double PageFilePercent;

        public double DiskPercent;
        public double DiskReadMBs;
        public double DiskWriteMBs;

        public double NetRecvKBs;
        public double NetSentKBs;

        /// <summary>-1 یعنی در دسترس نیست (بیشترین مصرف بین همه‌ی GPUها)</summary>
        public double GpuPercent = -1;
        public double GpuMemMB = -1;

        /// <summary>لیست همه‌ی کارت‌های گرافیک (شامل GPU داخلی Intel)</summary>
        public List<GpuStat> Gpus = new();

        public double ProcessCount = -1;
        public double ThreadCount = -1;
        public double UptimeSeconds = -1;
    }

    /// <summary>
    /// خواندن آمار زنده‌ی سیستم از طریق Performance Counter های ویندوز.
    /// همه‌ی بخش‌ها در try/catch هستند چون روی بعضی سیستم‌ها بعضی شمارنده‌ها وجود ندارند.
    /// </summary>
    public class SystemMonitor : IDisposable
    {
        /// <summary>یک نمونه‌ی سراسری مشترک بین همه‌ی صفحات و آیکون Tray</summary>
        public static SystemMonitor Instance { get; } = new();

        private sealed class GpuCounter
        {
            public string Instance = "";
            public PerformanceCounter Counter = null!;
            public int Phys;
            public string EngType = "";
            /// <summary>PID پردازه‌ای که این موتور GPU مال اوست (از نام شمارنده: pid_1234_...)</summary>
            public int Pid = -1;
        }

        private PerformanceCounter? _cpuTotal;
        private readonly List<PerformanceCounter> _cpuCores = new();
        private PerformanceCounter? _cpuPerf;
        private PerformanceCounter? _pageFile;
        private PerformanceCounter? _diskTime;
        private PerformanceCounter? _diskRead;
        private PerformanceCounter? _diskWrite;
        private readonly List<PerformanceCounter> _netRecv = new();
        private readonly List<PerformanceCounter> _netSent = new();
        private readonly List<GpuCounter> _gpuEngines = new();
        private readonly List<GpuCounter> _gpuMems = new();
        private readonly List<(string Name, PerformanceCounter Counter)> _thermal = new();
        private PerformanceCounter? _procCount;
        private PerformanceCounter? _threadCount;
        private PerformanceCounter? _uptime;

        private List<string> _gpuNames = new();
        private double _cpuBaseMhz = -1;
        private int _reads;
        private double _lastWmiTemp = -1;

        // چند صفحه/تایمر ممکن است همزمان Read را صدا بزنند (مثلاً Tray + Processes)
        private readonly object _readLock = new();

        // مصرف GPU هر پردازه (PID ← درصد) — در هر Read به‌روز می‌شود
        private volatile Dictionary<int, double> _gpuByPid = new();

        /// <summary>مصرف GPU هر پردازه (برای ستون GPU در صفحه‌ی Processes)</summary>
        public IReadOnlyDictionary<int, double> GpuByPid => _gpuByPid;

        public SystemMonitor()
        {
            try
            {
                _cpuTotal = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                for (int i = 0; i < Environment.ProcessorCount; i++)
                    _cpuCores.Add(new PerformanceCounter("Processor", "% Processor Time", i.ToString(), true));
            }
            catch { }

            // سرعت لحظه‌ای CPU (درصد عملکرد × فرکانس پایه)
            try { _cpuPerf = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", true); } catch { }

            try { _pageFile = new PerformanceCounter("Paging File", "% Usage", "_Total", true); } catch { }

            try
            {
                _diskTime = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true);
                _diskRead = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", true);
                _diskWrite = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", true);
            }
            catch { }

            try
            {
                var netCat = new PerformanceCounterCategory("Network Interface");
                foreach (var inst in netCat.GetInstanceNames())
                {
                    _netRecv.Add(new PerformanceCounter("Network Interface", "Bytes Received/sec", inst, true));
                    _netSent.Add(new PerformanceCounter("Network Interface", "Bytes Sent/sec", inst, true));
                }
            }
            catch { }

            // دما: Thermal Zone های ویندوز (دما به کلوین گزارش می‌شود)
            try
            {
                var thermalCat = new PerformanceCounterCategory("Thermal Zone Information");
                foreach (var inst in thermalCat.GetInstanceNames())
                {
                    try { _thermal.Add((inst, new PerformanceCounter("Thermal Zone Information", "Temperature", inst, true))); }
                    catch { }
                }
            }
            catch { }

            // آمار کلی سیستم: تعداد پردازش‌ها، تردها و مدت روشن بودن سیستم
            try { _procCount = new PerformanceCounter("System", "Processes", true); } catch { }
            try { _threadCount = new PerformanceCounter("System", "Threads", true); } catch { }
            try { _uptime = new PerformanceCounter("System", "System Up Time", true); } catch { }

            // GPU: همه‌ی کارت‌ها (شامل Intel داخلی) بر اساس شماره‌ی فیزیکی در نام شمارنده (phys_0, phys_1, ...)
            RefreshGpuInstances();

            // نام کارت‌های گرافیک و فرکانس پایه‌ی CPU از WMI (فقط یک بار)
            try { _gpuNames = HardwareInfo.GetGpuNames(); } catch { }
            try { _cpuBaseMhz = HardwareInfo.GetCpuBaseMHz(); } catch { }

            // اولین خواندن هر شمارنده همیشه صفر برمی‌گرداند؛ یک بار می‌خوانیم تا «گرم» شوند.
            try { Read(); } catch { }
        }

        /// <summary>
        /// شمارنده‌های GPU به ازای هر پردازش ساخته می‌شوند و مدام عوض می‌شوند؛
        /// این متد فقط موردهای جدید را اضافه و مرده‌ها را حذف می‌کند (بدون ریست شدن بقیه).
        /// </summary>
        private void RefreshGpuInstances()
        {
            try
            {
                var cat = new PerformanceCounterCategory("GPU Engine");
                var current = new HashSet<string>(cat.GetInstanceNames());

                for (int i = _gpuEngines.Count - 1; i >= 0; i--)
                {
                    if (!current.Contains(_gpuEngines[i].Instance))
                    {
                        _gpuEngines[i].Counter.Dispose();
                        _gpuEngines.RemoveAt(i);
                    }
                }

                var known = new HashSet<string>(_gpuEngines.Select(x => x.Instance));
                foreach (var inst in current)
                {
                    if (known.Contains(inst)) continue;
                    int phys = ParsePhys(inst);
                    if (phys < 0) continue;
                    try
                    {
                        _gpuEngines.Add(new GpuCounter
                        {
                            Instance = inst,
                            Phys = phys,
                            EngType = ParseEngType(inst),
                            Pid = ParsePid(inst),
                            Counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst, true),
                        });
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                var cat = new PerformanceCounterCategory("GPU Adapter Memory");
                var current = new HashSet<string>(cat.GetInstanceNames());

                for (int i = _gpuMems.Count - 1; i >= 0; i--)
                {
                    if (!current.Contains(_gpuMems[i].Instance))
                    {
                        _gpuMems[i].Counter.Dispose();
                        _gpuMems.RemoveAt(i);
                    }
                }

                var known = new HashSet<string>(_gpuMems.Select(x => x.Instance));
                foreach (var inst in current)
                {
                    if (known.Contains(inst)) continue;
                    int phys = Math.Max(ParsePhys(inst), 0);
                    try
                    {
                        _gpuMems.Add(new GpuCounter
                        {
                            Instance = inst,
                            Phys = phys,
                            Counter = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", inst, true),
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>استخراج شماره‌ی فیزیکی GPU از نام شمارنده (مثلاً ...phys_0_engtype_3D)</summary>
        private static int ParsePhys(string instance)
        {
            int idx = instance.IndexOf("phys_", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return -1;
            int start = idx + 5, end = start;
            while (end < instance.Length && char.IsDigit(instance[end])) end++;
            return int.TryParse(instance.Substring(start, end - start), out var n) ? n : -1;
        }

        private static string ParseEngType(string instance)
        {
            int idx = instance.IndexOf("engtype_", StringComparison.OrdinalIgnoreCase);
            return idx < 0 ? "" : instance.Substring(idx + 8);
        }

        /// <summary>استخراج PID از نام شمارنده (مثلاً pid_1234_luid_..._phys_0_engtype_3D)</summary>
        private static int ParsePid(string instance)
        {
            if (!instance.StartsWith("pid_", StringComparison.OrdinalIgnoreCase)) return -1;
            int start = 4, end = start;
            while (end < instance.Length && char.IsDigit(instance[end])) end++;
            return int.TryParse(instance.Substring(start, end - start), out var n) ? n : -1;
        }

        public SystemSnapshot Read()
        {
            // چند صفحه ممکن است همزمان بخوانند؛ با قفل از تداخل جلوگیری می‌کنیم
            lock (_readLock)
            {
                return ReadCore();
            }
        }

        private SystemSnapshot ReadCore()
        {
            _reads++;
            // هر 5 خواندن یک بار، لیست شمارنده‌های GPU را به‌روز کن (پردازش‌ها مدام باز/بسته می‌شوند)
            if (_reads % 5 == 0) RefreshGpuInstances();

            var s = new SystemSnapshot();

            try { s.CpuTotal = Math.Min(_cpuTotal?.NextValue() ?? 0, 100); } catch { }

            var cores = new List<double>();
            foreach (var c in _cpuCores)
            {
                try { cores.Add(Math.Min(c.NextValue(), 100)); }
                catch { cores.Add(0); }
            }
            s.CpuCores = cores.ToArray();

            // سرعت لحظه‌ای CPU
            try
            {
                double perf = _cpuPerf?.NextValue() ?? 0;
                if (_cpuBaseMhz > 0 && perf > 0)
                    s.CpuMhz = _cpuBaseMhz * perf / 100.0;
            }
            catch { }

            try { s.PageFilePercent = _pageFile?.NextValue() ?? 0; } catch { }

            try
            {
                s.DiskPercent = Math.Min(_diskTime?.NextValue() ?? 0, 100);
                s.DiskReadMBs = (_diskRead?.NextValue() ?? 0) / (1024.0 * 1024.0);
                s.DiskWriteMBs = (_diskWrite?.NextValue() ?? 0) / (1024.0 * 1024.0);
            }
            catch { }

            try
            {
                double recv = 0, sent = 0;
                foreach (var c in _netRecv) recv += c.NextValue();
                foreach (var c in _netSent) sent += c.NextValue();
                s.NetRecvKBs = recv / 1024.0;
                s.NetSentKBs = sent / 1024.0;
            }
            catch { }

            // ==== GPU ها: مصرف هر کارت به تفکیک + مصرف هر پردازه ====
            try
            {
                var engineSums = new Dictionary<(int Phys, string Eng), double>();
                var pidEng = new Dictionary<(int Pid, string Eng), double>();

                foreach (var g in _gpuEngines)
                {
                    try
                    {
                        double v = g.Counter.NextValue();
                        var key = (g.Phys, g.EngType);
                        engineSums[key] = engineSums.GetValueOrDefault(key) + v;

                        if (g.Pid > 0)
                        {
                            var pk = (g.Pid, g.EngType);
                            pidEng[pk] = pidEng.GetValueOrDefault(pk) + v;
                        }
                    }
                    catch { }
                }

                var usageByPhys = new Dictionary<int, double>();
                foreach (var kv in engineSums)
                {
                    if (!usageByPhys.TryGetValue(kv.Key.Phys, out var cur) || kv.Value > cur)
                        usageByPhys[kv.Key.Phys] = kv.Value;
                }

                // مصرف GPU هر پردازه = بیشینه‌ی مصرف بین موتورها (مثل Task Manager ویندوز)
                var byPid = new Dictionary<int, double>();
                foreach (var kv in pidEng)
                {
                    if (!byPid.TryGetValue(kv.Key.Pid, out var cur) || kv.Value > cur)
                        byPid[kv.Key.Pid] = kv.Value;
                }
                _gpuByPid = byPid;

                var memByPhys = new Dictionary<int, double>();
                foreach (var g in _gpuMems)
                {
                    try { memByPhys[g.Phys] = memByPhys.GetValueOrDefault(g.Phys) + g.Counter.NextValue(); }
                    catch { }
                }

                // همه‌ی GPU های شناخته‌شده حتی اگر شمارنده‌ای نداشته باشند در لیست می‌آیند
                // (تا هر دو کارت گرافیک همیشه جدا نمایش داده شوند)
                var physSet = new HashSet<int>(usageByPhys.Keys);
                foreach (var k in memByPhys.Keys) physSet.Add(k);
                foreach (var k in engineSums.Keys) physSet.Add(k.Phys);
                for (int i = 0; i < _gpuNames.Count; i++) physSet.Add(i);

                foreach (var p in physSet.OrderBy(x => x))
                {
                    var stat = new GpuStat
                    {
                        PhysIndex = p,
                        // ترتیب نام‌ها در WMI معمولاً با شماره‌ی فیزیکی یکی است (بهترین حدس)
                        Name = p < _gpuNames.Count ? _gpuNames[p] : $"GPU {p}",
                        UsagePercent = Math.Min(usageByPhys.GetValueOrDefault(p), 100),
                        DedicatedMB = memByPhys.GetValueOrDefault(p) / (1024.0 * 1024.0),
                    };

                    // مصرف هر موتور این کارت (برای گراف‌های موتور GPU در صفحه‌ی Performance)
                    foreach (var kv in engineSums)
                        if (kv.Key.Phys == p)
                            stat.Engines[kv.Key.Eng] = Math.Min(kv.Value, 100);

                    s.Gpus.Add(stat);
                }

                if (s.Gpus.Count > 0)
                {
                    s.GpuPercent = s.Gpus.Max(g => g.UsagePercent);
                    s.GpuMemMB = s.Gpus.Sum(g => g.DedicatedMB);
                }
            }
            catch { }

            // ==== دما ====
            try
            {
                double maxAll = -1, maxGpu = -1;
                foreach (var (name, counter) in _thermal)
                {
                    try
                    {
                        double c = counter.NextValue() - 273.15; // کلوین ← سانتی‌گراد
                        if (c < -30 || c > 150) continue;        // مقدارهای بی‌معنی را نادیده بگیر
                        if (c > maxAll) maxAll = c;
                        if (name.Contains("gpu", StringComparison.OrdinalIgnoreCase) && c > maxGpu) maxGpu = c;
                    }
                    catch { }
                }

                // اگر Thermal Zone جواب نداد، هر 5 ثانیه یک بار از WMI امتحان کن (روی بعضی سیستم‌ها نیاز به Admin دارد)
                if (maxAll <= 0 && _reads % 5 == 1)
                    _lastWmiTemp = ReadWmiTemperature();

                double lhmTemp = (maxAll <= 0 && _lastWmiTemp <= 0) ? SensorMonitor.ReadCpuTemp() : -1;
                s.CpuTempC = maxAll > 0 ? maxAll : (_lastWmiTemp > 0 ? _lastWmiTemp : lhmTemp);
                s.GpuTempC = maxGpu;
            }
            catch { }

            // ==== آمار کلی سیستم ====
            try { s.ProcessCount = _procCount?.NextValue() ?? -1; } catch { }
            try { s.ThreadCount = _threadCount?.NextValue() ?? -1; } catch { }
            try { s.UptimeSeconds = _uptime?.NextValue() ?? -1; } catch { }

            // رم از طریق API خود ویندوز (دقیق و سریع)
            var memStat = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref memStat))
            {
                s.MemTotalGB = memStat.ullTotalPhys / (1024.0 * 1024 * 1024);
                s.MemAvailableGB = memStat.ullAvailPhys / (1024.0 * 1024 * 1024);
                s.MemUsedGB = (memStat.ullTotalPhys - memStat.ullAvailPhys) / (1024.0 * 1024 * 1024);
                s.MemPercent = memStat.dwMemoryLoad;
            }

            return s;
        }

        /// <summary>دمای منطقه‌ی حرارتی از WMI (ممکن است روی بعضی دستگاه‌ها فقط با Admin کار کند)</summary>
        private static double ReadWmiTemperature()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI",
                    "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                double max = -1;
                foreach (ManagementObject o in searcher.Get())
                {
                    double c = Convert.ToDouble(o["CurrentTemperature"]) / 10.0 - 273.15;
                    if (c > max && c > -30 && c < 150) max = c;
                }
                return max;
            }
            catch { return -1; }
        }

        public void Dispose()
        {
            _cpuTotal?.Dispose();
            foreach (var c in _cpuCores) c.Dispose();
            _cpuPerf?.Dispose();
            _pageFile?.Dispose();
            _diskTime?.Dispose();
            _diskRead?.Dispose();
            _diskWrite?.Dispose();
            foreach (var c in _netRecv) c.Dispose();
            foreach (var c in _netSent) c.Dispose();
            foreach (var g in _gpuEngines) g.Counter.Dispose();
            foreach (var g in _gpuMems) g.Counter.Dispose();
            foreach (var (_, c) in _thermal) c.Dispose();
            _procCount?.Dispose();
            _threadCount?.Dispose();
            _uptime?.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    }
}
