using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Prisma.RelaySpike
{
    public delegate void RelayDataHandler(Guid from, ReadOnlySpan<byte> payload);

    /// <summary>
    /// One client's binding to a Relay server: BIND until BIND_RECEIVED, keep it alive with PING
    /// (the server drops a client after 10 s of silence), CONNECT_REQUEST to a host's connection
    /// data until ACCEPTED, then RELAY datagrams to and from connected allocation ids.
    /// A background thread owns receiving; <see cref="Data"/> is raised on that thread.
    /// This is the shape a Prisma <c>INetTransport</c> backend would wrap (reliability and
    /// ordering would sit on top, e.g. LiteNetLib or our own layer).
    /// </summary>
    public sealed class RelayConnection : IDisposable
    {
        readonly RelayAllocation _alloc;
        readonly IDatagramChannel _ch;
        readonly Thread _rx;
        readonly Timer _keepAlive;
        readonly byte[] _tx = new byte[2048];
        readonly object _txLock = new();
        readonly ManualResetEventSlim _bound = new();
        readonly ConcurrentDictionary<Guid, ManualResetEventSlim> _accepted = new();
        readonly ConcurrentDictionary<ushort, long> _pingSent = new();
        readonly ConcurrentQueue<double> _serverRtts = new();
        volatile bool _closed;
        ushort _bindNonce, _pingNo;

        public string Label { get; }
        public Guid AllocationId => _alloc.AllocationId;
        public event RelayDataHandler? Data;
        public event Action<Guid>? PeerDisconnected;
        public Action<string> Log { get; set; } = _ => { };
        public long DatagramsSent, DatagramsReceived, RelaySent, RelayReceived, Errors;
        public RelayErrorCode? LastError { get; private set; }
        public string ChannelName => _ch.Name;

        public RelayConnection(string label, RelayAllocation alloc, IDatagramChannel channel)
        {
            Label = label; _alloc = alloc; _ch = channel;
            _rx = new Thread(ReceiveLoop) { IsBackground = true, Name = "relay-rx-" + label };
            _rx.Start();
            _keepAlive = new Timer(_ => { if (_bound.IsSet && !_closed) PingServer(); }, null, 1000, 1000);
        }

        /// <summary>BIND (resent every 250 ms with the same nonce) until BIND_RECEIVED. Returns the time it took.</summary>
        public TimeSpan Bind(int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                lock (_txLock) Send(RelayProtocol.WriteBind(_tx, _bindNonce, _alloc.ConnectionData, _alloc.Key));
                if (_bound.Wait(250)) return sw.Elapsed;
            }
            throw new TimeoutException($"[{Label}] no BIND_RECEIVED within {timeoutMs} ms (last error: {LastError?.ToString() ?? "none"})");
        }

        /// <summary>CONNECT_REQUEST with the host's connection data until ACCEPTED. Returns the host's allocation id.</summary>
        public Guid Connect(byte[] hostConnectionData, int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            var anyAccept = _accepted.GetOrAdd(Guid.Empty, _ => new ManualResetEventSlim());
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                lock (_txLock) Send(RelayProtocol.WriteConnectRequest(_tx, _alloc.AllocationIdBytes, hostConnectionData));
                if (anyAccept.Wait(250))
                {
                    foreach (var kv in _accepted) if (kv.Key != Guid.Empty) return kv.Key;
                }
            }
            throw new TimeoutException($"[{Label}] no ACCEPTED within {timeoutMs} ms (last error: {LastError?.ToString() ?? "none"})");
        }

        public void SendTo(Guid peer, ReadOnlySpan<byte> payload)
        {
            Span<byte> to = stackalloc byte[16];
            GuidCodec.Write(peer, to);
            lock (_txLock)
            {
                Send(RelayProtocol.WriteRelay(_tx, _alloc.AllocationIdBytes, to, payload));
                RelaySent++;
            }
        }

        public void Disconnect(Guid peer)
        {
            Span<byte> to = stackalloc byte[16];
            GuidCodec.Write(peer, to);
            lock (_txLock) Send(RelayProtocol.WriteDisconnect(_tx, _alloc.AllocationIdBytes, to));
        }

        public void PingServer()
        {
            lock (_txLock)
            {
                ushort n = ++_pingNo;
                _pingSent[n] = Stopwatch.GetTimestamp();
                Send(RelayProtocol.WritePing(_tx, _alloc.AllocationIdBytes, n));
            }
        }

        /// <summary>Round-trip times (ms) of PINGs the Relay server echoed so far.</summary>
        public double[] ServerRtts() => _serverRtts.ToArray();

        void Send(int length)
        {
            _ch.Send(_tx.AsSpan(0, length));
            Interlocked.Increment(ref DatagramsSent);
        }

        void ReceiveLoop()
        {
            var buf = new byte[2048];
            while (!_closed)
            {
                int n = _ch.Receive(buf, 200);
                if (n <= 0) continue;
                Interlocked.Increment(ref DatagramsReceived);
                if (!RelayProtocol.TryParse(buf.AsSpan(0, n), out var m)) { Log($"[{Label}] unparsed datagram ({n} B, first bytes {Convert.ToHexString(buf, 0, Math.Min(n, 4))})"); continue; }
                switch (m.Type)
                {
                    case RelayMessageType.BindReceived:
                        if (!_bound.IsSet) Log($"[{Label}] BIND_RECEIVED");
                        _bound.Set();
                        break;
                    case RelayMessageType.Ping:
                        if (_pingSent.TryRemove(m.PingNumber, out long t0))
                            _serverRtts.Enqueue(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
                        break;
                    case RelayMessageType.Accepted:
                    {
                        var target = GuidCodec.Read(m.FromAllocationId);
                        Log($"[{Label}] ACCEPTED by {target}");
                        _accepted.GetOrAdd(target, _ => new ManualResetEventSlim(true)).Set();
                        _accepted.GetOrAdd(Guid.Empty, _ => new ManualResetEventSlim()).Set();
                        break;
                    }
                    case RelayMessageType.Relay:
                        Interlocked.Increment(ref RelayReceived);
                        Data?.Invoke(GuidCodec.Read(m.FromAllocationId), m.Payload);
                        break;
                    case RelayMessageType.Disconnect:
                        PeerDisconnected?.Invoke(GuidCodec.Read(m.FromAllocationId));
                        break;
                    case RelayMessageType.Error:
                        Interlocked.Increment(ref Errors);
                        LastError = m.Error;
                        Log($"[{Label}] ERROR {m.Error}");
                        break;
                }
            }
        }

        /// <summary>CLOSE is best effort and idempotent; the docs suggest sending it more than once.</summary>
        public void Close()
        {
            if (_closed) return;
            if (_bound.IsSet)
                for (int i = 0; i < 3; i++) { lock (_txLock) Send(RelayProtocol.WriteClose(_tx, _alloc.AllocationIdBytes)); Thread.Sleep(20); }
            _closed = true;
            _keepAlive.Dispose();
            _rx.Join(1000);
        }

        public void Dispose() { Close(); _ch.Dispose(); }
    }

    /// <summary>
    /// Allocation ids on the wire are the 16 bytes of <c>allocationIdBytes</c>, which are the UUID
    /// in RFC 4122 (big-endian) order, not .NET's mixed-endian Guid layout.
    /// </summary>
    public static class GuidCodec
    {
        public static void Write(Guid g, Span<byte> dst) => g.TryWriteBytes(dst, bigEndian: true, out _);
        public static Guid Read(ReadOnlySpan<byte> src) => new(src, bigEndian: true);
    }
}
