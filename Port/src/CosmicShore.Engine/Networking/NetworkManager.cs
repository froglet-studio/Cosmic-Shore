namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Minimal stand-in for Unity Netcode's <c>NetworkManager</c> singleton (engine
    /// addition for V10: GameDataSO consults it). Ported code only checks
    /// <see cref="Singleton"/> for null/fake-null ("no networking active") plus the
    /// role flags; the session/transport phase replaces this with a real network
    /// driver. A null <see cref="Singleton"/> models the offline / single-process
    /// case, which is the default until something assigns one. Defaults model
    /// single-process host-mode, matching <see cref="NetworkBehaviour.Spawn"/>.
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Singleton { get; set; }

        public bool IsServer { get; set; } = true;
        public bool IsClient { get; set; } = true;
        public bool IsHost => IsServer && IsClient;
        public bool IsListening { get; set; } = true;

        /// <summary>
        /// Server-synchronized clock (engine addition for SA1: TimePlayedScoring reads
        /// <c>ServerTime.Time</c> when a NetworkManager is listening). Assigned by the
        /// harness / future network driver; defaults to an unstarted clock (0).
        /// </summary>
        public NetworkTime ServerTime { get; set; }

        /// <summary>
        /// Connected client ids (engine addition for the controller-chain arc:
        /// MultiplayerDomainGamesController counts humans by <c>ConnectedClientsIds.Count</c>
        /// and solo-session gates check <c>Count &lt;= 1</c>). Defaults to the single host
        /// client (id 0) — the single-process host-mode the rest of this stand-in models.
        /// The session/transport phase maintains it from real connections.
        /// </summary>
        public System.Collections.Generic.List<ulong> ConnectedClientsIds { get; } = new() { 0 };
    }
}
