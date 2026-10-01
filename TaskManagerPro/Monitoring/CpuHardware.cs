using System;
using System.Management;

namespace TaskManagerPro.Monitoring
{
    /// <summary>مشخصات ثابت پردازنده (یک بار از WMI خوانده و کش می‌شود)</summary>
    public sealed class CpuInfo
    {
        public int Sockets;
        public int Cores;
        public int LogicalProcessors;
        public uint BaseMHz;
        public uint L1KB, L2KB, L3KB;
        /// <summary>"Enabled" / "Disabled" / "" (نامشخص)</summary>
        public string Virtualization = "";
    }

    public static class CpuHardware
    {
        private static CpuInfo? _info;

        /// <summary>[ترد پس‌زمینه] مشخصات CPU — بار اول از WMI، بعد از کش</summary>
        public static CpuInfo GetInfo()
        {
            if (_info != null) return _info;
            var info = new CpuInfo();
            bool? vtFirmware = null;
            try
            {
                using var s = new ManagementObjectSearcher(
                    "SELECT NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L2CacheSize, L3CacheSize, VirtualizationFirmwareEnabled FROM Win32_Processor");
                foreach (ManagementObject o in s.Get())
                {
                    info.Sockets++;
                    info.Cores += (int)ToUInt(o["NumberOfCores"]);
                    info.LogicalProcessors += (int)ToUInt(o["NumberOfLogicalProcessors"]);
                    info.BaseMHz = Math.Max(info.BaseMHz, ToUInt(o["MaxClockSpeed"]));
                    info.L2KB += ToUInt(o["L2CacheSize"]);
                    info.L3KB += ToUInt(o["L3CacheSize"]);
                    if (o["VirtualizationFirmwareEnabled"] is bool vt) vtFirmware = (vtFirmware ?? false) || vt;
                }
            }
            catch { }

            // کش L1 از Win32_CacheMemory (Level=3 یعنی L1 در این کلاس)
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Level, MaxCacheSize FROM Win32_CacheMemory");
                foreach (ManagementObject o in s.Get())
                    if (ToUInt(o["Level"]) == 3) info.L1KB += ToUInt(o["MaxCacheSize"]);
            }
            catch { }

            // وقتی Hyper-V فعال است، ویندوز VirtualizationFirmwareEnabled را false گزارش می‌کند؛
            // در آن حالت وجود Hypervisor یعنی مجازی‌سازی روشن است (Task Manager ویندوز هم همین کار را می‌کند)
            bool hypervisor = false;
            try
            {
                using var s = new ManagementObjectSearcher("SELECT HypervisorPresent FROM Win32_ComputerSystem");
                foreach (ManagementObject o in s.Get())
                    if (o["HypervisorPresent"] is bool h) hypervisor = h;
            }
            catch { }

            info.Virtualization = hypervisor || vtFirmware == true ? "Enabled"
                : vtFirmware == false ? "Disabled" : "";

            _info = info;
            return info;
        }

        private static uint ToUInt(object? v)
        {
            try { return v == null ? 0 : Convert.ToUInt32(v); } catch { return 0; }
        }
    }
}
