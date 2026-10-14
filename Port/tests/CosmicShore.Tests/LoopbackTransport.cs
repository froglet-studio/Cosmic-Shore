using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>
    /// An in-memory <see cref="INetTransportFactory"/>: servers are found by port inside one factory
    /// and frames move between queues with no socket. It proves NetDriver depends on the
    /// <see cref="INetTransport"/> seam alone, and lets the driver tests watch every frame it sends.
    /// </summary>
    sealed class LoopbackTransportFactory : INetTransportFactory
    {
        readonly Dictionary<int, LoopbackTransport> _servers = new();
        int _nextPort = 40000;

        /// <summary>A Listen on this port throws, as a taken TCP port does.</summary>
        public int FailListenPort = -1;
        public readonly List<int> Listens = new();
        public readonly List<LoopbackTransport> Opened = new();

        public INetTransport Listen(string address, int port)
        {
            Listens.Add(port);
            if (port == FailListenPort || (port != 0 && _servers.TryGetValue(port, out var taken) && !taken.Disposed))
                throw new InvalidOperationException($"loopback port {port} is taken");
            if (port == 0) port = _nextPort++;
            var s = new LoopbackTransport(isServer: true, port, this);
            _servers[port] = s;
            Opened.Add(s);
            return s;
        }

        public INetTransport Connect(string address, int port, int timeoutMs)
        {
            var c = new LoopbackTransport(isServer: false, 0, this);
            Opened.Add(c);
            if (_servers.TryGetValue(port, out var server) && !server.Disposed) server.Accept(c);
            else c.Inbox.Enqueue(new NetEvent(NetEventKind.Disconnected, 0, null));
            return c;
        }

        internal void Release(LoopbackTransport server) => _servers.Remove(server.ListenPort);
    }

    sealed class LoopbackTransport : INetTransport
    {
        internal readonly ConcurrentQueue<NetEvent> Inbox = new();
        readonly Dictionary<int, LoopbackTransport> _peers = new(); // server: its clients by peer id
        readonly LoopbackTransportFactory _factory;
        LoopbackTransport _server;                                    // client: the server it reached
        int _idOnServer;
        int _nextPeer;

        public bool IsServer { get; }
        public int ListenPort { get; }
        public bool Disposed { get; private set; }
        public int PeerCount => IsServer ? _peers.Count : _server != null ? 1 : 0;

        internal LoopbackTransport(bool isServer, int port, LoopbackTransportFactory factory)
        {
            IsServer = isServer;
            ListenPort = port;
            _factory = factory;
        }

        internal void Accept(LoopbackTransport client)
        {
            int id = ++_nextPeer;
            _peers[id] = client;
            client._server = this;
            client._idOnServer = id;
            Inbox.Enqueue(new NetEvent(NetEventKind.Connected, id, null));
            client.Inbox.Enqueue(new NetEvent(NetEventKind.Connected, 0, null));
        }

        public void Send(int peer, byte[] payload, int length = -1)
        {
            if (Disposed) return;
            if (length < 0) length = payload.Length;
            var copy = new byte[length];
            Array.Copy(payload, copy, length);
            if (!IsServer) _server?.Inbox.Enqueue(new NetEvent(NetEventKind.Data, _idOnServer, copy));
            else if (_peers.TryGetValue(peer, out var c)) c.Inbox.Enqueue(new NetEvent(NetEventKind.Data, 0, copy));
        }

        public void Disconnect(int peer)
        {
            if (!IsServer) { _server?.Unlink(_idOnServer, notifyClient: true); return; }
            Unlink(peer, notifyClient: true);
        }

        /// <summary>Server side: drop one client; both ends see Disconnected, as with TCP.</summary>
        void Unlink(int peer, bool notifyClient)
        {
            if (!_peers.Remove(peer, out var c)) return;
            c._server = null;
            if (notifyClient) c.Inbox.Enqueue(new NetEvent(NetEventKind.Disconnected, 0, null));
            Inbox.Enqueue(new NetEvent(NetEventKind.Disconnected, peer, null));
        }

        public bool Poll(out NetEvent e) => Inbox.TryDequeue(out e);

        /// <summary>Closes without an event of its own; the other end sees Disconnected (TCP's EOF).</summary>
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            if (IsServer)
            {
                foreach (var c in _peers.Values)
                {
                    c._server = null;
                    c.Inbox.Enqueue(new NetEvent(NetEventKind.Disconnected, 0, null));
                }
                _peers.Clear();
                _factory.Release(this);
            }
            else if (_server != null && _server._peers.Remove(_idOnServer))
            {
                _server.Inbox.Enqueue(new NetEvent(NetEventKind.Disconnected, _idOnServer, null));
                _server = null;
            }
        }
    }
}
