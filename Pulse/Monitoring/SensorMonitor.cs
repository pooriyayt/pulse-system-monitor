using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using LibreHardwareMonitor.Hardware;

namespace TaskManagerPro.Monitoring
{
    /// <summary>یک سنسور سخت‌افزاری (دما / فن / ولتاژ / توان / کلاک)</summary>
    public class SensorReading
    {
        public string Hardware = "";
        public string Name = "";
        public string Kind = "";
        public double Value;
        public string Unit = "";

        public string ValueText => Unit switch
        {
            "°C" => $"{Value:F0} °C",
            "RPM" => $"{Value:F0} RPM",
            "V" => $"{Value:F2} V",
            "W" => $"{Value:F1} W",
            "MHz" => $"{Value:F0} MHz",
            _ => $"{Value:F1} {Unit}",
        };
    }

    /// <summary>
    /// سنسورهای سخت‌افزار (دما / فن / ولتاژ / توان) با LibreHardwareMonitor —
    /// کاملاً لوکال و آفلاین، بدون سرویس.
    ///
    /// این نسخه هیچ درایور کرنلی را همراه خود ندارد و نصب/بارگذاری نمی‌کند:
    /// LibreHardwareMonitorLib 0.9.6 دیگر درایور WinRing0 را جاسازی نمی‌کند و
    /// داده‌ها را از مسیرهای user-mode می‌گیرد (NVAPI/NVML برای NVIDIA،
    /// ADL برای AMD، D3DKMT برای بقیه‌ی GPUها، SMART برای دیسک‌ها).
    /// سنسورهای مادربرد/MSR فقط اگر درایور امضاشده‌ی PawnIO از قبل روی سیستم
    /// نصب باشد در دسترس‌اند؛ در غیر این صورت به‌جای مقدار جعلی، چیزی نشان داده نمی‌شود.
    ///
    /// اگر هیچ سنسوری از سخت‌افزار نیامد، به‌عنوان جایگزین از Thermal Zone های
    /// خود ویندوز (ACPI) استفاده می‌شود که به هیچ درایور اضافه‌ای نیاز ندارد.
    /// </summary>
    public static class SensorMonitor
    {
        private static Computer? _pc;
        private static readonly object Lock = new();

        /// <summary>شمارنده‌های دمای ACPI ویندوز — جایگزین بدون درایور</summary>
        private static readonly List<(string Name, PerformanceCounter Counter)> Zones = new();
        private static bool _zonesInit;

        public static bool IsStarted { get; private set; }

        /// <summary>true یعنی تلاش برای باز کردن سنسورها انجام شده (موفق یا ناموفق)</summary>
        public static bool StartAttempted { get; private set; }

        /// <summary>true یعنی باز کردن سنسورها شکست خورد — سیستم پشتیبانی نمی‌کند</summary>
        public static bool Failed { get; private set; }

        /// <summary>پیام خطای باز کردن سنسورها (برای عیب‌یابی)</summary>
        public static string FailureMessage { get; private set; } = "";

        /// <summary>باز کردن دسترسی به سخت‌افزار (کُند است — فقط در ترد پس‌زمینه)</summary>
        public static void Start()
        {
            lock (Lock)
            {
                if (IsStarted) return;
                try
                {
                    _pc = new Computer
                    {
                        IsCpuEnabled = true,
                        IsGpuEnabled = true,
                        IsMemoryEnabled = true,
                        IsMotherboardEnabled = true,
                        IsStorageEnabled = true,
                    };
                    _pc.Open();

                    // اگر هیچ سخت‌افزاری برنگشت، عملاً چیزی برای نمایش نداریم
                    bool any = false;
                    try { foreach (var _ in _pc.Hardware) { any = true; break; } } catch { }
                    if (!any)
                    {
                        try { _pc.Close(); } catch { }
                        _pc = null;
                        Failed = true;
                        FailureMessage = "No hardware reported by the sensor library.";
                    }
                    else
                    {
                        IsStarted = true;
                        Failed = false;
                    }
                }
                catch (Exception ex)
                {
                    try { _pc?.Close(); } catch { }
                    _pc = null;
                    Failed = true;
                    FailureMessage = ex.Message;
                }
                finally
                {
                    // جایگزین بدون درایور همیشه آماده می‌شود — حتی اگر بالا شکست خورده باشد
                    InitZones();
                    if (Zones.Count > 0)
                    {
                        IsStarted = true;
                        Failed = false;
                        FailureMessage = "";
                    }
                    StartAttempted = true;
                }
            }
        }

        /// <summary>تلاش دوباره (مثلاً بعد از Run as administrator)</summary>
        public static void Retry()
        {
            lock (Lock)
            {
                if (_pc != null) return;
                IsStarted = false;
                StartAttempted = false;
                Failed = false;
                FailureMessage = "";
            }
            Start();
        }

