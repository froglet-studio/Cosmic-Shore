using System;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Online
{
    /// <summary>
    /// Settings, from the environment (the player's --relay* flags set these; docs/RELAY.md):
    ///   COSMIC_SHORE_UGS_PROJECT       the UGS project id (default: the game's, from ProjectSettings)
    ///   COSMIC_SHORE_UGS_ENVIRONMENT   the UGS environment (default "development")
    ///   COSMIC_SHORE_RELAY_REGION      a Relay region id such as asia-south1 (default: the closest, measured)
    ///   COSMIC_SHORE_RELAY_DTLS        "0" for plain UDP (default: DTLS, encrypted)
    /// </summary>
    public sealed class RelayOptions
    {
        /// <summary>The game's cloudProjectId (ProjectSettings/ProjectSettings.asset).</summary>
        public const string DefaultProjectId = "3030fd69-28ab-433f-b4bd-22b9b93c5118";
        /// <summary>The game's UGS environment (ProjectSettings/Packages/com.unity.services.core/Settings.json).</summary>
        public const string DefaultEnvironment = "development";

        public string ProjectId { get; set; } = DefaultProjectId;
        public string Environment { get; set; } = DefaultEnvironment;
        public string Region { get; set; }
        public bool Dtls { get; set; } = true;

        public static RelayOptions FromEnvironment()
        {
            static string Env(string n) => System.Environment.GetEnvironmentVariable(n) is { Length: > 0 } v ? v.Trim() : null;
            return new RelayOptions
            {
                ProjectId = Env("COSMIC_SHORE_UGS_PROJECT") ?? DefaultProjectId,
                Environment = Env("COSMIC_SHORE_UGS_ENVIRONMENT") ?? DefaultEnvironment,
                Region = Env("COSMIC_SHORE_RELAY_REGION"),
                Dtls = Env("COSMIC_SHORE_RELAY_DTLS") is not ("0" or "false" or "off" or "udp"),
            };
        }
    }

    /// <summary>
    /// The "relay" transport. A listen or connect that the relay backend armed (a slot reserved by
    /// <see cref="UnityRelayBackend"/>) runs <see cref="UdpTransport"/> over a <see cref="RelayLink"/>;
    /// anything else - the offline-mode host, a session hosted on the LAN because Relay was unreachable -
    /// runs over a plain UDP socket exactly as the "udp" transport does.
    /// </summary>
    internal sealed class RelayTransportFactory : INetTransportFactory
    {
        readonly UdpTransportFactory _direct = new();
        RelayAllocation _host, _join;
        public bool Dtls { get; set; } = true;
        /// <summary>Tests: how a link reaches its Relay server (default: the allocation's endpoint).</summary>
        internal Func<RelayAllocation, bool, Func<IRelayChannel>> Opener = RelayLink.Opener;
        /// <summary>Tests: a shorter setup timeout for new links.</summary>
        internal int SetupTimeoutMs = RelayLink.SetupTimeoutMs;
        /// <summary>The link of the transport opened last (for status and tests).</summary>
        public RelayLink LastLink { get; private set; }

        public void ArmHost(RelayAllocation a) { Interlocked.Exchange(ref _host, a); }
        public void ArmJoin(RelayAllocation a) { Interlocked.Exchange(ref _join, a); }

        public INetTransport Listen(string address, int port)
        {
            var a = Interlocked.Exchange(ref _host, null);
            if (a == null) { NetRelay.InUse = false; return _direct.Listen(address, port); }
            NetRelay.InUse = true;
            LastLink = new RelayLink(a, isHost: true, Opener(a, Dtls)) { SetupTimeout = SetupTimeoutMs };
            return UdpTransport.ListenOver(LastLink, "relay-host");
        }

        public INetTransport Connect(string address, int port, int timeoutMs)
        {
            var a = Interlocked.Exchange(ref _join, null);
            if (a == null) { NetRelay.InUse = false; return _direct.Connect(address, port, timeoutMs); }
            NetRelay.InUse = true;
            NetRelay.JoinCode = null;
            LastLink = new RelayLink(a, isHost: false, Opener(a, Dtls)) { SetupTimeout = SetupTimeoutMs };
            // The DTLS handshake, BIND and CONNECT come first; give them room on top of the caller's budget.
            return UdpTransport.ConnectOver(LastLink, RelayLink.Host, timeoutMs + SetupTimeoutMs, "relay-client");
        }
    }

    /// <summary>
    /// <see cref="INetRelayBackend"/> on Unity Relay: signs in (reusing this instance's cached UGS player),
    /// picks a region, allocates, makes the join code and arms <see cref="RelayTransportFactory"/>.
    /// </summary>
    public sealed class UnityRelayBackend : INetRelayBackend
    {
        readonly IUgsApi _api;
        readonly UgsSession _session;
        readonly RelayOptions _options;
        internal readonly RelayTransportFactory Factory;
        string _autoRegion;
        string _status = "relay: idle (not signed in yet)";

        internal UnityRelayBackend(IUgsApi api, UgsSession session, RelayOptions options, RelayTransportFactory factory)
        {
            _api = api;
            _session = session;
            _options = options;
            Factory = factory;
            factory.Dtls = options.Dtls;
        }

        public UgsSession Session => _session;
        /// <summary>The PING round trip to the Relay server of the transport opened last, ms (-1 before one).</summary>
        public double RelayServerPingMs => Factory.LastLink?.ServerRttMs ?? -1;
        public RelayOptions Options => _options;
        public string Status => NetRelay.InUse && Factory.LastLink is { } l && l.ServerRttMs >= 0 ? $"{_status} · relay ping {l.ServerRttMs:0} ms" : _status;

        public async Task<string> PrepareHostAsync(int maxConnections, string region = null)
        {
            var player = await _session.GetPlayerAsync().ConfigureAwait(false);
            region ??= await RegionAsync(player).ConfigureAwait(false);
            var a = await _api.AllocateAsync(player, Math.Clamp(maxConnections, 1, 100), region).ConfigureAwait(false);
            var code = await _api.CreateJoinCodeAsync(player, a.AllocationId).ConfigureAwait(false);
            Factory.ArmHost(a);
            NetRelay.JoinCode = code;
            NetRelay.Region = a.Region;
            _status = $"relay: hosting, JOIN CODE {code} · region {a.Region} · {(_options.Dtls ? "dtls" : "udp")} · player {player.PlayerId} ({_session.LastSignIn})";
            Console.WriteLine($"[relay] JOIN CODE: {code}   (region {a.Region}, {(_options.Dtls ? "DTLS encrypted" : "plain UDP")}, up to {maxConnections} joiners)");
            return code;
        }

        public async Task PrepareJoinAsync(string joinCode)
        {
            var player = await _session.GetPlayerAsync().ConfigureAwait(false);
            var a = await JoinWithRetryAsync(player, joinCode.Trim().ToUpperInvariant()).ConfigureAwait(false);
            Factory.ArmJoin(a);
            NetRelay.Region = a.Region;
            _status = $"relay: joining {joinCode.Trim().ToUpperInvariant()} · region {a.Region} · {(_options.Dtls ? "dtls" : "udp")} · player {player.PlayerId} ({_session.LastSignIn})";
            Console.WriteLine($"[relay] joining {joinCode.Trim().ToUpperInvariant()} (region {a.Region}, {(_options.Dtls ? "DTLS encrypted" : "plain UDP")})");
        }

        /// <summary>
        /// A brand-new join code can answer 404 "join code not found" (15009) for a few seconds while
        /// Relay propagates it, so a joiner that starts the instant the host prints the code retries
        /// briefly. A code that is truly wrong still fails, after about 6 seconds.
        /// </summary>
        async Task<RelayAllocation> JoinWithRetryAsync(UgsPlayer player, string code)
        {
            int[] waits = { 500, 1000, 1500, 3000 };
            for (int attempt = 0; ; attempt++)
            {
                try { return await _api.JoinAsync(player, code).ConfigureAwait(false); }
                catch (UgsException e) when (e.Status == 404 && attempt < JoinRetries && attempt < waits.Length)
                {
                    Console.WriteLine($"[relay] join code {code} not found yet; retrying in {waits[attempt]} ms");
                    await Task.Delay(waits[attempt]).ConfigureAwait(false);
                }
            }
        }

        /// <summary>How many times a 404 from Relay's join call is retried (tests set 0).</summary>
        internal int JoinRetries { get; set; } = 4;

        async Task<string> RegionAsync(UgsPlayer player)
        {
            if (!string.IsNullOrEmpty(_options.Region)) return _options.Region;
            if (_autoRegion != null) return _autoRegion.Length == 0 ? null : _autoRegion;
            var (region, measured) = await RelayRegions.PickAsync(_api, player).ConfigureAwait(false);
            _autoRegion = region ?? "";
            Console.WriteLine(region != null
                ? $"[relay] closest region: {region} ({string.Join(", ", System.Linq.Enumerable.Take(measured, 4))})"
                : "[relay] no region answered a ping; Relay picks the region");
            return region;
        }
    }
}
