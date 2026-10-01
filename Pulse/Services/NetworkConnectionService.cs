using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace TaskManagerPro.Services
{
    /// <summary>یک اتصال/پورت باز (داده‌ی خام، بدون وابستگی به UI)</summary>
    public readonly record struct ConnectionEntry(
        string Protocol,
        IPAddress LocalAddress,
        int LocalPort,
        IPAddress? RemoteAddress,
        int RemotePort,
        string State,
        int Pid)
    {
        /// <summary>کلید یکتا برای تطبیق ردیف‌ها بین دو بروزرسانی</summary>
        public string Key => $"{Protocol}|{LocalAddress}|{LocalPort}|{RemoteAddress}|{RemotePort}|{Pid}";
    }

    /// <summary>
    /// خواندن جدول اتصال‌های TCP/UDP (IPv4 و IPv6) به همراه PID مالک
    /// با GetExtendedTcpTable / GetExtendedUdpTable از iphlpapi.dll
    /// </summary>
    public static class NetworkConnectionService
    {
        public static List<ConnectionEntry> GetAll()
        {
            var list = new List<ConnectionEntry>(512);
            ReadTcp(AF_INET, list);
            ReadTcp(AF_INET6, list);
            ReadUdp(AF_INET, list);
            ReadUdp(AF_INET6, list);
            return list;
        }

        // ---- TCP ----

        private static void ReadTcp(int family, List<ConnectionEntry> list)
        {
            var buffer = QueryTable((IntPtr b, ref int size) =>
                GetExtendedTcpTable(b, ref size, false, family, TCP_TABLE_OWNER_PID_ALL, 0));
            if (buffer == IntPtr.Zero) return;

            try
            {
                int count = Marshal.ReadInt32(buffer);
                IntPtr row = buffer + 4;
                bool v6 = family == AF_INET6;
                string proto = v6 ? "TCPv6" : "TCP";
                int rowSize = v6 ? Marshal.SizeOf<MIB_TCP6ROW_OWNER_PID>() : Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();

                for (int i = 0; i < count; i++, row += rowSize)
                {
                    if (v6)
                    {
                        var r = Marshal.PtrToStructure<MIB_TCP6ROW_OWNER_PID>(row);
                        list.Add(new ConnectionEntry(proto,
                            ToIPv6(r.ucLocalAddr, r.dwLocalScopeId), ToPort(r.dwLocalPort),
                            ToIPv6(r.ucRemoteAddr, r.dwRemoteScopeId), ToPort(r.dwRemotePort),
                            StateName(r.dwState), (int)r.dwOwningPid));
                    }
                    else
                    {
                        var r = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(row);
                        list.Add(new ConnectionEntry(proto,
                            new IPAddress(r.dwLocalAddr), ToPort(r.dwLocalPort),
                            new IPAddress(r.dwRemoteAddr), ToPort(r.dwRemotePort),
                            StateName(r.dwState), (int)r.dwOwningPid));
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // ---- UDP ----

        private static void ReadUdp(int family, List<ConnectionEntry> list)
        {
            var buffer = QueryTable((IntPtr b, ref int size) =>
                GetExtendedUdpTable(b, ref size, false, family, UDP_TABLE_OWNER_PID, 0));
            if (buffer == IntPtr.Zero) return;

            try
            {
                int count = Marshal.ReadInt32(buffer);
                IntPtr row = buffer + 4;
                bool v6 = family == AF_INET6;
                string proto = v6 ? "UDPv6" : "UDP";
                int rowSize = v6 ? Marshal.SizeOf<MIB_UDP6ROW_OWNER_PID>() : Marshal.SizeOf<MIB_UDPROW_OWNER_PID>();

                for (int i = 0; i < count; i++, row += rowSize)
                {
                    if (v6)
                    {
                        var r = Marshal.PtrToStructure<MIB_UDP6ROW_OWNER_PID>(row);
                        list.Add(new ConnectionEntry(proto,
                            ToIPv6(r.ucLocalAddr, r.dwLocalScopeId), ToPort(r.dwLocalPort),
                            null, 0, "", (int)r.dwOwningPid));
                    }
                    else
                    {
                        var r = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(row);
                        list.Add(new ConnectionEntry(proto,
                            new IPAddress(r.dwLocalAddr), ToPort(r.dwLocalPort),
                            null, 0, "", (int)r.dwOwningPid));
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // ---- Helpers ----

        private delegate uint TableQuery(IntPtr buffer, ref int size);

        /// <summary>
        /// الگوی استاندارد: یک بار برای گرفتن اندازه، بعد با بافر کافی.
        /// چون جدول بین دو فراخوانی ممکن است بزرگ شود، چند بار تلاش می‌شود.
        /// خروجی باید با FreeHGlobal آزاد شود (Zero یعنی شکست).
        /// </summary>
        private static IntPtr QueryTable(TableQuery query)
        {
            int size = 0;
            uint err = query(IntPtr.Zero, ref size);
            for (int attempt = 0; attempt < 5 && err == ERROR_INSUFFICIENT_BUFFER; attempt++)
            {
                size += 4096; // کمی جای اضافه برای اتصال‌های جدید
                IntPtr buffer = Marshal.AllocHGlobal(size);
                err = query(buffer, ref size);
                if (err == NO_ERROR) return buffer;
                Marshal.FreeHGlobal(buffer);
            }
            return IntPtr.Zero;
        }

        /// <summary>پورت در بایت‌های پایینی DWORD و به ترتیب شبکه (big-endian) است</summary>
        private static int ToPort(uint raw) => ((int)(raw & 0xFF) << 8) | (int)((raw >> 8) & 0xFF);

        private static IPAddress ToIPv6(byte[] bytes, uint scopeId)
        {
            try { return new IPAddress(bytes, scopeId); }
            catch { return IPAddress.IPv6None; }
        }

        private static string StateName(uint state) => state switch
        {
            1 => "CLOSED",
            2 => "LISTENING",
            3 => "SYN_SENT",
            4 => "SYN_RECEIVED",
            5 => "ESTABLISHED",
            6 => "FIN_WAIT_1",
            7 => "FIN_WAIT_2",
            8 => "CLOSE_WAIT",
            9 => "CLOSING",
            10 => "LAST_ACK",
            11 => "TIME_WAIT",
            12 => "DELETE_TCB",
            _ => "UNKNOWN",
        };

        // ---- Native ----

        private const int AF_INET = (int)AddressFamily.InterNetwork;    // 2
        private const int AF_INET6 = (int)AddressFamily.InterNetworkV6; // 23
        private const int TCP_TABLE_OWNER_PID_ALL = 5;
        private const int UDP_TABLE_OWNER_PID = 1;
        private const uint NO_ERROR = 0;
        private const uint ERROR_INSUFFICIENT_BUFFER = 122;

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public uint dwState;
            public uint dwLocalAddr;
            public uint dwLocalPort;
            public uint dwRemoteAddr;
            public uint dwRemotePort;
            public uint dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCP6ROW_OWNER_PID
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucLocalAddr;
            public uint dwLocalScopeId;
            public uint dwLocalPort;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucRemoteAddr;
            public uint dwRemoteScopeId;
            public uint dwRemotePort;
            public uint dwState;
            public uint dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_UDPROW_OWNER_PID
        {
            public uint dwLocalAddr;
            public uint dwLocalPort;
            public uint dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_UDP6ROW_OWNER_PID
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucLocalAddr;
            public uint dwLocalScopeId;
            public uint dwLocalPort;
            public uint dwOwningPid;
        }

        [DllImport("iphlpapi.dll")]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable, ref int pdwSize, [MarshalAs(UnmanagedType.Bool)] bool bOrder,
            int ulAf, int tableClass, uint reserved);

        [DllImport("iphlpapi.dll")]
        private static extern uint GetExtendedUdpTable(
            IntPtr pUdpTable, ref int pdwSize, [MarshalAs(UnmanagedType.Bool)] bool bOrder,
            int ulAf, int tableClass, uint reserved);
    }
}
