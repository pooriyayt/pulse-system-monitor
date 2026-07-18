using System;
using System.Collections.Generic;
using System.Management;
using System.Text;

namespace TaskManagerPro.Monitoring
{
    /// <summary>
    /// خواندن مشخصات سخت‌افزار از طریق WMI (سیستم مدیریت ویندوز).
    /// این داده‌ها ثابتند و فقط یک بار خوانده می‌شوند.
    /// </summary>
    public static class HardwareInfo
    {
        public static string GetCpuName()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (ManagementObject o in searcher.Get())
                    return o["Name"]?.ToString()?.Trim() ?? "CPU";
            }
            catch { }
            return "CPU";
        }

        /// <summary>نام همه‌ی کارت‌های گرافیک (شامل GPU داخلی Intel/AMD و کارت مجزا)</summary>
        public static List<string> GetGpuNames()
        {
            var list = new List<string>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                foreach (ManagementObject o in searcher.Get())
                {
                    var n = o["Name"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(n)) list.Add(n);
                }
            }
            catch { }
            return list;
        }

        /// <summary>نام اولین GPU (برای سازگاری)</summary>
        public static string GetGpuName()
        {
            var names = GetGpuNames();
            return names.Count > 0 ? string.Join("  |  ", names) : "GPU";
        }

        /// <summary>فرکانس پایه‌ی CPU به مگاهرتز (برای محاسبه‌ی سرعت لحظه‌ای)</summary>
        public static double GetCpuBaseMHz()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor");
                foreach (ManagementObject o in searcher.Get())
                    return Convert.ToDouble(o["MaxClockSpeed"]);
            }
            catch { }
            return -1;
        }

        /// <summary>خلاصه‌ی کامل سخت‌افزار برای کارت Hardware</summary>
        public static string GetSummary()
        {
            var sb = new StringBuilder();

            try
            {
                using var cpu = new ManagementObjectSearcher(
                    "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
                foreach (ManagementObject o in cpu.Get())
                {
                    double ghz = Convert.ToDouble(o["MaxClockSpeed"]) / 1000.0;
                    sb.AppendLine($"CPU:  {o["Name"]?.ToString()?.Trim()}");
                    sb.AppendLine($"      Cores: {o["NumberOfCores"]}   Threads: {o["NumberOfLogicalProcessors"]}   Max: {ghz:F2} GHz");
                }
            }
            catch { sb.AppendLine("CPU:  N/A"); }

            try
            {
                using var gpu = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                foreach (ManagementObject o in gpu.Get())
                    sb.AppendLine($"GPU:  {o["Name"]?.ToString()?.Trim()}");
            }
            catch { sb.AppendLine("GPU:  N/A"); }

            try
            {
                using var sys = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject o in sys.Get())
                {
                    double gb = Convert.ToUInt64(o["TotalPhysicalMemory"]) / (1024.0 * 1024 * 1024);
                    sb.AppendLine($"RAM:  {gb:F1} GB");
                }
            }
            catch { sb.AppendLine("RAM:  N/A"); }

            try
            {
                using var disks = new ManagementObjectSearcher("SELECT Model, Size FROM Win32_DiskDrive");
                foreach (ManagementObject o in disks.Get())
                {
                    double gb = Convert.ToUInt64(o["Size"] ?? 0UL) / (1024.0 * 1024 * 1024);
                    sb.AppendLine($"Disk: {o["Model"]?.ToString()?.Trim()}   ({gb:F0} GB)");
                }
            }
            catch { sb.AppendLine("Disk: N/A"); }

            return sb.ToString().TrimEnd();
        }
    }
}
