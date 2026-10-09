using System;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Online
{
    /// <summary>
    /// Plugs Unity Relay into the engine: registers the "relay" transport with <see cref="NetTransports"/>,
    /// whose selection installs <see cref="UnityRelayBackend"/> on <see cref="NetRelay.Backend"/>. Until
    /// "relay" is selected nothing here touches the network, and the local stand-ins stay in charge.
    /// </summary>
    public static class OnlineBoot
    {
        /// <summary>The backend once "relay" was selected (null before).</summary>
        public static UnityRelayBackend Backend { get; private set; }

        /// <param name="dataDir">This instance's data folder: where its UGS session is cached.</param>
        public static void Register(string dataDir, RelayOptions options = null, IUgsApi api = null)
        {
            NetTransports.Register("relay", () =>
            {
                var o = options ?? RelayOptions.FromEnvironment();
                var ugs = api ?? new UgsClient(o.ProjectId, o.Environment);
                var factory = new RelayTransportFactory();
                Backend = new UnityRelayBackend(ugs, new UgsSession(ugs, o.ProjectId, o.Environment, dataDir), o, factory);
                NetRelay.Backend = Backend;
                Console.WriteLine($"[relay] Unity Relay ready: project {o.ProjectId}, environment {o.Environment}, " +
                                  $"{(o.Dtls ? "DTLS" : "plain UDP")}, region {(string.IsNullOrEmpty(o.Region) ? "auto" : o.Region)}");
                return factory;
            });
        }
    }
}
