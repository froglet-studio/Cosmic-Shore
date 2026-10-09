using System;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Opens Froglet's UDP transport over a relay when a session has an allocation waiting for it, and
    /// over a plain socket otherwise (docs/MULTIPLAYER.md §6.7). The session service allocates (an async
    /// web call) BEFORE it starts the NetworkManager, hands the allocation over here, and the driver's
    /// synchronous Listen / Connect pick it up. A client dials the address <see cref="RelayAddress"/>.
    /// </summary>
    public static class RelaySessions
    {
        /// <summary>The address a relay client "connects to": the allocation, not an IP, says where.</summary>
        public const string RelayAddress = "relay";

        /// <summary>The allocator sessions use, or null for direct connections (LAN, one machine).</summary>
        public static IRelayAllocator Allocator { get; set; }

        internal static RelayAllocation PendingHost, PendingJoin;

        /// <summary>The join code of the relay host this process is running now; null when it hosts directly or not at all.</summary>
        public static string HostingJoinCode { get; internal set; }

        /// <summary>The host allocation the next Listen binds with.</summary>
        public static void HostWith(RelayAllocation allocation) => PendingHost = allocation;

        /// <summary>The join allocation the next Connect to <see cref="RelayAddress"/> binds with.</summary>
        public static void JoinWith(RelayAllocation allocation) => PendingJoin = allocation;

        /// <summary>
        /// Puts the relay under the driver's current transport choice, once. Call after
        /// <see cref="NetTransports.Select"/> and before <see cref="NetSimulator.Install"/> (which then wraps it).
        /// </summary>
        public static void Install(IRelayAllocator allocator)
        {
            Allocator = allocator;
            if (NetDriver.TransportFactory is not RelayTransportFactory)
                NetDriver.TransportFactory = new RelayTransportFactory(NetDriver.TransportFactory);
        }

        public static void Reset()
        {
            Allocator = null;
            PendingHost = PendingJoin = null;
            HostingJoinCode = null;
        }
    }

    internal sealed class RelayTransportFactory : INetTransportFactory
    {
        readonly INetTransportFactory _direct;
        public RelayTransportFactory(INetTransportFactory direct) => _direct = direct;

        public INetTransport Listen(string address, int port)
        {
            var host = RelaySessions.PendingHost;
            RelaySessions.PendingHost = null;
            RelaySessions.HostingJoinCode = host?.JoinCode;
            if (host == null) return _direct.Listen(address, port);
            Console.WriteLine($"[relay] hosting through {host.ServerHost}:{host.ServerPort} (join code {host.JoinCode})");
            return UdpTransport.Over(new RelayLink(host, host: true), server: true);
        }

        public INetTransport Connect(string address, int port, int timeoutMs)
        {
            if (address != RelaySessions.RelayAddress) return _direct.Connect(address, port, timeoutMs);
            var join = RelaySessions.PendingJoin;
            RelaySessions.PendingJoin = null;
            if (join == null) throw new InvalidOperationException("connect to the relay with no join allocation; call RelaySessions.JoinWith first");
            Console.WriteLine($"[relay] joining {join.JoinCode} through {join.ServerHost}:{join.ServerPort}");
            return UdpTransport.Over(new RelayLink(join, host: false), server: false, timeoutMs);
        }
    }
}
