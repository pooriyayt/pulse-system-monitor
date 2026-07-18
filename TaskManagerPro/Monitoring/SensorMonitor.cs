using System;
using System.Collections.Generic;
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
    /// سنسورهای کامل سخت‌افزار (دما / فن / ولتاژ / توان) با LibreHardwareMonitor —
    /// کاملاً لوکال و آفلاین، بدون سرویس. برای بیشترین داده باید Run as administrator باشد.
    /// </summary>
    public static class SensorMonitor
    {
        private static Computer? _pc;
        private static readonly object Lock = new();

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
                        FailureMessage = "No hardware reported by the sensor driver.";
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
                    StartAttempted = true;
                }
            }
        }

        /// <summary>تلاش دوباره (مثلاً بعد از Run as administrator)</summary>
        public static void Retry()
        {
            lock (Lock)
            {
                if (IsStarted) return;
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
                if (_pc == null) return list;
                try
                {
                    foreach (var hw in _pc.Hardware)
                    {
                        try
                        {
                            hw.Update();
                            Collect(hw, hw.Name, list);
                            foreach (var sub in hw.SubHardware)
                            {
                                try { sub.Update(); Collect(sub, hw.Name, list); } catch { }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return list;
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
    }
}
