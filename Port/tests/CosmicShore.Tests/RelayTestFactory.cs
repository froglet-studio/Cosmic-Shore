using System;
using System.Collections.Generic;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Runs the transport contract through a relay: Listen allocates a host on an in-process
    /// <see cref="FrogletRelayServer"/> and hands out a stand-in port that names its join code; Connect to
    /// that port joins by the code. So a contract check written for sockets exercises the relay path.
    /// </summary>
    sealed class RelayTestFactory : INetTransportFactory
    {
        static readonly Lazy<FrogletRelayServer> s_server = new(() => FrogletRelayServer.Start());
        static int s_nextPort = 50000;
        static readonly Dictionary<int, (string code, Hosted host)> s_hosts = new();

        sealed class Hosted : INetTransport
        {
            readonly INetTransport _inner;
            public bool Disposed;
            public Hosted(INetTransport inner, int port) { _inner = inner; ListenPort = port; }
            public bool IsServer => true;
            public int ListenPort { get; }
            public int PeerCount => _inner.PeerCount;
            public void Send(int peer, byte[] payload, int length = -1) => _inner.Send(peer, payload, length);
            public void Send(int peer, byte[] payload, int length, NetChannel channel) => _inner.Send(peer, payload, length, channel);
            public void Disconnect(int peer) => _inner.Disconnect(peer);
            public bool Poll(out NetEvent e) => _inner.Poll(out e);
            public void Dispose() { Disposed = true; _inner.Dispose(); }
        }

        public INetTransport Listen(string address, int port)
        {
            var server = s_server.Value;
            lock (s_hosts)
            {
                if (port != 0 && s_hosts.TryGetValue(port, out var taken) && !taken.host.Disposed)
                    throw new InvalidOperationException($"relay stand-in port {port} is taken");
                var alloc = server.Allocate(8);
                string code = server.CreateJoinCode(alloc.AllocationId);
                int p = port != 0 ? port : s_nextPort++;
                var hosted = new Hosted(UdpTransport.Over(new RelayLink(alloc, host: true), server: true), p);
                s_hosts[p] = (code, hosted);
                return hosted;
            }
        }

        public INetTransport Connect(string address, int port, int timeoutMs)
        {
            string code;
            lock (s_hosts) code = s_hosts.TryGetValue(port, out var h) ? h.code : null;
            if (code == null) throw new InvalidOperationException($"no relay host behind stand-in port {port}");
            return UdpTransport.Over(new RelayLink(s_server.Value.Join(code), host: false), server: false, timeoutMs);
        }
    }
}
