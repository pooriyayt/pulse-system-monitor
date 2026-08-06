using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TaskManagerPro.Monitoring
{
    /// <summary>یک ردیف «پرمصرف‌ترین پردازه» زیر گراف‌های Performance</summary>
    public class TopProc
    {
        public int Pid;
        public string Name = "";
        public double Value;
        public string Text = "";
    }

    /// <summary>
    /// نمونه‌بردار سبک پردازه‌ها برای بخش «Top processes» زیر هر گراف Performance.
    /// در ترد پس‌زمینه صدا زده می‌شود.
    /// </summary>
    public static class ProcessSampler
    {
        private sealed class Row
        {
            public int Pid;
            public string Name = "";
            public double Cpu, MemMB, DiskMBs, NetKBs, NetDownKBs, NetUpKBs, Gpu;
        }

        private static readonly Dictionary<int, (TimeSpan Cpu, DateTime At)> PrevCpu = new();
        private static readonly Dictionary<int, (ulong Bytes, DateTime At)> PrevIo = new();
        private static readonly object Lock = new();
        private static List<Row> _rows = new();

        /// <summary>یک دور نمونه‌برداری از همه‌ی پردازه‌ها (هر تیک Performance)</summary>
        public static void Sample()
        {
            lock (Lock)
            {
                var now = DateTime.UtcNow;
                var gpuByPid = SystemMonitor.Instance.GpuByPid;
                var netRates = NetworkMonitor.Instance.RatesKBs;
                var netDown = NetworkMonitor.Instance.RecvRatesKBs;
                var netUp = NetworkMonitor.Instance.SentRatesKBs;
                var rows = new List<Row>(256);
                var alive = new HashSet<int>();

                foreach (var p in Process.GetProcesses())
                {
                    using (p)
                    {
                        try
                        {
                            int pid = p.Id;
                            if (pid == 0) continue;
                            alive.Add(pid);
                            var row = new Row
                            {
                                Pid = pid,
                                Name = p.ProcessName,
                                MemMB = p.WorkingSet64 / (1024.0 * 1024.0),
                            };

                            try
                            {
                                var cpu = p.TotalProcessorTime;
                                if (PrevCpu.TryGetValue(pid, out var prev))
                                {
                                    double dt = (now - prev.At).TotalSeconds;
                                    if (dt > 0.1)
                                        row.Cpu = Math.Clamp((cpu - prev.Cpu).TotalSeconds / dt / Environment.ProcessorCount * 100.0, 0, 100);
                                }
                                PrevCpu[pid] = (cpu, now);
                            }
                            catch { }

                            try
                            {
                                ulong io = ReadIoBytes(pid);
                                if (io > 0)
                                {
                                    if (PrevIo.TryGetValue(pid, out var prevIo))
                                    {
                                        double dt = (now - prevIo.At).TotalSeconds;
                                        if (dt > 0.1 && io >= prevIo.Bytes)
                                            row.DiskMBs = (io - prevIo.Bytes) / dt / (1024.0 * 1024.0);
                                    }
                                    PrevIo[pid] = (io, now);
                                }
                            }
                            catch { }

                            row.Gpu = gpuByPid.TryGetValue(pid, out var g) ? g : 0;
                            row.NetKBs = netRates.TryGetValue(pid, out var n) ? n : 0;
                            row.NetDownKBs = netDown.TryGetValue(pid, out var nd) ? nd : 0;
                            row.NetUpKBs = netUp.TryGetValue(pid, out var nu) ? nu : 0;
                            rows.Add(row);
                        }
                        catch { }
                    }
                }

                foreach (var dead in PrevCpu.Keys.Where(k => !alive.Contains(k)).ToList()) PrevCpu.Remove(dead);
                foreach (var dead in PrevIo.Keys.Where(k => !alive.Contains(k)).ToList()) PrevIo.Remove(dead);

                _rows = rows;
            }
        }

        /// <summary>سه پردازه‌ی پرمصرف بر اساس متریک: cpu / mem / gpu / disk / net / netdown / netup</summary>
        public static List<TopProc> Top(string metric, int n = 3)
        {
            List<Row> rows;
            lock (Lock) rows = _rows;

            static double Pick(Row r, string metric) => metric switch
            {
                "mem" => r.MemMB,
                "gpu" => r.Gpu,
                "net" => r.NetKBs,
                "netdown" => r.NetDownKBs,
                "netup" => r.NetUpKBs,
                "disk" => r.DiskMBs,
                _ => r.Cpu,
            };

            return rows.OrderByDescending(r => Pick(r, metric)).Take(n).Select(r => new TopProc
            {
                Pid = r.Pid,
                Name = r.Name,
                Value = Pick(r, metric),
                Text = metric switch
                {
                    "mem" => $"{r.MemMB:F0} MB",
                    "gpu" => $"{r.Gpu:F0}%",
                    "net" or "netdown" or "netup" => FormatSpeed(Pick(r, metric)),
                    "disk" => $"{r.DiskMBs:F1} MB/s",
                    _ => $"{r.Cpu:F1}%",
                },
            }).ToList();
        }

        private static string FormatSpeed(double kbs) =>
            kbs >= 1024 ? $"{kbs / 1024.0:F1} MB/s" : $"{kbs:F0} KB/s";

        // ---------- P/Invoke برای I/O دیسک ----------

        private static ulong ReadIoBytes(int pid)
        {
            IntPtr h = OpenProcess(0x1000, false, pid);
            if (h == IntPtr.Zero) return 0;
            try
            {
                if (GetProcessIoCounters(h, out IO_COUNTERS io))
                    return io.ReadTransferCount + io.WriteTransferCount;
            }
            catch { }
            finally { CloseHandle(h); }
            return 0;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int access, bool inherit, int pid);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS counters);
    }
}
