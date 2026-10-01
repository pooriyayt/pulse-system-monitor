using System;
using System.Collections.Generic;
using System.Management;
using Microsoft.Win32;

namespace TaskManagerPro.Monitoring
{
    /// <summary>مشخصات ثابت یک کارت گرافیک</summary>
    public sealed class GpuInfo
    {
        public string Name = "";
        public string Vendor = "";
        public string DriverVersion = "";
        public DateTime? DriverDate;
        /// <summary>حافظه‌ی اختصاصی واقعی (بایت) — از رجیستری درایور، نه AdapterRAM که سقف ۴ گیگ دارد</summary>
        public ulong DedicatedBytes;
        public bool Integrated;
        public string Resolution = "";
    }

    public static class GpuHardware
    {
        private static List<GpuInfo>? _list;

        /// <summary>[ترد پس‌زمینه] مشخصات همه‌ی GPUها (کش‌شده)</summary>
        public static List<GpuInfo> GetAll()
        {
            if (_list != null) return _list;
            var list = new List<GpuInfo>();
            var vram = ReadRegistryVram();
            try
            {
                using var s = new ManagementObjectSearcher(
                    "SELECT Name, DriverVersion, DriverDate, AdapterRAM, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate, PNPDeviceID FROM Win32_VideoController");
                foreach (ManagementObject o in s.Get())
                {
                    string name = (o["Name"] as string ?? "").Trim();
                    string pnp = o["PNPDeviceID"] as string ?? "";
                    var g = new GpuInfo
                    {
                        Name = name,
                        Vendor = pnp.Contains("VEN_10DE") ? "NVIDIA" : pnp.Contains("VEN_8086") ? "Intel"
                               : pnp.Contains("VEN_1002") ? "AMD" : "",
                        DriverVersion = o["DriverVersion"] as string ?? "",
                    };
                    try
                    {
                        if (o["DriverDate"] is string dd && dd.Length >= 8)
                            g.DriverDate = ManagementDateTimeConverter.ToDateTime(dd);
                    }
                    catch { }

                    g.DedicatedBytes = vram.TryGetValue(name, out var v) && v > 0 ? v : ToULong(o["AdapterRAM"]);

                    // گرافیک مجتمع: Intel، یا حافظه‌ی اختصاصی خیلی کم (APUهای AMD)
                    g.Integrated = g.Vendor == "Intel" || g.DedicatedBytes < 600UL * 1024 * 1024;

                    ulong w = ToULong(o["CurrentHorizontalResolution"]), h = ToULong(o["CurrentVerticalResolution"]);
                    ulong hz = ToULong(o["CurrentRefreshRate"]);
                    if (w > 0 && h > 0) g.Resolution = hz > 1 ? $"{w} × {h} @ {hz} Hz" : $"{w} × {h}";

                    list.Add(g);
                }
            }
            catch { }
            _list = list;
            return list;
        }

        /// <summary>اولین GPU که نامش با نام داده‌شده جور است</summary>
        public static GpuInfo? Find(string name)
        {
            foreach (var g in GetAll())
                if (g.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(g.Name, StringComparison.OrdinalIgnoreCase) ||
                    g.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return g;
            return null;
        }

        /// <summary>حافظه‌ی اختصاصی واقعی هر کارت از رجیستری کلاس Display</summary>
        private static Dictionary<string, ulong> ReadRegistryVram()
        {
            var map = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cls = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                if (cls == null) return map;
                foreach (var sub in cls.GetSubKeyNames())
                {
                    if (sub.Length != 4) continue; // 0000، 0001، ...
                    try
                    {
                        using var k = cls.OpenSubKey(sub);
                        if (k?.GetValue("DriverDesc") is not string desc) continue;
                        ulong size = 0;
                        var q = k.GetValue("HardwareInformation.qwMemorySize");
                        if (q is long l) size = (ulong)l;
                        else if (q is byte[] qb && qb.Length >= 8) size = BitConverter.ToUInt64(qb, 0);
                        if (size == 0)
                        {
                            var d = k.GetValue("HardwareInformation.MemorySize");
                            if (d is int i) size = (uint)i;
                            else if (d is byte[] db && db.Length >= 4) size = BitConverter.ToUInt32(db, 0);
                        }
                        if (size > 0 && (!map.TryGetValue(desc, out var old) || size > old)) map[desc] = size;
                    }
                    catch { }
                }
            }
            catch { }
            return map;
        }

        private static ulong ToULong(object? v)
        {
            try { return v == null ? 0 : Convert.ToUInt64(v); } catch { return 0; }
        }
    }
}
