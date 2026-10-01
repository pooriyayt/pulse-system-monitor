using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;

namespace TaskManagerPro.Monitoring
{
    /// <summary>مشخصات ثابت یک دیسک فیزیکی</summary>
    public sealed class DiskInfo
    {
        public int Index;
        public string Model = "";
        public string Brand = "";
        /// <summary>SSD / HDD / ""</summary>
        public string MediaType = "";
        /// <summary>NVMe / SATA / USB / ...</summary>
        public string Bus = "";
        public ulong SizeBytes;
        public string Firmware = "";
        /// <summary>Healthy / Warning / Unhealthy / ""</summary>
        public string Health = "";
        public uint SpindleRpm;
        /// <summary>نسل PCIe لینک فعلی (۳ = Gen3، ۴ = Gen4 ...) — ۰ یعنی نامشخص/غیر PCIe</summary>
        public int PcieGen;
        public int PcieLanes;
        public int PcieMaxGen;
    }

    /// <summary>شمارنده‌های سلامت دیسک (SMART از طریق Storage API ویندوز — نیاز به ادمین)</summary>
    public sealed class DiskReliability
    {
        /// <summary>درصد عمر مصرف‌شده (SSD) — -1 یعنی نامشخص</summary>
        public int WearPercent = -1;
        public int TemperatureC = -1;
        public int TemperatureMaxC = -1;
        public long PowerOnHours = -1;
        public long ReadErrors = -1;
        public long WriteErrors = -1;
    }

    public static class DiskHardware
    {
        private static Dictionary<int, DiskInfo>? _map;

        /// <summary>[ترد پس‌زمینه] مشخصات همه‌ی دیسک‌ها بر اساس شماره‌ی دیسک (کش‌شده)</summary>
        public static Dictionary<int, DiskInfo> GetAll()
        {
            if (_map != null) return _map;
            var map = new Dictionary<int, DiskInfo>();

            // ۱) Storage API ویندوز: نوع رسانه، باس، سلامت، فرم‌ور
            try
            {
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                using var s = new ManagementObjectSearcher(scope, new ObjectQuery(
                    "SELECT DeviceId, FriendlyName, Model, MediaType, BusType, Size, FirmwareVersion, HealthStatus, SpindleSpeed FROM MSFT_PhysicalDisk"));
                foreach (ManagementObject o in s.Get())
                {
                    if (!int.TryParse(o["DeviceId"]?.ToString(), out int idx)) continue;
                    string model = (o["Model"] as string ?? o["FriendlyName"] as string ?? "").Trim();
                    uint rpm = (uint)ToULong(o["SpindleSpeed"]);
                    map[idx] = new DiskInfo
                    {
                        Index = idx,
                        Model = model,
                        Brand = BrandOf(model),
                        MediaType = ToULong(o["MediaType"]) switch { 3 => "HDD", 4 => "SSD", 5 => "SCM", _ => "" },
                        Bus = ToULong(o["BusType"]) switch
                        {
                            17 => "NVMe", 11 => "SATA", 7 => "USB", 10 => "SAS", 3 => "ATA",
                            8 => "RAID", 12 => "SD", 13 => "MMC", 15 => "File-backed virtual", 18 => "SCM", 19 => "UFS",
                            _ => "",
                        },
                        SizeBytes = ToULong(o["Size"]),
                        Firmware = (o["FirmwareVersion"] as string ?? "").Trim(),
                        Health = ToULong(o["HealthStatus"]) switch { 0 => "Healthy", 1 => "Warning", 2 => "Unhealthy", _ => "" },
                        SpindleRpm = rpm is > 0 and < uint.MaxValue ? rpm : 0,
                    };
                }
            }
            catch { }

            // ۲) لینک PCIe از کنترلر NVMe (والد دستگاه دیسک)
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Index, Model, PNPDeviceID FROM Win32_DiskDrive");
                foreach (ManagementObject o in s.Get())
                {
                    int idx = Convert.ToInt32(o["Index"]);
                    if (!map.TryGetValue(idx, out var info))
                    {
                        string model = (o["Model"] as string ?? "").Trim();
                        map[idx] = info = new DiskInfo { Index = idx, Model = model, Brand = BrandOf(model) };
                    }
                    string? pnp = o["PNPDeviceID"] as string;
                    if (string.IsNullOrEmpty(pnp)) continue;
                    string? parent = GetStringProp(pnp, DevpkeyParent);
                    // بعضی درایورها یک لایه‌ی میانی دارند — تا دو سطح بالا برو تا دستگاه PCI پیدا شود
                    for (int i = 0; i < 2 && parent != null; i++)
                    {
                        if (parent.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase))
                        {
                            info.PcieGen = (int)(GetUIntProp(parent, PciCurrentLinkSpeed) ?? 0);
                            info.PcieLanes = (int)(GetUIntProp(parent, PciCurrentLinkWidth) ?? 0);
                            info.PcieMaxGen = (int)(GetUIntProp(parent, PciMaxLinkSpeed) ?? 0);
                            break;
                        }
                        parent = GetStringProp(parent, DevpkeyParent);
                    }
                }
            }
            catch { }