        /// <summary>خواندن همه‌ی سنسورهای معنی‌دار (در ترد پس‌زمینه)</summary>
        public static List<SensorReading> Read()
        {
            var list = new List<SensorReading>();
            lock (Lock)
            {
                bool hasCpuTemp = false;
                if (_pc != null)
                {
                    try
                    {
                        foreach (var hw in _pc.Hardware)
                        {
                            try
                            {
                                hw.Update();
                                int before = list.Count;
                                Collect(hw, hw.Name, list);
                                foreach (var sub in hw.SubHardware)
                                {
                                    try { sub.Update(); Collect(sub, hw.Name, list); } catch { }
                                }
                                if (hw.HardwareType == HardwareType.Cpu)
                                    for (int i = before; i < list.Count; i++)
                                        if (list[i].Kind == "Temperature") { hasCpuTemp = true; break; }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                // اگر CPU دمایی نداد (مثلاً بدون درایور PawnIO دسترسی به MSR نداریم)،
                // دست‌کم دمای ACPI خود ویندوز را نشان بده
                if (!hasCpuTemp)
                {
                    foreach (var (name, value) in ReadZones())
                    {
                        list.Add(new SensorReading
                        {
                            Hardware = "System (ACPI)",
                            Name = name,
                            Kind = "Temperature",
                            Value = value,
                            Unit = "°C",
                        });
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// فقط دمای CPU — سبک‌تر از Read کامل، fallback برای SystemMonitor.
        /// -1 یعنی در دسترس نیست.
        /// </summary>
        public static double ReadCpuTemp()
        {
            double max = -1;
            lock (Lock)
            {
                if (_pc != null)
                {
                    try
                    {
                        foreach (var hw in _pc.Hardware)
                        {
                            if (hw.HardwareType != HardwareType.Cpu) continue;
                            try
                            {
                                hw.Update();
                                foreach (var s in hw.Sensors)
                                    if (s.SensorType == SensorType.Temperature && s.Value is float v && !float.IsNaN(v) && v > max)
                                        max = v;
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                if (max <= 0)
                {
                    foreach (var (_, value) in ReadZones())
                        if (value > max) max = value;
                }
            }
            return max;
        }

        /// <summary>
        /// فقط دمای GPU — سبک‌تر از Read کامل، برای تب GPU در Performance.
        /// -1 یعنی در دسترس نیست.
        /// </summary>
        public static double ReadGpuTemp()
        {
            double max = -1;
            lock (Lock)
            {
                if (_pc == null) return -1;
                try
                {
                    foreach (var hw in _pc.Hardware)
                    {
                        if (hw.HardwareType is not (HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel))
                            continue;
                        try
                        {
                            hw.Update();
                            foreach (var s in hw.Sensors)
                                if (s.SensorType == SensorType.Temperature && s.Value is float v && !float.IsNaN(v) && v > max)
                                    max = v;
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return max;
        }

        private static void Collect(IHardware hw, string hwName, List<SensorReading> list)
        {
            foreach (var s in hw.Sensors)
            {
                if (s.Value is not float v || float.IsNaN(v)) continue;

                (string kind, string unit) = s.SensorType switch
                {
                    SensorType.Temperature => ("Temperature", "°C"),
                    SensorType.Fan => ("Fan", "RPM"),
                    SensorType.Voltage => ("Voltage", "V"),
                    SensorType.Power => ("Power", "W"),
                    SensorType.Clock => ("Clock", "MHz"),
                    _ => ("", ""),
                };
                if (kind.Length == 0) continue;

                list.Add(new SensorReading
                {
                    Hardware = hwName,
                    Name = s.Name,
                    Kind = kind,
                    Value = v,
                    Unit = unit,
                });
            }
        }

        // ---------- جایگزین بدون درایور: Thermal Zone های ویندوز ----------

        /// <summary>ساخت شمارنده‌های دمای ACPI (فقط یک بار — داخل قفل صدا زده می‌شود)</summary>
        private static void InitZones()
        {
            if (_zonesInit) return;
            _zonesInit = true;
            try
            {
                var cat = new PerformanceCounterCategory("Thermal Zone Information");
                foreach (var inst in cat.GetInstanceNames())
                {
                    try { Zones.Add((ShortZoneName(inst), new PerformanceCounter("Thermal Zone Information", "Temperature", inst, true))); }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>نام کوتاه و خوانا از مسیر طولانی ACPI (مثلاً \_TZ.TZ00)</summary>
        private static string ShortZoneName(string instance)
        {
            int i = instance.LastIndexOf('_');
            string s = i >= 0 && i + 1 < instance.Length ? instance.Substring(i + 1) : instance;
            return $"Thermal zone {s}";
        }

        /// <summary>دمای منطقه‌های حرارتی به سانتی‌گراد (مقدارهای بی‌معنی حذف می‌شوند)</summary>
        private static List<(string Name, double Value)> ReadZones()
        {
            var list = new List<(string, double)>();
            foreach (var (name, counter) in Zones)
            {
                try
                {
                    double c = counter.NextValue() - 273.15; // کلوین ← سانتی‌گراد
                    if (c < -30 || c > 150) continue;
                    list.Add((name, c));
                }
                catch { }
            }

            // اگر شمارنده‌ها چیزی ندادند، یک بار از WMI امتحان کن (روی بعضی سیستم‌ها فقط با Admin)
            if (list.Count == 0)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\WMI",
                        "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                    int n = 0;
                    foreach (ManagementObject o in searcher.Get())
                    {
                        double c = Convert.ToDouble(o["CurrentTemperature"]) / 10.0 - 273.15;
                        if (c < -30 || c > 150) continue;
                        string name = o["InstanceName"]?.ToString() ?? $"TZ{n}";
                        list.Add((ShortZoneName(name), c));
                        n++;
                    }
                }
                catch { }
            }

            return list;
        }
    }
}
