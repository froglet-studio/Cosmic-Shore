using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace CosmicShore.Engine.Networking
{
    /// <summary>The UDP transport (<see cref="UdpTransport"/>).</summary>
    internal sealed class UdpTransportFactory : INetTransportFactory
    {
        public INetTransport Listen(string address, int port) => UdpTransport.Listen(address, port);
        public INetTransport Connect(string address, int port, int timeoutMs) => UdpTransport.Connect(address, port, timeoutMs);
    }

    /// <summary>
    /// Froglet's own transport: reliable, ordered, whole frames over UDP (docs/MULTIPLAYER.md §6.6).
    /// It keeps the <see cref="INetTransport"/> contract, so NetDriver runs on it unchanged, and it is the
    /// base the unreliable channel and an internet relay build on: a relay forwards datagrams, so the
    /// reliability has to be ours.
    ///
    /// WIRE (little-endian). Every packet starts with a type byte:
    ///   ConnectRequest  [1][magic u32][version u8][nonce u64]          client → server, every 100 ms until accepted
    ///   ConnectAccept   [2][nonce u64][token u64]                       server → client (again for a repeated request)
    ///   Data            [3][token u64][ackNext u32][ackBits u64] then fragments [seq u32][flags u8][len u16][bytes]
    ///                   (flags: 1 = the last fragment of a frame; 2 = an unreliable frame, whole, seq unused)
    ///   Disconnect      [4][token u64]                                  either way, sent three times
    /// A frame is split into fragments of at most <see cref="FragmentBytes"/>, numbered by one u32 sequence
    /// per direction (no wrap in practice: 4 TB per connection); the last fragment of a frame carries
    /// <see cref="LastFlag"/>. The receiver acks with the next sequence it needs (everything below arrived)
    /// plus a bitmask of the 64 after it, in every packet it sends. The sender resends a fragment when its
    /// retransmission timeout (smoothed RTT + 4 × variance, Karn's rule: only first sends are timed) passes,
    /// keeps at most <see cref="Window"/> fragments in flight, and the receiver delivers in order. The token,
    /// random per connection, makes a packet from anyone else's address or an old connection fall on the floor.
    ///
    /// LIFE. A peer that sends nothing for <see cref="TimeoutMs"/> is gone (Unity Transport's default is the
    /// same 10 s); an idle connection sends an ack every <see cref="KeepaliveMs"/>. A Disconnect is sent only
    /// after the frames queued before it are acknowledged (a kick's reason arrives before the close), or after
    /// <see cref="LingerMs"/>. One background thread owns the socket and every peer's state; the main thread
    /// only queues frames and reads events, as with <see cref="NetSocket"/>.
    /// </summary>
    internal sealed class UdpTransport : INetTransport
    {
        const uint Magic = 0x504E5343; // "CSNP"
        const byte Version = 1;
        const byte TConnectRequest = 1, TConnectAccept = 2, TData = 3, TDisconnect = 4;
        const byte LastFlag = 1, UnreliableFlag = 2;
        const int HeaderBytes = 1 + 8 + 4 + 8, FragmentHeaderBytes = 4 + 1 + 2;
        /// <summary>Fragment payload: a full packet stays under 1,200 bytes, inside any internet path's MTU.</summary>
        public const int FragmentBytes = 1150;
        const int MaxPacket = HeaderBytes + FragmentHeaderBytes + FragmentBytes;
        public const int Window = 512;
        const int ReorderWindow = 8192;
        public const int TimeoutMs = 10000;
        public const int KeepaliveMs = 250;
        const int ConnectRetryMs = 100;
        const int LingerMs = 2000;
        const int MinRtoMs = 30, MaxRtoMs = 2000;
        const int MaxFrame = 64 * 1024 * 1024;

        sealed class Fragment
        {
            public uint Seq;
            public byte[] Data;
            public int Offset, Length;
            public bool Last;
            public double SentAt = -1;
            public int Sends;
        }

        sealed class Peer
        {
            public int Id;
            public EndPoint EndPoint;
            public ulong Token, Nonce;
            // Sending.
            public uint NextSeq;
            public readonly Queue<Fragment> Waiting = new();
            /// <summary>Unreliable frames for the next packet: sent once, never resent, never held back.</summary>
            public readonly Queue<byte[]> Unreliable = new();
            public readonly SortedDictionary<uint, Fragment> InFlight = new();
            // Receiving.
            public uint NextExpected;
            public readonly Dictionary<uint, (byte[] data, bool last)> Early = new();
            public MemoryStream Assembly = new();
            public bool AckDirty;
            public double LastRecv, LastSend;
            // Timing.
            public double Srtt = -1, RttVar, Rto = 200;
            // Closing.
            public bool Closing;
            public double ClosingSince;
        }

        readonly Socket _sock;
        readonly Thread _thread;
        readonly ConcurrentQueue<NetEvent> _inbox = new();
        readonly ConcurrentQueue<(int peer, byte[] data, NetChannel channel)> _outbox = new();
        readonly ConcurrentQueue<int> _disconnects = new();
        readonly Dictionary<string, Peer> _byEndPoint = new();
        readonly Dictionary<int, Peer> _byId = new();
        readonly byte[] _rx = new byte[65536];
        readonly byte[] _tx = new byte[MaxPacket];
        readonly long _origin = System.Diagnostics.Stopwatch.GetTimestamp();
        int _nextId;
        volatile int _peerCount;
        volatile bool _disposed, _stopped;
        double _disposedAt = -1;

        // Client side.
        readonly IPEndPoint _server;
        readonly ulong _nonce;
        double _connectDeadline, _lastConnectSend = -1;
        bool _connecting;

        /// <summary>Tests: drop this share (0-100) of outgoing datagrams of every kind, to prove the resends.</summary>
        internal double DropPercent { get; set; }
        /// <summary>Tests: a shorter silence timeout than <see cref="TimeoutMs"/>.</summary>
        internal int SilenceTimeoutMs { get; set; } = TimeoutMs;
        readonly System.Random _dropRng = new(1234);
        /// <summary>Datagrams sent and fragments resent, for the stats and the tests.</summary>
        public long DatagramsSent, Resends;

        public bool IsServer { get; }
        public int ListenPort { get; }
        public int PeerCount => _peerCount;

        double Now => System.Diagnostics.Stopwatch.GetElapsedTime(_origin).TotalMilliseconds;

        UdpTransport(bool server, Socket sock, IPEndPoint serverEndPoint, int timeoutMs)
        {
            IsServer = server;
            _sock = sock;
            ListenPort = server ? ((IPEndPoint)sock.LocalEndPoint).Port : 0;
            if (!server)
            {
                _server = serverEndPoint;
                _nonce = RandomU64();
                _connecting = true;
                _connectDeadline = Now + timeoutMs;
            }
            _thread = new Thread(Loop) { IsBackground = true, Name = server ? "udp-server" : "udp-client" };
            _thread.Start();
        }

        static Socket NewSocket(AddressFamily family)
        {
            var s = new Socket(family, SocketType.Dgram, ProtocolType.Udp);
            // A burst (a scene snapshot is hundreds of fragments) must not overflow the kernel's buffer.
            try { s.ReceiveBufferSize = 4 * 1024 * 1024; s.SendBufferSize = 4 * 1024 * 1024; } catch (SocketException) { }
            if (OperatingSystem.IsWindows())
            {
                // Windows reports an ICMP "port unreachable" from an earlier send as an error on the next
                // receive (SIO_UDP_CONNRESET); a server must not lose its socket to one gone client.
                try { s.IOControl(unchecked((int)0x9800000C), new byte[] { 0 }, null); } catch (SocketException) { }
            }
            return s;
        }

        public static UdpTransport Listen(string address, int port)
        {
            var ip = string.IsNullOrEmpty(address) || address == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(address);
            var s = NewSocket(ip.AddressFamily);
            s.ExclusiveAddressUse = true; // a second instance on the same port must fail and pick another
            try { s.Bind(new IPEndPoint(ip, port)); }
            catch { s.Dispose(); throw; }
            return new UdpTransport(true, s, null, 0);
        }

        /// <summary>Connects in the background; a Connected or Disconnected event reports the outcome.</summary>
        public static UdpTransport Connect(string address, int port, int timeoutMs)
        {
            IPAddress ip;
            if (!IPAddress.TryParse(address, out ip))
            {
                try { ip = Array.Find(Dns.GetHostAddresses(address), a => a.AddressFamily == AddressFamily.InterNetwork) ?? IPAddress.Loopback; }
                catch (SocketException) { ip = IPAddress.Loopback; }
            }
            var s = NewSocket(ip.AddressFamily);
            s.Bind(new IPEndPoint(ip.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
            return new UdpTransport(false, s, new IPEndPoint(ip, port), timeoutMs);
        }

        static ulong RandomU64()
        {
            Span<byte> b = stackalloc byte[8];
            RandomNumberGenerator.Fill(b);
            ulong v = BitConverter.ToUInt64(b);
            return v == 0 ? 1 : v;
        }

        // ── Main-thread surface ────────────────────────────────────

        public void Send(int peer, byte[] payload, int length = -1) => Send(peer, payload, length, NetChannel.Reliable);

        public void Send(int peer, byte[] payload, int length, NetChannel channel)
        {
            if (_disposed) return;
            if (length < 0) length = payload.Length;
            var copy = new byte[length];
            Buffer.BlockCopy(payload, 0, copy, 0, length);
            // An unreliable frame must fit one packet; a bigger one goes reliably rather than in pieces that could half-arrive.
            if (channel == NetChannel.Unreliable && length > FragmentBytes) channel = NetChannel.Reliable;
            _outbox.Enqueue((IsServer ? peer : 0, copy, channel));
        }

        /// <summary>Unreliable frames sent, for the stats and the tests.</summary>
        public long UnreliableSent;

        public void Disconnect(int peer)
        {
            if (!_disposed) _disconnects.Enqueue(IsServer ? peer : 0);
        }

        public bool Poll(out NetEvent e) => _inbox.TryDequeue(out e);

        /// <summary>
        /// Closes like a TCP socket does: frames already queued still go out (for up to <see cref="LingerMs"/>),
        /// then every peer gets a Disconnect and the socket closes, on the network thread. Returns at once.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }

        // ── The network thread ─────────────────────────────────────

        void Loop()
        {
            try
            {
                while (!_stopped)
                {
                    bool readable;
                    try { readable = _sock.Poll(1000, SelectMode.SelectRead); }
                    catch (ObjectDisposedException) { return; }
                    if (readable) Receive();
                    double now = Now;
                    TakeOutbox();
                    if (_disposed)
                    {
                        if (_disposedAt < 0)
                        {
                            _disposedAt = now;
                            _connecting = false;
                            foreach (var peer in _byId.Values) if (!peer.Closing) { peer.Closing = true; peer.ClosingSince = now; }
                        }
                        if (_byId.Count == 0 || now - _disposedAt > LingerMs) { Shutdown(); return; }
                    }
                    TakeDisconnects(now);
                    if (!IsServer && _connecting) ServiceConnect(now);
                    ServicePeers(now);
                }
            }
            catch (Exception e) { Console.WriteLine($"[net] udp transport stopped: {e.Message}"); }
            finally { try { _sock.Dispose(); } catch { } }
        }

        void Receive()
        {
            while (true)
            {
                int n;
                EndPoint from = new IPEndPoint(_sock.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
                try
                {
                    if (_sock.Available <= 0) return;
                    n = _sock.ReceiveFrom(_rx, ref from);
                }
                catch (SocketException) { continue; } // an ICMP error about an earlier send: not this packet
                catch (ObjectDisposedException) { return; }
                if (n > 0) Handle(n, from);
            }
        }

        void Handle(int n, EndPoint from)
        {
            byte type = _rx[0];
            double now = Now;
            var key = from.ToString();
            switch (type)
            {
                case TConnectRequest when IsServer && n >= 14 && !_disposed:
                {
                    if (BitConverter.ToUInt32(_rx, 1) != Magic || _rx[5] != Version) return;
                    ulong nonce = BitConverter.ToUInt64(_rx, 6);
                    if (_byEndPoint.TryGetValue(key, out var existing))
                    {
                        if (existing.Nonce == nonce) { SendAccept(existing); return; }
                        Remove(existing, notifyRemote: false); // the client restarted on the same address
                    }
                    var p = new Peer { Id = ++_nextId, EndPoint = from, Token = RandomU64(), Nonce = nonce, LastRecv = now, LastSend = now };
                    _byEndPoint[key] = p;
                    _byId[p.Id] = p;
                    _peerCount = _byId.Count;
                    _inbox.Enqueue(new NetEvent(NetEventKind.Connected, p.Id, null));
                    SendAccept(p);
                    return;
                }
                case TConnectAccept when !IsServer && n >= 17:
                {
                    if (!_connecting || BitConverter.ToUInt64(_rx, 1) != _nonce || !SameEndPoint(from, _server)) return;
                    _connecting = false;
                    // Keyed by the address that answered (a loopback server may answer from another loopback address).
                    var p = new Peer { Id = 0, EndPoint = from, Token = BitConverter.ToUInt64(_rx, 9), LastRecv = now, LastSend = now };
                    _byEndPoint[key] = p;
                    _byId[0] = p;
                    _peerCount = 1;
                    _inbox.Enqueue(new NetEvent(NetEventKind.Connected, 0, null));
                    return;
                }
                case TData when n >= HeaderBytes:
                {
                    if (!_byEndPoint.TryGetValue(key, out var p) || BitConverter.ToUInt64(_rx, 1) != p.Token) return;
                    p.LastRecv = now;
                    Acked(p, BitConverter.ToUInt32(_rx, 9), BitConverter.ToUInt64(_rx, 13), now);
                    int at = HeaderBytes;
                    while (at + FragmentHeaderBytes <= n)
                    {
                        uint seq = BitConverter.ToUInt32(_rx, at);
                        bool last = (_rx[at + 4] & LastFlag) != 0;
                        int len = BitConverter.ToUInt16(_rx, at + 5);
                        at += FragmentHeaderBytes;
                        if (at + len > n) return; // malformed: drop the rest
                        if ((_rx[at - FragmentHeaderBytes + 4] & UnreliableFlag) != 0)
                            _inbox.Enqueue(new NetEvent(NetEventKind.Data, p.Id, _rx.AsSpan(at, len).ToArray(), NetChannel.Unreliable));
                        else Arrived(p, seq, last, _rx, at, len);
                        at += len;
                    }
                    return;
                }
                case TDisconnect when n >= 9:
                {
                    if (!_byEndPoint.TryGetValue(key, out var p) || BitConverter.ToUInt64(_rx, 1) != p.Token) return;
                    Remove(p, notifyRemote: false);
                    return;
                }
            }
        }

        static bool SameEndPoint(EndPoint a, IPEndPoint b)
            => a is IPEndPoint ia && ia.Port == b.Port && (ia.Address.Equals(b.Address) || (IPAddress.IsLoopback(ia.Address) && IPAddress.IsLoopback(b.Address)));

        void SendAccept(Peer p)
        {
            _tx[0] = TConnectAccept;
            BitConverter.TryWriteBytes(_tx.AsSpan(1), p.Nonce);
            BitConverter.TryWriteBytes(_tx.AsSpan(9), p.Token);
            SendRaw(p.EndPoint, 17);
        }

        void SendRaw(EndPoint to, int length)
        {
            DatagramsSent++;
            if (DropPercent > 0 && _dropRng.NextDouble() * 100 < DropPercent) return;
            try { _sock.SendTo(_tx, 0, length, SocketFlags.None, to); }
            catch (SocketException) { } // the line is down or the buffer full: the resend timer covers it
            catch (ObjectDisposedException) { }
        }

        /// <summary>The remote acknowledged everything below <paramref name="next"/>, and the fragments flagged in <paramref name="bits"/>.</summary>
        void Acked(Peer p, uint next, ulong bits, double now)
        {
            if (p.InFlight.Count == 0) return;
            List<uint> done = null;
            foreach (var kv in p.InFlight)
            {
                uint seq = kv.Key;
                bool acked = seq < next || (seq > next && seq - next - 1 < 64 && (bits & (1UL << (int)(seq - next - 1))) != 0);
                if (!acked) { if (seq >= next + 65) break; continue; }
                (done ??= new List<uint>()).Add(seq);
                var f = kv.Value;
                if (f.Sends == 1) Sample(p, now - f.SentAt); // Karn: a resent fragment's ack is ambiguous
            }
            if (done != null) foreach (var s in done) p.InFlight.Remove(s);
        }

        static void Sample(Peer p, double ms)
        {
            if (p.Srtt < 0) { p.Srtt = ms; p.RttVar = ms / 2; }
            else
            {
                p.RttVar = 0.75 * p.RttVar + 0.25 * Math.Abs(p.Srtt - ms);
                p.Srtt = 0.875 * p.Srtt + 0.125 * ms;
            }
            p.Rto = Math.Clamp(p.Srtt + 4 * p.RttVar + 5, MinRtoMs, MaxRtoMs);
        }

        void Arrived(Peer p, uint seq, bool last, byte[] buf, int offset, int len)
        {
            p.AckDirty = true;
            if (seq < p.NextExpected) return; // a duplicate: the ack it gets says so
            if (seq != p.NextExpected)
            {
                if (seq - p.NextExpected < ReorderWindow && !p.Early.ContainsKey(seq))
                    p.Early[seq] = (buf.AsSpan(offset, len).ToArray(), last);
                return;
            }
            Deliver(p, buf, offset, len, last);
            while (p.Early.Remove(p.NextExpected, out var e)) Deliver(p, e.data, 0, e.data.Length, e.last);
        }

        void Deliver(Peer p, byte[] buf, int offset, int len, bool last)
        {
            p.NextExpected++;
            if (p.Assembly.Length + len > MaxFrame) { Remove(p, notifyRemote: true); return; }
            p.Assembly.Write(buf, offset, len);
            if (!last) return;
            _inbox.Enqueue(new NetEvent(NetEventKind.Data, p.Id, p.Assembly.ToArray()));
            p.Assembly.SetLength(0);
        }

        void TakeOutbox()
        {
            while (_outbox.TryDequeue(out var o))
            {
                if (!_byId.TryGetValue(o.peer, out var p) || p.Closing) continue;
                var data = o.data;
                if (o.channel == NetChannel.Unreliable) { p.Unreliable.Enqueue(data); continue; }
                int at = 0;
                do
                {
                    int len = Math.Min(FragmentBytes, data.Length - at);
                    p.Waiting.Enqueue(new Fragment { Seq = p.NextSeq++, Data = data, Offset = at, Length = len, Last = at + len >= data.Length });
                    at += len;
                } while (at < data.Length);
            }
        }

        void TakeDisconnects(double now)
        {
            while (_disconnects.TryDequeue(out int id))
            {
                if (!_byId.TryGetValue(id, out var p) || p.Closing) continue;
                p.Closing = true;
                p.ClosingSince = now;
            }
        }

        void ServiceConnect(double now)
        {
            if (now > _connectDeadline)
            {
                _connecting = false;
                Console.WriteLine($"[net] connect to {_server} timed out");
                _inbox.Enqueue(new NetEvent(NetEventKind.Disconnected, 0, null));
                return;
            }
            if (_lastConnectSend >= 0 && now - _lastConnectSend < ConnectRetryMs) return;
            _lastConnectSend = now;
            _tx[0] = TConnectRequest;
            BitConverter.TryWriteBytes(_tx.AsSpan(1), Magic);
            _tx[5] = Version;
            BitConverter.TryWriteBytes(_tx.AsSpan(6), _nonce);
            SendRaw(_server, 14);
        }

        void ServicePeers(double now)
        {
            if (_byId.Count == 0) return;
            List<Peer> gone = null;
            foreach (var p in _byId.Values)
            {
                if (now - p.LastRecv > SilenceTimeoutMs)
                {
                    Console.WriteLine($"[net] udp peer {p.Id} timed out ({SilenceTimeoutMs} ms without a packet)");
                    (gone ??= new List<Peer>()).Add(p);
                    continue;
                }
                Flush(p, now);
                if (p.Closing && ((p.InFlight.Count == 0 && p.Waiting.Count == 0) || now - p.ClosingSince > LingerMs))
                    (gone ??= new List<Peer>()).Add(p);
            }
            if (gone == null) return;
            foreach (var p in gone) Remove(p, notifyRemote: p.Closing);
        }

        /// <summary>Sends whatever is due to one peer: resends past their timeout, new fragments the window allows, and the ack.</summary>
        void Flush(Peer p, double now)
        {
            int used = 0;
            bool any = false;
            foreach (var f in p.InFlight.Values)
            {
                if (f.SentAt >= 0 && now - f.SentAt < p.Rto * Math.Min(8, 1 << Math.Max(0, f.Sends - 1))) continue; // backoff per resend
                Resends++;
                Put(p, f, ref used, now);
                any = true;
            }
            while (p.Waiting.Count > 0 && p.InFlight.Count < Window)
            {
                var f = p.Waiting.Dequeue();
                p.InFlight[f.Seq] = f;
                Put(p, f, ref used, now);
                any = true;
            }
            while (p.Unreliable.Count > 0)
            {
                var data = p.Unreliable.Dequeue();
                if (used > 0 && used + FragmentHeaderBytes + data.Length > MaxPacket) { EndPacket(p, used, now); used = 0; }
                if (used == 0) { BeginPacket(p); used = HeaderBytes; }
                BitConverter.TryWriteBytes(_tx.AsSpan(used), 0u);
                _tx[used + 4] = UnreliableFlag;
                BitConverter.TryWriteBytes(_tx.AsSpan(used + 5), (ushort)data.Length);
                Buffer.BlockCopy(data, 0, _tx, used + FragmentHeaderBytes, data.Length);
                used += FragmentHeaderBytes + data.Length;
                UnreliableSent++;
                any = true;
            }
            if (used > 0) EndPacket(p, used, now);
            else if (p.AckDirty || (!any && now - p.LastSend > KeepaliveMs)) { BeginPacket(p); EndPacket(p, HeaderBytes, now); }
        }

        void BeginPacket(Peer p)
        {
            _tx[0] = TData;
            BitConverter.TryWriteBytes(_tx.AsSpan(1), p.Token);
            BitConverter.TryWriteBytes(_tx.AsSpan(9), p.NextExpected);
            ulong bits = 0;
            if (p.Early.Count > 0)
                for (int i = 0; i < 64; i++)
                    if (p.Early.ContainsKey(p.NextExpected + 1 + (uint)i)) bits |= 1UL << i;
            BitConverter.TryWriteBytes(_tx.AsSpan(13), bits);
        }

        void Put(Peer p, Fragment f, ref int used, double now)
        {
            if (used > 0 && used + FragmentHeaderBytes + f.Length > MaxPacket) { EndPacket(p, used, now); used = 0; }
            if (used == 0) { BeginPacket(p); used = HeaderBytes; }
            BitConverter.TryWriteBytes(_tx.AsSpan(used), f.Seq);
            _tx[used + 4] = f.Last ? LastFlag : (byte)0;
            BitConverter.TryWriteBytes(_tx.AsSpan(used + 5), (ushort)f.Length);
            Buffer.BlockCopy(f.Data, f.Offset, _tx, used + FragmentHeaderBytes, f.Length);
            used += FragmentHeaderBytes + f.Length;
            f.SentAt = now;
            f.Sends++;
        }

        void EndPacket(Peer p, int length, double now)
        {
            SendRaw(p.EndPoint, length);
            p.LastSend = now;
            p.AckDirty = false;
        }

        void Remove(Peer p, bool notifyRemote)
        {
            // Forget it first: once the remote reads the Disconnect, this side already counts one fewer peer.
            _byEndPoint.Remove(p.EndPoint.ToString());
            _byId.Remove(p.Id);
            _peerCount = _byId.Count;
            if (notifyRemote) SendDisconnect(p);
            _inbox.Enqueue(new NetEvent(NetEventKind.Disconnected, p.Id, null));
        }

        void SendDisconnect(Peer p)
        {
            _tx[0] = TDisconnect;
            BitConverter.TryWriteBytes(_tx.AsSpan(1), p.Token);
            for (int i = 0; i < 3; i++) SendRaw(p.EndPoint, 9); // three, so one lost datagram does not leave the peer waiting out the timeout
        }

        void Shutdown()
        {
            foreach (var p in new List<Peer>(_byId.Values)) SendDisconnect(p);
            _byId.Clear();
            _byEndPoint.Clear();
            _peerCount = 0;
            _stopped = true;
        }
    }
}
