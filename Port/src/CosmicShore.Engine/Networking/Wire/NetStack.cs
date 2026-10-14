using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Networking
{
    /// <summary>A transport factory that adds a layer over another one (the simulator, a relay, the online server).</summary>
    internal interface IWrappingTransportFactory
    {
        INetTransportFactory Inner { get; }
    }

    /// <summary>
    /// The one place a player puts its networking together, in the only order that works (docs/MULTIPLAYER.md §6.9):
    /// <list type="number">
    /// <item>the transport (<see cref="NetTransports.Select"/>: UDP unless COSMIC_SHORE_NET_TRANSPORT=tcp);</item>
    /// <item>the Unity-Relay-protocol relay over it, when COSMIC_SHORE_RELAY names one;</item>
    /// <item>the session service: the online server (which adds its own relay over the transport) or the shared folder;</item>
    /// <item>the network simulator around the whole transport chain, then session-service faults around the service.</item>
    /// </list>
    /// A merge once dropped steps 1, 2 and 4 from the player while every engine test stayed green, so the
    /// player calls this and <c>NetStackTests</c> holds both the order and the call.
    /// </summary>
    public static class NetStack
    {
        public static void Install(string transport, Action installRelay, Action installSessions)
        {
            if (installSessions == null) throw new ArgumentNullException(nameof(installSessions));
            Console.WriteLine($"[net] transport: {NetTransports.Select(transport)}");
            installRelay?.Invoke();
            installSessions();
            NetSimulator.Install();
            NetFaults.Install();
            Console.WriteLine($"[net] stack: {Describe()}; sessions: {DescribeSessions()}");
        }

        /// <summary>The driver's transport chain, outermost first: "simulated > online > relay > udp".</summary>
        public static string Describe() => Describe(NetDriver.TransportFactory);

        internal static string Describe(INetTransportFactory factory)
        {
            var names = new List<string>();
            for (var f = factory; f != null; f = (f as IWrappingTransportFactory)?.Inner)
            {
                names.Add(f switch
                {
                    SimulatedTransportFactory => "simulated",
                    OnlineTransportFactory => "online",
                    RelayTransportFactory => "relay",
                    UdpTransportFactory => "udp",
                    TcpTransportFactory => "tcp",
                    _ => f.GetType().Name,
                });
                if (f is not IWrappingTransportFactory) break;
            }
            return string.Join(" > ", names);
        }

        /// <summary>The session service chain, outermost first: "faults > online".</summary>
        public static string DescribeSessions()
        {
            var names = new List<string>();
            IMultiplayerService s = MultiplayerService.Instance;
            while (s != null)
            {
                switch (s)
                {
                    case FaultInjectingMultiplayerService f: names.Add("faults"); s = f.Inner; continue;
                    case OnlineMultiplayerService: names.Add("online"); break;
                    case DirectoryMultiplayerService: names.Add("folder"); break;
                    default: names.Add(s.GetType().Name); break;
                }
                break;
            }
            return string.Join(" > ", names);
        }
    }
}