            _map = map;
            return map;
        }

        /// <summary>
        /// [ترد پس‌زمینه] شمارنده‌های سلامت دیسک‌ها بر اساس شماره‌ی دیسک.
        /// بدون Run as administrator ویندوز چیزی برنمی‌گرداند (دیکشنری خالی).
        /// </summary>
        public static Dictionary<int, DiskReliability> GetReliability()
        {
            var map = new Dictionary<int, DiskReliability>();
            try
            {
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                using var s = new ManagementObjectSearcher(scope, new ObjectQuery(
                    "SELECT DeviceId, Wear, Temperature, TemperatureMax, PowerOnHours, ReadErrorsTotal, WriteErrorsTotal FROM MSFT_StorageReliabilityCounter"));
                foreach (ManagementObject o in s.Get())
                {
                    if (!int.TryParse(o["DeviceId"]?.ToString(), out int idx)) continue;
                    map[idx] = new DiskReliability
                    {
                        WearPercent = Num(o["Wear"]),
                        TemperatureC = Num(o["Temperature"]),
                        TemperatureMaxC = Num(o["TemperatureMax"]),
                        PowerOnHours = NumL(o["PowerOnHours"]),
                        ReadErrors = NumL(o["ReadErrorsTotal"]),
                        WriteErrors = NumL(o["WriteErrorsTotal"]),
                    };
                }
            }
            catch { }
            return map;

            // مقدار صفر برای دما معمولاً یعنی «گزارش نشده»
            static int Num(object? v) => v == null ? -1 : Convert.ToInt32(v);
            static long NumL(object? v) => v == null ? -1 : Convert.ToInt64(v);
        }

        public static DiskInfo? Get(int index) => GetAll().TryGetValue(index, out var d) ? d : null;

        /// <summary>برند از روی نام مدل</summary>
        private static string BrandOf(string model)
        {
            string m = model.ToUpperInvariant();
            (string Key, string Brand)[] brands =
            {
                ("SAMSUNG", "Samsung"), ("WDC", "Western Digital"), ("WD ", "Western Digital"), ("WD_", "Western Digital"),
                ("SANDISK", "SanDisk"), ("SK HYNIX", "SK hynix"), ("HFM", "SK hynix"), ("HFS", "SK hynix"),
                ("KIOXIA", "KIOXIA"), ("TOSHIBA", "Toshiba"), ("CRUCIAL", "Crucial"), ("CT", "Crucial"),
                ("MICRON", "Micron"), ("MTFD", "Micron"), ("KINGSTON", "Kingston"), ("INTEL", "Intel"),
                ("SEAGATE", "Seagate"), ("ST", "Seagate"), ("ADATA", "ADATA"), ("XPG", "ADATA"), ("LEXAR", "Lexar"),
                ("HITACHI", "Hitachi"), ("HGST", "HGST"), ("TEAM", "TeamGroup"), ("PNY", "PNY"), ("CORSAIR", "Corsair"),
                ("SABRENT", "Sabrent"), ("PATRIOT", "Patriot"), ("TRANSCEND", "Transcend"), ("PHISON", "Phison"),
            };
            foreach (var (key, brand) in brands)
                if (m.StartsWith(key) || m.Contains(" " + key.Trim() + " ")) return brand;
            return "";
        }

        private static ulong ToULong(object? v)
        {
            try { return v == null ? 0 : Convert.ToUInt64(v); } catch { return 0; }
        }

        // ---- خواندن ویژگی‌های دستگاه (CfgMgr32) ----

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;
            public DEVPROPKEY(string g, uint p) { fmtid = new Guid(g); pid = p; }
        }

        private static readonly DEVPROPKEY DevpkeyParent = new("4340a6c5-93fa-4706-972c-7b648008a5a7", 8);
        private static readonly DEVPROPKEY PciCurrentLinkSpeed = new("3ab22e31-8264-4b4e-9af5-a8d2d8e33e62", 9);
        private static readonly DEVPROPKEY PciCurrentLinkWidth = new("3ab22e31-8264-4b4e-9af5-a8d2d8e33e62", 10);
        private static readonly DEVPROPKEY PciMaxLinkSpeed = new("3ab22e31-8264-4b4e-9af5-a8d2d8e33e62", 11);

        private static byte[]? GetProp(string instanceId, DEVPROPKEY key)
        {
            if (CM_Locate_DevNodeW(out uint dev, instanceId, 0) != 0) return null;
            uint size = 0;
            CM_Get_DevNode_PropertyW(dev, ref key, out _, null, ref size, 0);
            if (size == 0) return null;
            var buf = new byte[size];
            return CM_Get_DevNode_PropertyW(dev, ref key, out _, buf, ref size, 0) == 0 ? buf : null;
        }

        private static string? GetStringProp(string id, DEVPROPKEY key)
        {
            var b = GetProp(id, key);
            return b == null ? null : System.Text.Encoding.Unicode.GetString(b).TrimEnd('\0');
        }

        private static uint? GetUIntProp(string id, DEVPROPKEY key)
        {
            var b = GetProp(id, key);
            return b is { Length: >= 4 } ? BitConverter.ToUInt32(b, 0) : null;
        }

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_DevNode_PropertyW(uint devInst, ref DEVPROPKEY key, out uint type,
            byte[]? buffer, ref uint size, uint flags);
    }
}
