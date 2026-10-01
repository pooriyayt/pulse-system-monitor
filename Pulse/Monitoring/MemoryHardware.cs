using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace TaskManagerPro.Monitoring
{
    /// <summary>یک ماژول رم نصب‌شده</summary>
    public sealed class RamModule
    {
        public string Slot = "";
        public ulong CapacityBytes;
        public uint SpeedMHz;
        public uint ConfiguredMHz;
        public string Type = "";
        public string FormFactor = "";
        public string Manufacturer = "";
        public string PartNumber = "";
    }

    /// <summary>مشخصات ثابت رم (یک بار از WMI خوانده و کش می‌شود)</summary>
    public sealed class RamInfo
    {
        public List<RamModule> Modules = new();
        /// <summary>تعداد کل اسلات‌های مادربرد (0 = نامشخص)</summary>
        public int TotalSlots;
        public int UsedSlots => Modules.Count;
        public string Type => Modules.Select(m => m.Type).FirstOrDefault(t => t.Length > 0) ?? "";
        public string FormFactor => Modules.Select(m => m.FormFactor).FirstOrDefault(t => t.Length > 0) ?? "";
        /// <summary>سرعت فعلی (تنظیم‌شده) — اگر نبود، سرعت نامی</summary>
        public uint SpeedMHz => Modules.Select(m => m.ConfiguredMHz > 0 ? m.ConfiguredMHz : m.SpeedMHz).DefaultIfEmpty(0u).Max();
    }

    /// <summary>آمار زنده‌ی حافظه‌ی سیستم (GetPerformanceInfo)</summary>
    public readonly struct MemoryCounters
    {
        public readonly ulong CommitTotal, CommitLimit, SystemCache, PagedPool, NonPagedPool;
        public MemoryCounters(ulong ct, ulong cl, ulong sc, ulong pp, ulong np)
        { CommitTotal = ct; CommitLimit = cl; SystemCache = sc; PagedPool = pp; NonPagedPool = np; }
    }

    public static class MemoryHardware
    {
        private static RamInfo? _info;

        /// <summary>[ترد پس‌زمینه] مشخصات رم — بار اول از WMI، بعد از کش</summary>
        public static RamInfo GetInfo()
        {
            if (_info != null) return _info;
            var info = new RamInfo();
            try
            {
                using var s = new ManagementObjectSearcher(
                    "SELECT DeviceLocator, Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType, MemoryType, FormFactor, Manufacturer, PartNumber FROM Win32_PhysicalMemory");
                foreach (ManagementObject o in s.Get())
                {
                    info.Modules.Add(new RamModule
                    {
                        Slot = (o["DeviceLocator"] as string ?? "").Trim(),
                        CapacityBytes = ToULong(o["Capacity"]),
                        SpeedMHz = (uint)ToULong(o["Speed"]),
                        ConfiguredMHz = (uint)ToULong(o["ConfiguredClockSpeed"]),
                        Type = MemoryTypeName((uint)ToULong(o["SMBIOSMemoryType"]), (uint)ToULong(o["MemoryType"])),
                        FormFactor = FormFactorName((uint)ToULong(o["FormFactor"])),
                        Manufacturer = (o["Manufacturer"] as string ?? "").Trim(),
                        PartNumber = (o["PartNumber"] as string ?? "").Trim(),
                    });
                }
            }
            catch { }

            try
            {
                using var s = new ManagementObjectSearcher("SELECT MemoryDevices FROM Win32_PhysicalMemoryArray");
                foreach (ManagementObject o in s.Get())
                    info.TotalSlots += (int)ToULong(o["MemoryDevices"]);
            }
            catch { }
            // بعضی بایوس‌ها تعداد اسلات را کمتر از ماژول‌های نصب‌شده گزارش می‌کنند
            if (info.TotalSlots < info.Modules.Count) info.TotalSlots = info.Modules.Count;

            _info = info;
            return info;
        }

        /// <summary>آمار زنده: Committed / سقف Commit / کش / Paged و Non-paged pool</summary>
        public static MemoryCounters? GetCounters()
        {
            var p = new PERFORMANCE_INFORMATION { cb = (uint)Marshal.SizeOf<PERFORMANCE_INFORMATION>() };
            if (!GetPerformanceInfo(out p, p.cb)) return null;
            ulong page = (ulong)p.PageSize;
            return new MemoryCounters(
                (ulong)p.CommitTotal * page, (ulong)p.CommitLimit * page, (ulong)p.SystemCache * page,
                (ulong)p.KernelPaged * page, (ulong)p.KernelNonpaged * page);
        }

        /// <summary>حجم رم فشرده‌شده (Working set پردازه‌ی Memory Compression) یا -1</summary>
        public static long GetCompressedBytes()
        {
            try
            {
                foreach (var pr in System.Diagnostics.Process.GetProcessesByName("Memory Compression"))
                    using (pr) return pr.WorkingSet64;
            }
            catch { }
            return -1;
        }

        private static ulong ToULong(object? v)
        {
            try { return v == null ? 0 : Convert.ToUInt64(v); } catch { return 0; }
        }

        /// <summary>نوع رم از کد SMBIOS (و در نبودش کد قدیمی WMI)</summary>
        private static string MemoryTypeName(uint smbios, uint legacy) => smbios switch
        {
            20 => "DDR",
            21 => "DDR2",
            24 => "DDR3",
            26 => "DDR4",
            27 => "LPDDR",
            28 => "LPDDR2",
            29 => "LPDDR3",
            30 => "LPDDR4",
            34 => "DDR5",
            35 => "LPDDR5",
            _ => legacy switch { 20 => "DDR", 21 => "DDR2", 24 => "DDR3", 26 => "DDR4", _ => "" },
        };

        private static string FormFactorName(uint f) => f switch
        {
            8 => "DIMM",
            12 => "SODIMM",
            13 => "SRIMM",
            _ => "",
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct PERFORMANCE_INFORMATION
        {
            public uint cb;
            public UIntPtr CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable, SystemCache,
                KernelTotal, KernelPaged, KernelNonpaged, PageSize;
            public uint HandleCount, ProcessCount, ThreadCount;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION info, uint size);
    }
}
