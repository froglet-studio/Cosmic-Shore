using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// A <see cref="IDatagramLink"/> through a relay server speaking Unity Relay's protocol
    /// (<see cref="RelayProtocol"/>; docs/MULTIPLAYER.md §6.7). Nobody opens a port: every player only
    /// sends to the relay, which forwards between allocations it has seen connect.
    ///
    ///   1. BIND (signed with the allocation's key) every 200 ms until BIND_RECEIVED.
    ///   2. A joiner sends CONNECT_REQUEST naming the host's connection data until ACCEPTED, which names
    ///      the host's allocation: from then on <see cref="ServerKey"/> is known and the transport's own
    ///      handshake runs inside RELAY messages. A host waits; its clients reach it.
    ///   3. PING every second keeps the binding alive (the relay drops a client after 10 s of silence).
    ///   4. An ERROR asking for a re-bind (client/player mismatch, timeout) re-binds with the next nonce; an
    ///      ERROR naming the allocation as unknown or unauthorized is a <see cref="Failure"/>.
    ///   5. CLOSE (three times) on dispose.
    /// A peer's key is its allocation id in hex. Every member runs on the transport's network thread.
    /// </summary>
    internal sealed class RelayLink : IDatagramLink
    {
        const int RetryMs = 200, PingMs = 1000;

        readonly Socket _sock;
        readonly IPEndPoint _relay;
        readonly RelayAllocation _alloc;
        readonly bool _host;
        readonly byte[] _rx = new byte[2048];
        readonly byte[] _tx = new byte[RelayProtocol.RelayHeaderLength + RelayProtocol.MaxRelayContent];
        readonly Dictionary<string, byte[]> _ids = new();
        ushort _nonce, _pingNumber;
        bool _bound, _accepted;
        double _lastBind = double.MinValue, _lastConnect = double.MinValue, _lastPing = double.MinValue;
        readonly HashSet<byte> _reported = new();

        public int LocalPort => 0;
        public string ServerKey { get; private set; }
        public string Failure { get; private set; }
        public bool Bound => _bound;

        public RelayLink(RelayAllocation allocation, bool host)
        {
            _alloc = allocation;
            _host = host;
            var ip = DirectLink.Resolve(allocation.ServerHost);
            _relay = new IPEndPoint(ip, allocation.ServerPort);
            _sock = DirectLink.NewSocket(ip.AddressFamily);
            _sock.Bind(new IPEndPoint(ip.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
        }

        public bool Wait(int microseconds)
        {
            try { return _sock.Poll(microseconds, SelectMode.SelectRead); }
            catch (ObjectDisposedException) { return false; }
        }

        public void Service(double nowMs)
        {
            if (Failure != null) return;
            if (!_bound)
            {
                if (nowMs - _lastBind < RetryMs) return;
                _lastBind = nowMs;
                Raw(RelayProtocol.WriteBind(_tx, _nonce, _alloc.ConnectionData, _alloc.Key));
                return;
            }
            if (!_host && !_accepted && nowMs - _lastConnect >= RetryMs)
            {
                _lastConnect = nowMs;
                Raw(RelayProtocol.WriteConnectRequest(_tx, _alloc.AllocationIdBytes, _alloc.HostConnectionData));
            }
            if (nowMs - _lastPing >= PingMs)
            {
                _lastPing = nowMs;
                Raw(RelayProtocol.WritePing(_tx, _alloc.AllocationIdBytes, ++_pingNumber));
            }
        }

        void Raw(int length)
        {
            try { _sock.SendTo(_tx, 0, length, SocketFlags.None, _relay); }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }

        public int Receive(byte[] buffer, out string from)
        {
            from = null;
            while (true)
            {
                EndPoint ep = new IPEndPoint(_sock.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
                int n;
                try
                {
                    if (_sock.Available <= 0) return -1;
                    n = _sock.ReceiveFrom(_rx, ref ep);
                }
                catch (SocketException) { continue; }
                catch (ObjectDisposedException) { return -1; }
                if (!RelayProtocol.TryReadHeader(_rx.AsSpan(0, n), out byte type)) continue; // not from a relay
                var self = _alloc.AllocationIdBytes.AsSpan();
                switch (type)
                {
                    case RelayProtocol.BindReceived:
                        _bound = true;
                        continue;
                    case RelayProtocol.Accepted when n >= RelayProtocol.AcceptedLength:
                        if (!_host && _rx.AsSpan(20, 16).SequenceEqual(self))
                        {
                            _accepted = true;
                            ServerKey = Remember(_rx.AsSpan(4, 16));
                        }
                        continue;
                    case RelayProtocol.Relay when n >= RelayProtocol.RelayHeaderLength:
                    {
                        int len = RelayProtocol.RelayContentLength(_rx);
                        if (RelayProtocol.RelayHeaderLength + len > n || !_rx.AsSpan(20, 16).SequenceEqual(self)) continue;
                        Buffer.BlockCopy(_rx, RelayProtocol.RelayHeaderLength, buffer, 0, len);
                        from = Remember(_rx.AsSpan(4, 16));
                        return len;
                    }
                    case RelayProtocol.Error when n >= RelayProtocol.ErrorLength:
                        OnError(_rx[20]);
                        continue;
                    default:
                        continue; // PING echoes, DISCONNECT confirmations
                }
            }
        }

        void OnError(byte code)
        {
            if (code == RelayProtocol.ErrClientPlayerMismatch || code == RelayProtocol.ErrTimeout)
            {
                // The relay lost this binding (a new local port, or silence): bind again with the next nonce.
                _bound = false;
                _nonce++;
                _lastBind = double.MinValue;
                Report(code, "re-binding");
                return;
            }
            if (code == RelayProtocol.ErrAllocationNotFound || code == RelayProtocol.ErrUnauthorized || code == RelayProtocol.ErrInvalidProtocolVersion
                || code == RelayProtocol.ErrSelfConnectNotAllowed)
            {
                Failure = "relay: " + RelayProtocol.ErrorName(code);
                Report(code, "giving up");
                return;
            }
            Report(code, "ignored"); // NotConnected: the peer is gone; the transport's timeout handles it
        }

        void Report(byte code, string action)
        {
            if (_reported.Add(code)) Console.WriteLine($"[relay] {RelayProtocol.ErrorName(code)} from the relay: {action}");
        }

        string Remember(ReadOnlySpan<byte> id)
        {
            var key = RelayProtocol.Key(id);
            if (!_ids.ContainsKey(key)) _ids[key] = id.ToArray();
            return key;
        }

        public void Send(byte[] buffer, int length, string to)
        {
            if (!_bound || to == null || length > RelayProtocol.MaxRelayContent || !_ids.TryGetValue(to, out var target)) return;
            Raw(RelayProtocol.WriteRelay(_tx, _alloc.AllocationIdBytes, target, buffer.AsSpan(0, length)));
        }

        public void Dispose()
        {
            if (_bound) for (int i = 0; i < 3; i++) Raw(RelayProtocol.WriteClose(_tx, _alloc.AllocationIdBytes));
            try { _sock.Dispose(); } catch { }
        }
    }
}
