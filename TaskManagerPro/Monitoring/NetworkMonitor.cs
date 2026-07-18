using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using TaskManagerPro.Helpers;

namespace TaskManagerPro.Monitoring
{
    /// <summary>
    /// مصرف شبکهی هر پردازه (دانلود + آپلود) از طریق ETW — همان روشی که Task Manager ویندوز استفاده می‌کند.
    /// فقط با Run as administrator کار می‌کند (محدودیت خود ویندوز).
    /// اگر دسترسی Admin نباشد، IsAvailable = false می‌ماند و UI قفل 🔒 نشان می‌دهد.
    /// </summary>
    public sealed class NetworkMonitor : IDisposable
    {
        /// <summary>نمونه‌ی سراسری مشترک</summary>
        public static NetworkMonitor Instance { get; } = new();

        /// <summary>true یعنی مانیتور فعال است (فقط وقتی برنامه Run as administrator باشد)</summary>
        public bool IsAvailable { get; private set; }

        private TraceEventSession? _session;
        private readonly object _lock = new();
        private readonly Dictionary<int, long> _bytes = new();
        private volatile Dictionary<int, double> _rates = new();
        private DateTime _lastSnap = DateTime.UtcNow;

        /// <summary>مصرف شبکهی هر پردازه به KB/s (PID ← سرعت) — با هر Snapshot به‌روز می‌شود</summary>
        public IReadOnlyDictionary<int, double> RatesKBs => _rates;

        /// <summary>شروع گوش دادن به رویدادهای شبکهی کرنل (بی‌خطر است؛ اگر Admin نباشیم فقط غیرفعال می‌ماند)</summary>
        public void Start()
        {
            if (_session != null) return;
            if (!AdminHelper.IsAdmin) { IsAvailable = false; return; }

            try
            {
                _session = new TraceEventSession("TaskManagerProNetSession");
                _session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);

                var kernel = _session.Source.Kernel;
                kernel.TcpIpRecv += d => Add(d.ProcessID, d.size);
                kernel.TcpIpSend += d => Add(d.ProcessID, d.size);
                kernel.TcpIpRecvIPV6 += d => Add(d.ProcessID, d.size);
                kernel.TcpIpSendIPV6 += d => Add(d.ProcessID, d.size);
                kernel.UdpIpRecv += d => Add(d.ProcessID, d.size);
                kernel.UdpIpSend += d => Add(d.ProcessID, d.size);

                // حلقه‌ی پردازش رویدادها در یک ترد جدا (تا بسته شدن Session ادامه دارد)
                Task.Run(() =>
                {
                    try { _session?.Source.Process(); } catch { }
                });

                IsAvailable = true;
            }
            catch
            {
                try { _session?.Dispose(); } catch { }
                _session = null;
                IsAvailable = false;
            }
        }

        private void Add(int pid, int size)
        {
            if (pid <= 0 || size <= 0) return;
            lock (_lock)
            {
                _bytes[pid] = _bytes.GetValueOrDefault(pid) + size;
            }
        }

        /// <summary>محاسبه‌ی سرعت (KB/s) از بایت‌های جمع‌شده از آخرین Snapshot</summary>
        public void Snapshot()
        {
            if (!IsAvailable) return;
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                double sec = (now - _lastSnap).TotalSeconds;
                if (sec < 0.2) return;

                var rates = new Dictionary<int, double>(_bytes.Count);
                foreach (var kv in _bytes)
                    rates[kv.Key] = kv.Value / sec / 1024.0;

                _rates = rates;
                _bytes.Clear();
                _lastSnap = now;
            }
        }

        public void Dispose()
        {
            try { _session?.Dispose(); } catch { }
            _session = null;
            IsAvailable = false;
        }
    }
}
