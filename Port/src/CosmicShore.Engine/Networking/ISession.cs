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

        /// <summary>
        /// The local player's writable roster entry (party-system arc:
        /// <c>AcceptanceSignalService</c> publishes accepted_invite / invite_payloads through
        /// <c>CurrentPlayer.SetProperty</c>). The services phase supplies the live implementation.
        /// </summary>
        IPlayer CurrentPlayer { get; }

        /// <summary>Pull the latest lobby state (UGS surface subset — <c>LobbyPropertyWriter</c>
        /// refreshes before conflict-sensitive writes). Placeholder implementations complete
        /// synchronously.</summary>
        System.Threading.Tasks.Task RefreshAsync();

        /// <summary>Push the local player's pending property writes (UGS surface subset —
        /// <c>LobbyPropertyWriter.SaveWithRetryAsync</c>).</summary>
        System.Threading.Tasks.Task SaveCurrentPlayerDataAsync();

        /// <summary>Leave the session as a non-host member (UGS surface subset —
        /// <c>PartySessionService.LeaveAsync</c>). Placeholder implementations complete
        /// synchronously.</summary>
        System.Threading.Tasks.Task LeaveAsync();

        /// <summary>Host-privileged view of this session (UGS surface subset — the host
        /// deletes rather than leaves).</summary>
        IHostSession AsHost();
    }

    /// <summary>Placeholder for <c>Unity.Services.Multiplayer.IHostSession</c> (subset).</summary>
    public interface IHostSession : ISession
    {
        System.Threading.Tasks.Task DeleteAsync();
    }

    /// <summary>
    /// Placeholder for <c>Unity.Services.Multiplayer.SessionError</c> — the subset of codes
    /// ported catch-classification reads (values placeholder-local until the services phase
    /// wires the real SDK; never serialized).
    /// </summary>
    public enum SessionError
    {
        Unknown = 0,
        NotInLobby = 1,
        SessionDeleted = 2,
        SessionNotFound = 3,
        RateLimitExceeded = 4,
    }

    /// <summary>
    /// Placeholder for <c>Unity.Services.Multiplayer.SessionException</c> — carries the
    /// structured <see cref="Error"/> code that ported error classification matches on
    /// (structured matching, never message text — Docs/PartySystem BUGS precedent).
    /// </summary>
    public class SessionException : System.Exception
    {
        public SessionError Error { get; }

        public SessionException(SessionError error, string message = null)
            : base(message ?? error.ToString())
        {
            Error = error;
        }
    }

    /// <summary>
    /// Placeholder for <c>Unity.Services.Multiplayer.IPlayer</c> — the writable counterpart of
    /// <see cref="IReadOnlyPlayer"/> (party-system arc: per-player property publication).
    /// </summary>
    public interface IPlayer : IReadOnlyPlayer
    {
        void SetProperty(string key, PlayerProperty property);
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
