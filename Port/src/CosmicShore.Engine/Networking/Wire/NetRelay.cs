using System;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// What an internet relay service offers the session layer (docs/RELAY.md). The engine only knows
    /// this shape; CosmicShore.Online implements it with Unity Relay + UGS sign-in and registers it on
    /// <see cref="NetRelay.Backend"/>. With no backend (the default) every session stays on the local
    /// stand-ins: the shared session folder and a direct UDP address.
    /// </summary>
    public interface INetRelayBackend
    {
        /// <summary>
        /// Host side: sign in, reserve a relay slot for up to <paramref name="maxConnections"/> joiners and
        /// make its join code, then arm the transport so the next listen goes through that slot.
        /// <paramref name="region"/> null = pick the closest region automatically.
        /// </summary>
        Task<string> PrepareHostAsync(int maxConnections, string region = null);

        /// <summary>Join side: resolve <paramref name="joinCode"/> and arm the transport so the next connect goes through it.</summary>
        Task PrepareJoinAsync(string joinCode);

        /// <summary>One line for logs, the window title and <c>do relay</c>: signed in?, region, join code.</summary>
        string Status { get; }
    }

    /// <summary>The process-wide relay hook (one NetDriver per process, so one relay too).</summary>
    public static class NetRelay
    {
        /// <summary>Set by CosmicShore.Online when the "relay" transport is chosen; null keeps the offline stand-ins.</summary>
        public static INetRelayBackend Backend { get; set; }

        /// <summary>The join code of the slot this process hosts, while it hosts one (null otherwise).</summary>
        public static string JoinCode { get; set; }

        /// <summary>True while the running transport goes through the relay (set by the relay's transport factory).</summary>
        public static bool InUse { get; set; }

        /// <summary>The relay region in use (empty until a slot exists).</summary>
        public static string Region { get; set; } = "";

        /// <summary>A short tag for the window title: " · Relay ABC123 (asia-south1)" while hosting, "" otherwise.</summary>
        public static string TitleTag
        {
            get
            {
                if (!InUse || !NetDriver.IsActive) return "";
                if (NetDriver.IsServer && !string.IsNullOrEmpty(JoinCode))
                    return $" · Relay {JoinCode}{(string.IsNullOrEmpty(Region) ? "" : $" ({Region})")}";
                return NetDriver.IsClientOnly ? " · via Relay" : "";
            }
        }
    }
}
