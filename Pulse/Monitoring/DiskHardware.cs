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
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            Microsoft.Win32.SafeHandles.SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        private static void QueryDeviceReliability(int driveIndex, DiskReliability rel)
        {
            try
            {
                using var h = CreateFile($@"\\.\PhysicalDrive{driveIndex}", 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (h.IsInvalid) return;

                int bufferSize = 4096;
                IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

                try
                {
                    // ۱) NVMe SMART / Health Information Log
                    int[] protoPropIds = { 49, 48 }; // StorageAdapterProtocolSpecificProperty, StorageDeviceProtocolSpecificProperty
                    foreach (int propId in protoPropIds)
                    {
                        byte[] zero = new byte[bufferSize];
                        Marshal.Copy(zero, 0, buffer, bufferSize);

                        Marshal.WriteInt32(buffer, 0, propId);
                        Marshal.WriteInt32(buffer, 4, 0); // PropertyStandardQuery

                        int protoOffset = 8;
                        Marshal.WriteInt32(buffer, protoOffset + 0, 3); // ProtocolTypeNvme (3)
                        Marshal.WriteInt32(buffer, protoOffset + 4, 2); // NVMeDataTypeLogPage (2)
                        Marshal.WriteInt32(buffer, protoOffset + 8, 2); // NVME_LOG_PAGE_HEALTH_INFO (2)
                        Marshal.WriteInt32(buffer, protoOffset + 12, 0);

                        int dataOffset = 40; // sizeof(STORAGE_PROTOCOL_SPECIFIC_DATA)
                        Marshal.WriteInt32(buffer, protoOffset + 16, dataOffset);
                        Marshal.WriteInt32(buffer, protoOffset + 20, 512); // ProtocolDataLength

                        bool ok = DeviceIoControl(h, IOCTL_STORAGE_QUERY_PROPERTY, buffer, (uint)bufferSize, buffer, (uint)bufferSize, out uint ret, IntPtr.Zero);
                        if (ok && ret >= (uint)(protoOffset + dataOffset + 512))
                        {
                            byte[] data = new byte[ret];
                            Marshal.Copy(buffer, data, 0, (int)ret);

                            int returnedDataOffset = BitConverter.ToInt32(data, protoOffset + 16);
                            int payloadStart = protoOffset + (returnedDataOffset > 0 ? returnedDataOffset : dataOffset);

                            if (payloadStart + 512 <= ret)
                            {
                                ushort tempK = BitConverter.ToUInt16(data, payloadStart + 1);
                                if (tempK > 273 && rel.TemperatureC <= 0)
                                    rel.TemperatureC = tempK - 273;

                                byte percentUsed = data[payloadStart + 5];
                                if (percentUsed <= 100 && rel.WearPercent < 0)
                                    rel.WearPercent = percentUsed;

                                ulong poh = BitConverter.ToUInt64(data, payloadStart + 96);
                                if (poh > 0 && rel.PowerOnHours < 0)
                                    rel.PowerOnHours = (long)poh;

                                ulong mediaErrors = BitConverter.ToUInt64(data, payloadStart + 160);
                                if (rel.ReadErrors < 0)
                                    rel.ReadErrors = (long)mediaErrors;
                                if (rel.WriteErrors < 0)
                                    rel.WriteErrors = 0;

                                break;
                            }
                        }
                    }

                    // ۲) اگر دما دریافت نشد، StorageDeviceTemperatureProperty (30) یا StorageAdapterTemperatureProperty (29) را می‌خوانیم
                    if (rel.TemperatureC <= 0)
                    {
                        int[] tempPropIds = { 30, 29 };
                        foreach (int propId in tempPropIds)
                        {
                            byte[] zero = new byte[bufferSize];
                            Marshal.Copy(zero, 0, buffer, bufferSize);

                            Marshal.WriteInt32(buffer, 0, propId);
                            Marshal.WriteInt32(buffer, 4, 0);

                            bool ok = DeviceIoControl(h, IOCTL_STORAGE_QUERY_PROPERTY, buffer, (uint)bufferSize, buffer, (uint)bufferSize, out uint ret, IntPtr.Zero);
                            if (ok && ret >= 16)
                            {
                                byte[] data = new byte[ret];
                                Marshal.Copy(buffer, data, 0, (int)ret);

                                short warn = BitConverter.ToInt16(data, 10);
                                short infoCount = BitConverter.ToInt16(data, 12);
                                if (warn > 0 && rel.TemperatureMaxC <= 0)
                                    rel.TemperatureMaxC = warn;

                                if (infoCount > 0 && ret >= 24)
                                {
                                    short curTemp = BitConverter.ToInt16(data, 18);
                                    if (curTemp > 0 && rel.TemperatureC <= 0)
                                        rel.TemperatureC = curTemp;
                                }

                                if (rel.TemperatureC > 0) break;
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch { }
        }

        /// <summary>
        /// [ترد پس‌زمینه] شمارنده‌های سلامت دیسک‌ها بر اساس شماره‌ی دیسک.
        /// </summary>
        public static Dictionary<int, DiskReliability> GetReliability()
        {
            var map = new Dictionary<int, DiskReliability>();

            // ۱) دسترسی مستقیم بدون نیاز به ادمین از طریق Win32 IOCTL
            try
            {
                var drives = GetAll();
                foreach (int idx in drives.Keys)
                {
                    if (!map.TryGetValue(idx, out var rel))
                        map[idx] = rel = new DiskReliability();
                    QueryDeviceReliability(idx, rel);
                }

                if (map.Count == 0)
                {
                    var rel = new DiskReliability();
                    QueryDeviceReliability(0, rel);
                    if (rel.TemperatureC > 0 || rel.WearPercent >= 0 || rel.PowerOnHours >= 0)
                        map[0] = rel;
                }
            }
            catch { }

            // ۲) استفاده از DiskInfoToolkit (درایورهای سطح پایین ATA / SMART)
            try
            {
                var storages = DiskInfoToolkit.StorageManager.Storages;
                if (storages != null)
                {
                    foreach (var st in storages)
                    {
                        int idx = st.DriveNumber;
                        if (!map.TryGetValue(idx, out var rel))
                            map[idx] = rel = new DiskReliability();

                        if (st.Smart != null)
                        {
                            if (rel.TemperatureC <= 0 && st.Smart.Temperature.HasValue && st.Smart.Temperature.Value > 0)
                                rel.TemperatureC = (int)st.Smart.Temperature.Value;

                            if (rel.TemperatureMaxC <= 0 && st.Smart.TemperatureWarning.HasValue && st.Smart.TemperatureWarning.Value > 0)
                                rel.TemperatureMaxC = (int)st.Smart.TemperatureWarning.Value;

                            if (rel.WearPercent < 0 && st.Smart.Life.HasValue)
                                rel.WearPercent = Math.Clamp(100 - (int)st.Smart.Life.Value, 0, 100);

                            if (rel.PowerOnHours < 0)
                            {
                                long poh = (long)Math.Max(st.Smart.DetectedPowerOnHours, st.Smart.MeasuredPowerOnHours);
                                if (poh > 0) rel.PowerOnHours = poh;
                            }

                            if (rel.ReadErrors < 0 || rel.WriteErrors < 0)
                            {
                                if (st.Smart.SmartAttributes != null)
                                {
                                    foreach (var attr in st.Smart.SmartAttributes)
                                    {
                                        if (attr.Info?.Type == DiskInfoToolkit.Interop.Enums.SmartAttributeType.ReadErrorRate)
                                            rel.ReadErrors = Math.Max(0, (long)attr.Attribute.RawValueULong);
                                        if (attr.Info?.Type == DiskInfoToolkit.Interop.Enums.SmartAttributeType.WriteErrorRate)
                                            rel.WriteErrors = Math.Max(0, (long)attr.Attribute.RawValueULong);
                                    }
                                }

                                if (rel.ReadErrors < 0 && st.Smart.DiskStatus == DiskInfoToolkit.Enums.Interop.DiskStatus.Good)
                                    rel.ReadErrors = 0;
                                if (rel.WriteErrors < 0 && st.Smart.DiskStatus == DiskInfoToolkit.Enums.Interop.DiskStatus.Good)
                                    rel.WriteErrors = 0;
                            }
                        }
                    }
                }
            }
            catch { }

            // ۳) ویندوز Storage API (متد GetStorageReliabilityCounter از کلاس PS_StorageCmdlets در صورت داشتن دسترسی)
            try
            {
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                using var s = new ManagementObjectSearcher(scope, new ObjectQuery(
                    "SELECT DeviceId, ObjectId FROM MSFT_PhysicalDisk"));
                using var psClass = new ManagementClass(scope, new ManagementPath("PS_StorageCmdlets"), null);
                foreach (ManagementObject disk in s.Get())
                {
                    if (!int.TryParse(disk["DeviceId"]?.ToString(), out int idx)) continue;
                    if (!map.TryGetValue(idx, out var rel))
                        map[idx] = rel = new DiskReliability();

                    try
                    {
                        var inParams = psClass.GetMethodParameters("GetStorageReliabilityCounter");
                        inParams["PhysicalDisk"] = disk;
                        var outParams = psClass.InvokeMethod("GetStorageReliabilityCounter", inParams, null);
                        if (outParams?["StorageReliabilityCounter"] is ManagementBaseObject r)
                        {
                            int wear = Num(r["Wear"]);
                            if (wear >= 0 && rel.WearPercent < 0) rel.WearPercent = wear;

                            int temp = Num(r["Temperature"]);
                            if (temp > 0 && rel.TemperatureC <= 0) rel.TemperatureC = temp;

                            int tempMax = Num(r["TemperatureMax"]);
                            if (tempMax > 0 && rel.TemperatureMaxC <= 0) rel.TemperatureMaxC = tempMax;

                            long poh = NumL(r["PowerOnHours"]);
                            if (poh >= 0 && rel.PowerOnHours < 0) rel.PowerOnHours = poh;

                            long rErr = NumL(r["ReadErrorsTotal"]);
                            if (rErr >= 0 && rel.ReadErrors < 0) rel.ReadErrors = rErr;

                            long wErr = NumL(r["WriteErrorsTotal"]);
                            if (wErr >= 0 && rel.WriteErrors < 0) rel.WriteErrors = wErr;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // ۴) اگر خطایی ثبت نشده و وضعیت دیسک فعال است، مقادیر صفر طبیعی برای خطا در نظر گرفته شود
            foreach (var kv in map)
            {
                var rel = kv.Value;
                if (rel.ReadErrors < 0 && (rel.WearPercent >= 0 || rel.TemperatureC > 0 || rel.PowerOnHours >= 0))
                    rel.ReadErrors = 0;
                if (rel.WriteErrors < 0 && (rel.WearPercent >= 0 || rel.TemperatureC > 0 || rel.PowerOnHours >= 0))
                    rel.WriteErrors = 0;
            }

            return map;

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
