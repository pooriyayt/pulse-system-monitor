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
        private readonly Dictionary<int, long> _recvBytes = new();
        private readonly Dictionary<int, long> _sentBytes = new();
        private volatile Dictionary<int, double> _rates = new();
        private volatile Dictionary<int, double> _recvRates = new();
        private volatile Dictionary<int, double> _sentRates = new();
        private DateTime _lastSnap = DateTime.UtcNow;

        /// <summary>مصرف کل شبکهی هر پردازه به KB/s (PID ← سرعت) — با هر Snapshot به‌روز می‌شود</summary>
        public IReadOnlyDictionary<int, double> RatesKBs => _rates;

        /// <summary>سرعت دانلود هر پردازه به KB/s</summary>
        public IReadOnlyDictionary<int, double> RecvRatesKBs => _recvRates;

        /// <summary>سرعت آپلود هر پردازه به KB/s</summary>
        public IReadOnlyDictionary<int, double> SentRatesKBs => _sentRates;

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
                kernel.TcpIpRecv += d => Add(d.ProcessID, d.size, recv: true);
                kernel.TcpIpSend += d => Add(d.ProcessID, d.size, recv: false);
                kernel.TcpIpRecvIPV6 += d => Add(d.ProcessID, d.size, recv: true);
                kernel.TcpIpSendIPV6 += d => Add(d.ProcessID, d.size, recv: false);
                kernel.UdpIpRecv += d => Add(d.ProcessID, d.size, recv: true);
                kernel.UdpIpSend += d => Add(d.ProcessID, d.size, recv: false);

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

        private void Add(int pid, int size, bool recv)
        {
            if (pid <= 0 || size <= 0) return;
            lock (_lock)
            {
                var map = recv ? _recvBytes : _sentBytes;
                map[pid] = map.GetValueOrDefault(pid) + size;
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

                var recv = new Dictionary<int, double>(_recvBytes.Count);
                foreach (var kv in _recvBytes)
                    recv[kv.Key] = kv.Value / sec / 1024.0;

                var sent = new Dictionary<int, double>(_sentBytes.Count);
                foreach (var kv in _sentBytes)
                    sent[kv.Key] = kv.Value / sec / 1024.0;

                var total = new Dictionary<int, double>(recv.Count + sent.Count);
                foreach (var kv in recv) total[kv.Key] = kv.Value;
                foreach (var kv in sent) total[kv.Key] = total.GetValueOrDefault(kv.Key) + kv.Value;

                _recvRates = recv;
                _sentRates = sent;
                _rates = total;
                _recvBytes.Clear();
                _sentBytes.Clear();
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
