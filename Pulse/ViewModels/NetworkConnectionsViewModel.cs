using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using TaskManagerPro.Helpers;
using TaskManagerPro.Models;
using TaskManagerPro.Services;

namespace TaskManagerPro.ViewModels
{
    /// <summary>
    /// ViewModel صفحه‌ی اتصال‌های شبکه:
    /// هر چند ثانیه جدول TCP/UDP خوانده می‌شود و با لیست فعلی «تطبیق» داده می‌شود
    /// (اضافه / حذف / جابه‌جایی) تا لیست پرش نکند و انتخاب و اسکرول حفظ شوند.
    /// </summary>
    public sealed class NetworkConnectionsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void On(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>فاصله‌ی بروزرسانی خودکار</summary>
        public static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(2500);

        /// <summary>ردیف‌های نمایش‌داده‌شده (بعد از فیلتر)</summary>
        public ObservableCollection<ConnectionRow> Rows { get; } = new();

        // همه‌ی ردیف‌ها (قبل از فیلتر) — کلید: ConnectionEntry.Key
        private readonly Dictionary<string, ConnectionRow> _all = new();

        private readonly DispatcherQueue _dispatcher;
        private DispatcherQueueTimer? _timer;
        private bool _busy;

        public NetworkConnectionsViewModel(DispatcherQueue dispatcher)
        {
            _dispatcher = dispatcher;
        }

        // ---- فیلترها ----

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                value ??= "";
                if (_searchText == value) return;
                _searchText = value;
                On(nameof(SearchText));
                ApplyFilter();
            }
        }

        /// <summary>0 = همه، 1 = TCP، 2 = UDP، 3 = فقط LISTENING</summary>
        private int _protocolFilter;
        public int ProtocolFilter
        {
            get => _protocolFilter;
            set
            {
                if (_protocolFilter == value) return;
                _protocolFilter = value;
                On(nameof(ProtocolFilter));
                ApplyFilter();
            }
        }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused == value) return;
                _isPaused = value;
                On(nameof(IsPaused));
                if (!value) _ = RefreshAsync();
            }
        }

        private string _summary = "";
        /// <summary>مثلاً «۱۲۰ اتصال · ۳۵ پورت در حال گوش دادن»</summary>
        public string Summary
        {
            get => _summary;
            private set { if (_summary == value) return; _summary = value; On(nameof(Summary)); }
        }

        private string? _error;
        public string? Error
        {
            get => _error;
            private set { if (_error == value) return; _error = value; On(nameof(Error)); }
        }

        // ---- چرخه‌ی عمر ----

        public void Start()
        {
            if (_timer != null) return;
            _timer = _dispatcher.CreateTimer();
            _timer.Interval = RefreshInterval;
            _timer.Tick += (_, _) => { if (!IsPaused) _ = RefreshAsync(); };
            _timer.Start();
            _ = RefreshAsync();
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer = null;
        }

        /// <summary>[ترد UI] خواندن جدول اتصال‌ها و بروزرسانی ردیف‌ها</summary>
        public async Task RefreshAsync()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var snapshot = await Task.Run(() =>
                {
                    var entries = NetworkConnectionService.GetAll();

                    // نام و مسیر هر PID فقط یک بار در هر دور
                    var procs = new Dictionary<int, (string name, string? path)>();
                    foreach (var pid in entries.Select(e => e.Pid).Distinct())
                    {
                        var path = ProcessPathResolver.GetPath(pid);
                        IconCache.Preload(path);
                        procs[pid] = (ProcessPathResolver.GetName(pid, path), path);
                    }
                    ProcessPathResolver.TrimCache();
                    return (entries, procs);
                });

                Merge(snapshot.entries, snapshot.procs);
                Error = null;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
            finally
            {
                _busy = false;
            }
        }

        private void Merge(List<ConnectionEntry> entries, Dictionary<int, (string name, string? path)> procs)
        {
            var seen = new HashSet<string>();
            foreach (var e in entries)
            {
                var key = e.Key;
                if (!seen.Add(key)) continue;

                if (_all.TryGetValue(key, out var row))
                {
                    row.State = e.State;
                }
                else
                {
                    var (name, path) = procs.TryGetValue(e.Pid, out var pr) ? pr : ("PID " + e.Pid, null);
                    row = new ConnectionRow(e, name, path) { Icon = IconCache.Get(path) };
                    _all[key] = row;
                }
            }

            foreach (var gone in _all.Keys.Where(k => !seen.Contains(k)).ToList())
                _all.Remove(gone);

            int listening = _all.Values.Count(r => r.State == "LISTENING");
            Summary = string.Format(L10n.T("{0} connections · {1} listening"), _all.Count, listening);

            ApplyFilter();
        }

        /// <summary>اعمال فیلتر و مرتب‌سازی، با کمترین تغییر ممکن روی Rows</summary>
        public void ApplyFilter()
        {
            var q = _searchText.Trim();
            IEnumerable<ConnectionRow> query = _all.Values;

            query = _protocolFilter switch
            {
                1 => query.Where(r => r.Protocol.StartsWith("TCP", StringComparison.Ordinal)),
                2 => query.Where(r => r.Protocol.StartsWith("UDP", StringComparison.Ordinal)),
                3 => query.Where(r => r.State == "LISTENING" || r.Protocol.StartsWith("UDP", StringComparison.Ordinal)),
                _ => query,
            };

            if (q.Length > 0)
            {
                query = query.Where(r =>
                    r.ProcessName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.LocalEndpoint.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.RemoteEndpoint.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.PidText == q ||
                    r.State.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            var desired = query
                .OrderBy(r => r.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Protocol, StringComparer.Ordinal)
                .ThenBy(r => r.LocalPort)
                .ThenBy(r => r.RemoteEndpoint, StringComparer.Ordinal)
                .ToList();

            Reconcile(desired);
        }

        private void Reconcile(List<ConnectionRow> desired)
        {
            var desiredSet = new HashSet<ConnectionRow>(desired);
            for (int i = Rows.Count - 1; i >= 0; i--)
                if (!desiredSet.Contains(Rows[i])) Rows.RemoveAt(i);

            for (int i = 0; i < desired.Count; i++)
            {
                var item = desired[i];
                if (i < Rows.Count && ReferenceEquals(Rows[i], item)) continue;

                int existing = IndexOf(item, i + 1);
                if (existing >= 0) Rows.Move(existing, i);
                else Rows.Insert(i, item);
            }
        }

        private int IndexOf(ConnectionRow item, int start)
        {
            for (int i = start; i < Rows.Count; i++)
                if (ReferenceEquals(Rows[i], item)) return i;
            return -1;
        }

        /// <summary>زبان عوض شد</summary>
        public void RefreshTexts()
        {
            foreach (var r in _all.Values) r.RefreshText();
            int listening = _all.Values.Count(r => r.State == "LISTENING");
            Summary = string.Format(L10n.T("{0} connections · {1} listening"), _all.Count, listening);
        }

        /// <summary>بعد از بستن پردازه، ردیف‌هایش فوراً حذف شوند</summary>
        public void RemoveByPid(int pid)
        {
            foreach (var key in _all.Where(kv => kv.Value.Pid == pid).Select(kv => kv.Key).ToList())
                _all.Remove(key);
            ApplyFilter();
        }
    }
}
