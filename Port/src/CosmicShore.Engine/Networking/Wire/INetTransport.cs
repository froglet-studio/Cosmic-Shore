using System;

namespace CosmicShore.Engine.Networking
{
    internal enum NetEventKind { Connected, Data, Disconnected }

    /// <summary>One thing a transport received: a peer connected, a frame arrived, or a peer left.</summary>
    internal readonly struct NetEvent
    {
        public readonly NetEventKind Kind;
        public readonly int Peer;
        public readonly byte[] Payload;
        public NetEvent(NetEventKind k, int peer, byte[] payload) { Kind = k; Peer = peer; Payload = payload; }
    }

    /// <summary>
    /// The seam between <see cref="NetDriver"/> (replication) and whatever carries its bytes.
    /// The contract every implementation keeps:
    ///  • frames are reliable, ordered and arrive whole (one Send = one Data event);
    ///  • events reach the driver only through <see cref="Poll"/>, which the main thread drains once per frame;
    ///  • a client addresses the server as peer 0; a server addresses each client by the id its Connected event carried;
    ///  • a client whose connect fails reports a Disconnected event (it never throws from Connect);
    ///  • a listen that cannot bind throws, so the driver can retry on another port.
    /// TCP (<see cref="NetSocket"/>) is the first implementation; an internet relay is the next (C6, gate G2).
    /// </summary>
    internal interface INetTransport : IDisposable
    {
        bool IsServer { get; }
        /// <summary>The port a server listens on (0 when the transport has no port, or on a client).</summary>
        int ListenPort { get; }
        int PeerCount { get; }
        /// <summary>Queue a frame to one peer (0 = the server, from a client).</summary>
        void Send(int peer, byte[] payload, int length = -1);
        void Disconnect(int peer);
        bool Poll(out NetEvent e);
    }

    /// <summary>Opens a transport as a server or a client. <see cref="NetDriver.TransportFactory"/> picks the implementation.</summary>
    internal interface INetTransportFactory
    {
        INetTransport Listen(string address, int port);
        INetTransport Connect(string address, int port, int timeoutMs);
    }

    /// <summary>The default factory: TCP.</summary>
    internal sealed class TcpTransportFactory : INetTransportFactory
    {
        public INetTransport Listen(string address, int port) => NetSocket.Listen(address, port);
        public INetTransport Connect(string address, int port, int timeoutMs) => NetSocket.Connect(address, port, timeoutMs);
    }
}
