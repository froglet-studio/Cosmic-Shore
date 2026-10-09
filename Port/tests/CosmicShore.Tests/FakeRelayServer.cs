using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using CosmicShore.Online;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A Unity Relay server on the loopback, from the public protocol page and the live behaviour the
    /// spike measured: BIND (HMAC checked against the allocation key) → BIND_RECEIVED; CONNECT_REQUEST
    /// naming the host's connection data → ACCEPTED (From = host, To = joiner), with nothing told to the
    /// host; RELAY forwarded unchanged between connected allocations (else ERROR NotConnected); PING
    /// echoed; CLOSE frees the allocation. Lets the Relay link and transport be tested without UGS.
    /// </summary>
    sealed class FakeRelayServer : IDisposable
    {
        sealed class Alloc
        {
            public RelayAllocation Allocation;
            public byte[] HostKeyData; // the 50 bytes a joiner's CONNECT_REQUEST names (host allocations only)
            public EndPoint Bound;
            public ushort LastNonce;
            public bool Mismatched;
            public readonly HashSet<Guid> Connected = new();
        }

        readonly Socket _sock;
        readonly Thread _thread;
        readonly object _lock = new();
        readonly List<Alloc> _allocs = new();
        readonly Random _rng = new(11);
        volatile bool _stop;

        public int Port { get; }
        public int Binds, Accepts, Forwarded, Pings, Closes, BadHmacs, NotConnectedErrors, LargestRelayContent;
        /// <summary>Answer each allocation's first BIND with ERROR ClientPlayerMismatch (the live server does for a stale nonce).</summary>
        public bool MismatchFirstBind;
        /// <summary>Drop this share of forwarded RELAY messages.</summary>
        public double DropPercent;

        public FakeRelayServer()
        {
            _sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _sock.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            Port = ((IPEndPoint)_sock.LocalEndPoint).Port;
            _thread = new Thread(Loop) { IsBackground = true, Name = "fake-relay" };
            _thread.Start();
        }

        static byte[] Rand(int n) => RandomNumberGenerator.GetBytes(n);

        RelayAllocation New(byte[] hostConnectionData)
        {
            var id = Guid.NewGuid();
            var bytes = new byte[16];
            id.TryWriteBytes(bytes, bigEndian: true, out _);
            return new RelayAllocation(id, bytes, Rand(255), Rand(64), hostConnectionData, "loopback-1",
                new[] { new RelayEndpoint("udp", "127.0.0.1", Port, false) });
        }

        public RelayAllocation AllocateHost()
        {
            var a = New(null);
            lock (_lock) _allocs.Add(new Alloc { Allocation = a, HostKeyData = Rand(50) });
            return a;
        }

        /// <summary>What /join returns for <paramref name="host"/>'s join code.</summary>
        public RelayAllocation Join(RelayAllocation host)
        {
            byte[] hostData;
            lock (_lock) hostData = _allocs.First(x => x.Allocation.AllocationId == host.AllocationId).HostKeyData;
            var a = New(hostData);
            lock (_lock) _allocs.Add(new Alloc { Allocation = a });
            return a;
        }

        void Loop()
        {
            var buf = new byte[4096];
            while (!_stop)
            {
                EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                int n;
                try
                {
                    if (!_sock.Poll(20000, SelectMode.SelectRead)) continue;
                    n = _sock.ReceiveFrom(buf, ref from);
                }
                catch (SocketException) { continue; }
                catch (ObjectDisposedException) { return; }
                lock (_lock) Handle(buf.AsSpan(0, n), from);
            }
        }

        void Send(EndPoint to, ReadOnlySpan<byte> data)
        {
            try { _sock.SendTo(data.ToArray(), to); } catch (SocketException) { }
        }

        static byte[] Header(byte type) => new byte[] { 0xDA, 0x72, 0x00, type };

        Alloc ById(ReadOnlySpan<byte> span)
        {
            var id = span.ToArray();
            return _allocs.FirstOrDefault(x => x.Allocation.AllocationIdBytes.AsSpan().SequenceEqual(id));
        }

        void Handle(ReadOnlySpan<byte> d, EndPoint from)
        {
            if (d.Length < 4 || d[0] != 0xDA || d[1] != 0x72) return;
            switch (d[3])
            {
                case 0: // BIND
                {
                    ushort nonce = BinaryPrimitives.ReadUInt16BigEndian(d[5..]);
                    int len = d[7];
                    var conn = d.Slice(8, len).ToArray();
                    var mac = d.Slice(8 + len, 32);
                    var a = _allocs.FirstOrDefault(x => x.Allocation.ConnectionData.AsSpan().SequenceEqual(conn));
                    if (a == null) return;
                    if (!HMACSHA256.HashData(a.Allocation.Key, d[..(8 + len)]).AsSpan().SequenceEqual(mac)) { BadHmacs++; return; }
                    if (MismatchFirstBind && !a.Mismatched)
                    {
                        a.Mismatched = true;
                        a.LastNonce = nonce;
                        Send(from, Header(12).Concat(a.Allocation.AllocationIdBytes).Append((byte)3).ToArray());
                        return;
                    }
                    if (MismatchFirstBind && nonce == a.LastNonce) return; // the same stale nonce: still refused
                    a.Bound = from;
                    Binds++;
                    Send(from, Header(1));
                    return;
                }
                case 2: // PING: echoed
                    Pings++;
                    Send(from, d);
                    return;
                case 3: // CONNECT_REQUEST
                {
                    var joiner = ById(d.Slice(4, 16));
                    int len = d[20];
                    var hostData = d.Slice(21, len).ToArray();
                    var host = _allocs.FirstOrDefault(x => x.HostKeyData != null && x.HostKeyData.AsSpan().SequenceEqual(hostData));
                    if (joiner == null || host == null || joiner.Bound == null || !joiner.Bound.Equals(from)) return;
                    joiner.Connected.Add(host.Allocation.AllocationId);
                    host.Connected.Add(joiner.Allocation.AllocationId);
                    Accepts++;
                    Send(from, Header(6).Concat(host.Allocation.AllocationIdBytes).Concat(joiner.Allocation.AllocationIdBytes).ToArray());
                    return;
                }
                case 10: // RELAY
                {
                    var src = ById(d.Slice(4, 16));
                    var dst = ById(d.Slice(20, 16));
                    if (src == null || src.Bound == null || !src.Bound.Equals(from)) return;
                    LargestRelayContent = Math.Max(LargestRelayContent, BinaryPrimitives.ReadUInt16BigEndian(d[36..]));
                    if (dst == null || dst.Bound == null || !src.Connected.Contains(dst.Allocation.AllocationId))
                    {
                        NotConnectedErrors++;
                        Send(from, Header(12).Concat(src.Allocation.AllocationIdBytes).Append((byte)5).ToArray());
                        return;
                    }
                    if (DropPercent > 0 && _rng.NextDouble() * 100 < DropPercent) return;
                    Forwarded++;
                    Send(dst.Bound, d);
                    return;
                }
                case 11: // CLOSE
                {
                    var a = ById(d.Slice(4, 16));
                    if (a?.Bound == null) return;
                    a.Bound = null;
                    Closes++;
                    return;
                }
            }
        }

        /// <summary>The server ends the allocation (sends CLOSE), as on its idle timeout.</summary>
        public void CloseAllocation(RelayAllocation alloc)
        {
            lock (_lock)
            {
                var a = ById(alloc.AllocationIdBytes);
                if (a?.Bound == null) return;
                Send(a.Bound, Header(11).Concat(a.Allocation.AllocationIdBytes).ToArray());
                a.Bound = null;
            }
        }

        public void Dispose()
        {
            _stop = true;
            _sock.Dispose();
        }
    }
}
