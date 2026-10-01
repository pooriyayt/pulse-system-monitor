using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using TaskManagerPro.Services;

namespace TaskManagerPro.Models
{
    /// <summary>
    /// یک ردیف در صفحه‌ی اتصال‌های شبکه.
    /// ردیف‌ها بین بروزرسانی‌ها حفظ می‌شوند و فقط مقادیر تغییرکرده اطلاع داده می‌شوند (بدون پرش UI).
    /// </summary>
    public class ConnectionRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void On(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ConnectionRow(ConnectionEntry e, string processName, string? processPath)
        {
            Key = e.Key;
            Protocol = e.Protocol;
            LocalPort = e.LocalPort;
            RemotePort = e.RemotePort;
            Pid = e.Pid;
            LocalEndpoint = FormatEndpoint(e.LocalAddress, e.LocalPort);
            RemoteEndpoint = e.RemoteAddress == null ? "*:*" : FormatEndpoint(e.RemoteAddress, e.RemotePort);
            ProcessName = processName;
            ProcessPath = processPath;
            _state = e.State;
        }

        public string Key { get; }
        public string Protocol { get; }
        public string LocalEndpoint { get; }
        public string RemoteEndpoint { get; }
        public int LocalPort { get; }
        public int RemotePort { get; }
        public int Pid { get; }
        public string ProcessName { get; }
        public string? ProcessPath { get; }
        public string PidText => Pid.ToString();

        private string _state;
        public string State
        {
            get => _state;
            set
            {
                if (_state == value) return;
                _state = value;
                On(nameof(State));
                On(nameof(StateText));
            }
        }

        /// <summary>وضعیت ترجمه‌شده برای نمایش</summary>
        public string StateText => string.IsNullOrEmpty(_state) ? "—" : Helpers.L10n.T(_state);

        private ImageSource? _icon;
        public ImageSource? Icon
        {
            get => _icon;
            set
            {
                if (ReferenceEquals(_icon, value)) return;
                _icon = value;
                On(nameof(Icon));
                On(nameof(FallbackVisibility));
            }
        }

        public Visibility FallbackVisibility => Icon == null ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>زبان عوض شد — متن وضعیت دوباره خوانده شود</summary>
        public void RefreshText() => On(nameof(StateText));

        private static string FormatEndpoint(IPAddress address, int port)
        {
            string a = address.ToString();
            return address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{a}]:{port}" : $"{a}:{port}";
        }
    }
}
