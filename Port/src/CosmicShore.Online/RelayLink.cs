using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Online
{
    /// <summary>A Relay allocation as a peer address: its 16 id bytes. Equal ids print equal, which is how the transport keys peers.</summary>
    internal sealed class RelayPeer : EndPoint
    {
        public readonly Guid Id;
        public readonly byte[] IdBytes;
        readonly string _text;

        public RelayPeer(byte[] idBytes, string text = null)
        {
            IdBytes = idBytes;
            Id = idBytes.Length == 16 ? new Guid(idBytes, bigEndian: true) : Guid.Empty;
            _text = text ?? "relay:" + Id;
        }

        public override string ToString() => _text;
        public override bool Equals(object obj) => obj is RelayPeer p && p._text == _text;
        public override int GetHashCode() => _text.GetHashCode();
    }

    /// <summary>
    /// Unity Relay as an <see cref="IDatagramLink"/>: <see cref="UdpTransport"/>'s packets travel as the
    /// content of RELAY messages, so Prisma's own reliability layer (acks, resends, ordering, fragments,
    /// keepalive, timeouts) runs unchanged across the internet. The link only speaks the Relay protocol
    /// (https://docs.unity.com/en-us/relay/relay-message-protocol):
    ///
    ///  • BIND (signed with the allocation key) every 250 ms until BIND_RECEIVED; a ClientPlayerMismatch
    ///    error re-binds with the next nonce, a binding Timeout error re-binds.
    ///  • A joiner then sends CONNECT_REQUEST (its id + the host's connection data) every 250 ms until
    ///    ACCEPTED, which names the host's allocation id. Nothing is sent before then.
    ///  • A PING every second once bound: the server forgets an allocation idle for 10 s, and its echo
    ///    tells us the server is still there.
    ///  • The host is told of nobody: it learns a peer from the first RELAY that peer sends (the transport's
    ///    ConnectRequest), and answers it by allocation id.
    ///  • On dispose, CLOSE three times (the server frees the slot at once rather than timing it out).
    ///
    /// The channel (UDP or DTLS) is opened on the first <see cref="Service"/> call, on the transport's thread,
    /// so a DTLS handshake never blocks the game's main thread. A host's link answers <see cref="UdpTransport"/>
    /// with a <see cref="RelayPeer"/> per joiner; a joiner's link answers with the single <see cref="Host"/>.
    /// </summary>
    internal sealed class RelayLink : IDatagramLink
    {
        public const int BindRetryMs = 250, ConnectRetryMs = 250, PingMs = 1000;
        /// <summary>No word from the Relay server for this long (its PING echoes included) means it is gone.</summary>
        public const int ServerSilenceMs = 15000;
        /// <summary>How long binding (and, for a joiner, being accepted) may take before the link gives up.</summary>
        public const int SetupTimeoutMs = 15000;
        const int MaxPeers = 256;

        /// <summary>A joiner's only peer: the host, whatever its allocation id turns out to be.</summary>
        public static readonly RelayPeer Host = new(new byte[16], "relay:host");

        readonly RelayAllocation _alloc;
        readonly bool _isHost;
        readonly Func<IRelayChannel> _open;
        readonly byte[] _raw = new byte[2048];
        readonly byte[] _out = new byte[2048];
        readonly Dictionary<Guid, RelayPeer> _peers = new();
        IRelayChannel _ch;
        byte[] _hostId; // a joiner's: from ACCEPTED

        enum State { Opening, Binding, Connecting, Ready, Failed }
        State _state = State.Opening;
        ushort _nonce;
        ushort _pingNumber;
        double _lastBind = -1, _lastConnect = -1, _lastPing = -1, _lastHeard, _setupDeadline;
        bool _bound;

        /// <summary>Tests shorten <see cref="SetupTimeoutMs"/>.</summary>
        internal int SetupTimeout = SetupTimeoutMs;

        public string Fault { get; private set; }
        public int LocalPort => 0;
        public string ChannelName => _ch?.Name ?? "";
        public bool IsReady => _state == State.Ready;
        /// <summary>The latest PING round trip to the Relay server, ms (-1 before the first echo).</summary>
        public double ServerRttMs { get; private set; } = -1;
        readonly Dictionary<ushort, double> _pingSent = new();
        double _now;

        /// <param name="open">Opens the channel to the Relay server (blocking is fine: it runs on the transport thread).</param>
        public RelayLink(RelayAllocation allocation, bool isHost, Func<IRelayChannel> open)
        {
            _alloc = allocation;
            _isHost = isHost;
            _open = open;
            if (!isHost && (allocation.HostConnectionData == null || allocation.HostConnectionData.Length == 0))
                throw new ArgumentException("a joiner's allocation must carry the host's connection data (from /join)");
            Span<byte> b = stackalloc byte[2];
            RandomNumberGenerator.Fill(b);
            _nonce = (ushort)(b[0] | b[1] << 8);
        }

        /// <summary>The default channel: DTLS to the "dtls" endpoint, or plain UDP to the "udp" endpoint.</summary>
        public static Func<IRelayChannel> Opener(RelayAllocation a, bool dtls) => () =>
        {
            var ep = a.Endpoint(dtls ? "dtls" : "udp");
            return dtls ? new DtlsRelayChannel(ep.Host, ep.Port, a.AllocationId, a.Key) : new UdpRelayChannel(ep.Host, ep.Port);
        };

        public void Service(double nowMs)
        {
            _now = nowMs;
            if (_state == State.Failed) return;
            if (_state == State.Opening)
            {
                try { _ch = _open(); }
                catch (Exception e) { Fail($"could not reach the Relay server: {e.Message}"); return; }
                // The handshake may have taken a while; time setup from here.
                _state = State.Binding;
                _setupDeadline = nowMs + SetupTimeout;
                _lastHeard = nowMs;
                Console.WriteLine($"[relay] {(_isHost ? "host" : "joiner")} allocation {_alloc.AllocationId} over {_ch.Name} ({_alloc.Region})");
            }
            try
            {
                if (_state is State.Binding or State.Connecting && nowMs > _setupDeadline)
                {
                    Fail(_state == State.Binding ? "the Relay server never confirmed the BIND" : "the host's allocation never accepted the connection");
                    return;
                }
                if (_state == State.Binding && (_lastBind < 0 || nowMs - _lastBind >= BindRetryMs))
                {
                    _lastBind = nowMs;
                    _ch.Send(_out.AsSpan(0, RelayProtocol.WriteBind(_out, _nonce, _alloc.ConnectionData, _alloc.Key)));
                }
                if (_state == State.Connecting && (_lastConnect < 0 || nowMs - _lastConnect >= ConnectRetryMs))
                {
                    _lastConnect = nowMs;
                    _ch.Send(_out.AsSpan(0, RelayProtocol.WriteConnectRequest(_out, _alloc.AllocationIdBytes, _alloc.HostConnectionData)));
                }
                if (_bound && (_lastPing < 0 || nowMs - _lastPing >= PingMs))
                {
                    _lastPing = nowMs;
                    _pingNumber++;
                    if (_pingSent.Count > 32) _pingSent.Clear();
                    _pingSent[_pingNumber] = nowMs;
                    _ch.Send(_out.AsSpan(0, RelayProtocol.WritePing(_out, _alloc.AllocationIdBytes, _pingNumber)));
                }
                if (_bound && nowMs - _lastHeard > ServerSilenceMs) Fail($"no word from the Relay server for {ServerSilenceMs / 1000} s");
            }
            catch (RelayChannelException e) { Fail(e.Message); }
        }

        public int Receive(byte[] buffer, int waitMicros, out EndPoint from)
        {
            from = null;
            if (_ch == null || _state == State.Failed)
            {
                if (waitMicros > 0) System.Threading.Thread.Sleep(1);
                return -1;
            }
            while (true)
            {
                int n;
                try { n = _ch.Receive(_raw, waitMicros); }
                catch (RelayChannelException e) { Fail(e.Message); return -1; }
                if (n < 0) return -1;
                waitMicros = 0;
                if (!RelayProtocol.TryParse(_raw.AsSpan(0, n), out var m)) continue;
                _lastHeard = _now;
                switch (m.Type)
                {
                    case RelayMessageType.BindReceived:
                        if (!_bound) { _bound = true; _state = _isHost ? State.Ready : State.Connecting; }
                        break;
                    case RelayMessageType.Accepted:
                        if (!_isHost && _state != State.Ready)
                        {
                            _hostId = m.FromAllocationId.ToArray();
                            _state = State.Ready;
                            Console.WriteLine($"[relay] connected to the host's allocation {new Guid(_hostId, bigEndian: true)}");
                        }
                        break;
                    case RelayMessageType.Ping:
                        if (_pingSent.Remove(m.PingNumber, out double sent)) ServerRttMs = _now - sent;
                        break;
                    case RelayMessageType.Error:
                        OnError(m.Error);
                        if (_state == State.Failed) return -1;
                        break;
                    case RelayMessageType.Close:
                        Fail("the Relay server closed the allocation");
                        return -1;
                    case RelayMessageType.Relay:
                    {
                        if (_state != State.Ready) break;
                        if (m.Payload.Length > buffer.Length) break;
                        if (_isHost) from = PeerFor(m.FromAllocationId);
                        else if (m.FromAllocationId.SequenceEqual(_hostId)) from = Host;
                        if (from == null) break;
                        m.Payload.CopyTo(buffer);
                        return m.Payload.Length;
                    }
                    // DISCONNECT from the server: the remote's own transport says goodbye inside RELAY; ignore.
                }
            }
        }

        RelayPeer PeerFor(ReadOnlySpan<byte> id)
        {
            var g = new Guid(id, bigEndian: true);
            if (_peers.TryGetValue(g, out var p)) return p;
            if (_peers.Count >= MaxPeers) return null;
            p = new RelayPeer(id.ToArray());
            _peers[g] = p;
            return p;
        }

        void OnError(RelayErrorCode code)
        {
            switch (code)
            {
                case RelayErrorCode.ClientPlayerMismatch:
                    // Another socket bound this allocation (or our address changed): bind again with the next nonce.
                    _nonce++;
                    Rebind();
                    break;
                case RelayErrorCode.Timeout:
                    Rebind();
                    break;
                case RelayErrorCode.NotConnected:
                    // A RELAY to someone the server does not connect us to (they left): their timeout will tell.
                    break;
                default:
                    Fail($"Relay error {code}");
                    break;
            }
        }

        void Rebind()
        {
            _bound = false;
            _state = State.Binding;
            _lastBind = -1;
            _setupDeadline = _now + SetupTimeout;
        }

        public void Send(byte[] buffer, int length, EndPoint to)
        {
            if (_state != State.Ready || length > RelayProtocol.MaxRelayContent) return;
            byte[] target = _isHost ? (to as RelayPeer)?.IdBytes : _hostId;
            if (target == null) return;
            int n = RelayProtocol.WriteRelay(_out, _alloc.AllocationIdBytes, target, buffer.AsSpan(0, length));
            try { _ch.Send(_out.AsSpan(0, n)); }
            catch (RelayChannelException e) { Fail(e.Message); }
        }

        public bool IsServer(EndPoint from, EndPoint server) => ReferenceEquals(from, Host) && ReferenceEquals(server, Host);

        void Fail(string why)
        {
            if (_state == State.Failed) return;
            _state = State.Failed;
            Fault = why;
        }

        public void Dispose()
        {
            if (_ch == null) return;
            try
            {
                if (_bound)
                {
                    int n = RelayProtocol.WriteClose(_out, _alloc.AllocationIdBytes);
                    for (int i = 0; i < 3; i++) _ch.Send(_out.AsSpan(0, n));
                }
            }
            catch (Exception) { }
            _ch.Dispose();
            _ch = null;
        }
    }
}
