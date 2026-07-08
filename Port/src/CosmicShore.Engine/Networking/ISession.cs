namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Placeholder for the Unity Gaming Services Multiplayer session contract
    /// (<c>Unity.Services.Multiplayer.ISession</c>) until the services phase ports the
    /// real session layer. The surface mirrors the scalar subset of the UGS interface
    /// (id / join code / host flag / player counts) so the eventual implementation slots
    /// in without call-site churn; ported game code (e.g. <c>GameDataSO.ActiveSession</c>)
    /// currently only stores and hands around the reference.
    /// </summary>
    public interface ISession
    {
        /// <summary>Unique session id.</summary>
        string Id { get; }

        /// <summary>Human-shareable join code.</summary>
        string Code { get; }

        /// <summary>True when the local client is the session host.</summary>
        bool IsHost { get; }

        /// <summary>Maximum number of players the session admits.</summary>
        int MaxPlayers { get; }

        /// <summary>Number of players currently in the session.</summary>
        int PlayerCount { get; }

        /// <summary>
        /// Raised when the session is deleted (UGS surface subset — the controller-chain
        /// arc's MultiplayerMiniGameControllerBase subscribes to unhook its handlers).
        /// </summary>
        event System.Action Deleted;

        /// <summary>Raised with the leaving player's client id string (UGS surface subset).</summary>
        event System.Action<string> PlayerLeaving;

        /// <summary>
        /// The session roster (party-system arc: <c>PartyMemberService.SyncFromSession</c>
        /// reconciles the SOAP member list against it). The services phase maintains it live;
        /// harness/test implementations back it with a plain list.
        /// </summary>
        System.Collections.Generic.IReadOnlyList<IReadOnlyPlayer> Players { get; }
    }

    /// <summary>
    /// Placeholder for <c>Unity.Services.Multiplayer.IReadOnlyPlayer</c> (party-system arc:
    /// <c>PartyMemberService.ReadMemberData</c> reads a roster member's id + properties).
    /// The services phase supplies the live implementation.
    /// </summary>
    public interface IReadOnlyPlayer
    {
        /// <summary>UGS player id.</summary>
        string Id { get; }

        /// <summary>Per-player session properties (displayName, avatarId, invite_payloads, …).</summary>
        System.Collections.Generic.IReadOnlyDictionary<string, PlayerProperty> Properties { get; }
    }

    /// <summary>
    /// Placeholder for <c>Unity.Services.Multiplayer.PlayerProperty</c> — a string value with a
    /// lobby visibility level (party-system arc: per-player invite slots + identity properties).
    /// </summary>
    public class PlayerProperty
    {
        public string Value { get; }
        public VisibilityPropertyOptions Visibility { get; }

        public PlayerProperty(string value, VisibilityPropertyOptions visibility = VisibilityPropertyOptions.Public)
        {
            Value = value;
            Visibility = visibility;
        }
    }

    /// <summary>Placeholder for <c>Unity.Services.Multiplayer.VisibilityPropertyOptions</c>.</summary>
    public enum VisibilityPropertyOptions
    {
        Public = 0,
        Member = 1,
        Private = 2,
    }
}
