using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Reflex.Attributes;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Single-responsibility service that establishes and maintains the host
    /// connection (presence lobby) at the main-menu stage.
    ///
    /// Writes all state into <see cref="HostConnectionDataSO"/> so every UI
    /// consumer can react via SOAP events / lists without coupling to this class.
    /// </summary>
    public class HostConnectionService : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Auth (Source of Truth)")]
        [SerializeField] private AuthenticationDataVariable authenticationDataVariable;
        private AuthenticationData AuthData => authenticationDataVariable.Value;

        [Header("SOAP Data Container")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("Presence Lobby")]
        [Tooltip("Max simultaneous players in the global presence lobby.")]
        [SerializeField] private int presenceLobbyMaxPlayers = 100;

        [Tooltip("How often (seconds) to refresh the online player list and check for invites. " +
                 "UGS lobby read rate limit is ~1/s per client, so 1.5s keeps us safely under " +
                 "while staying responsive enough that invite arrival and member joins feel instant.")]
        [SerializeField] private float refreshIntervalSeconds = 1.5f;

        [Inject] private PlayerDataService playerDataService;
        [Inject] private GameDataSO _gameData;

        // ─────────────────────────────────────────────────────────────────────
        // Static access
        // ─────────────────────────────────────────────────────────────────────

        public static HostConnectionService Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        // Internal state
        // ─────────────────────────────────────────────────────────────────────

        private ISession _presenceLobby;
        private ISession _partySession;
        private float _refreshTimer;
        private bool _initialized;
        private bool _joining;
        private bool _leaving;
        private PartyInviteData? _lastFiredInvite;
        /// <summary>
        /// Set when the user has answered <see cref="_lastFiredInvite"/> (accept or decline).
        /// Kept alongside _lastFiredInvite so the SDK-side dedup guard (which compares
        /// PartySessionId) still suppresses repeated SOAP raises during the window where
        /// the sender's lobby properties are still stale, while UI queries via
        /// <see cref="LastPendingInvite"/> correctly report "no pending invite".
        /// </summary>
        private bool _lastInviteResolved;

        /// <summary>
        /// Outgoing-invite tracking, keyed by the invitee's player id. Each entry
        /// caches the pre-serialized payload line so we don't have to rebuild it
        /// every time a slot is added or removed (re-serialization happens after
        /// every dict mutation to publish the composite invite_payloads property).
        /// </summary>
        private struct OutgoingInvite
        {
            public string Payload;     // "targetId|hostId|sessionId|hostName|avatarId"
            public float ExpiresAt;    // Time.unscaledTime
        }
        private readonly Dictionary<string, OutgoingInvite> _outgoingInvites = new();

        /// <summary>
        /// Raised when an outgoing invite to <c>playerId</c> is cleared for any
        /// reason (acceptance detected, target left presence, timeout, manual cancel).
        /// UI subscribes to revert PENDING REQUEST tints back to ONLINE.
        /// </summary>
        public event Action<string> OutgoingInviteCleared;

        /// <summary>
        /// Snapshot of player ids with an outstanding outgoing invite. Used by the
        /// UI on first render so rows that were already pending before the panel
        /// opened are restored to the pulsing-tint state.
        /// </summary>
        public IReadOnlyCollection<string> OutgoingInviteTargets => _outgoingInvites.Keys;

        private ILogHandler _originalLogHandler;
        private Task _creatingPartySessionTask;
        /// <summary>
        /// Mutex flag preventing concurrent lobby operations.
        /// RefreshAsync skips if busy; SendInviteAsync waits then claims.
        /// Prevents the SDK's internal player index from going stale when
        /// a refresh and a save race each other.
        /// </summary>
        private bool _lobbyBusy;

        private const string PRESENCE_LOBBY_GAME_MODE = "PRESENCE_LOBBY";
        private const string DISPLAY_NAME_KEY = "displayName";
        private const string AVATAR_ID_KEY = "avatarId";
        private const string PARTY_COUNT_KEY = "partyCount";
        private const string PARTY_MAX_KEY = "partyMax";
        private const string MATCH_NAME_KEY = "matchName";

        // Composite invite property. Stores zero-or-more outstanding invites in a
        // single player property to stay under UGS lobbies' 10-property-per-player
        // cap (six base presence properties + per-invite slots had blown past the
        // limit). Each line is one invite formatted as
        //     targetPlayerId|hostPlayerId|sessionId|hostDisplayName|avatarId
        // Lines are joined with '\n' (illegal in UGS auth-supplied display names
        // and player ids, so it cannot collide with payload content).
        private const string INVITE_PAYLOADS_KEY = "invite_payloads";
        private const char INVITE_LINE_SEPARATOR = '\n';
        private const char INVITE_FIELD_SEPARATOR = '|';

        /// <summary>
        /// Auto-clear an outgoing invite after this many seconds with no acceptance.
        /// Covers the recipient-declined and recipient-ignored cases (UGS provides no
        /// SDK signal for either) so the sender's UI reverts to "online" instead of
        /// staying stuck on PENDING REQUEST forever.
        /// </summary>
        private const float OUTGOING_INVITE_TIMEOUT_SECONDS = 30f;

        /// <summary>
        /// Presence-lobby player property set by a client after
        /// <see cref="AcceptInviteAsync"/> successfully joins the party session.
        /// Value is the party session id.
        ///
        /// Exists as a fallback channel for the sender's
        /// <see cref="RefreshPartyMembersAsync"/> path: UGS party-session
        /// <c>RefreshAsync()</c> lags behind the actual join by a tick or
        /// two, so the sender's PartyMembers list (and their
        /// <c>partyCount</c> presence) would otherwise stay stale until the
        /// next refresh. The sender's <see cref="RefreshAsync"/> also scans
        /// presence-lobby players for this key and upserts matches into
        /// PartyMembers immediately, which is what the UI (ArcadeLobbyList
        /// etc.) keys off.
        ///
        /// Cleared when the client leaves the party or when the session is
        /// cleared so stale joins don't bleed into future parties.
        /// </summary>
        private const string JOINED_PARTY_KEY = "joined_party";

        /// <summary>
        /// Sentinel session id embedded in invite payloads before the Relay
        /// party session has been created. The host creates the session only
        /// when the first recipient signals acceptance (detected via
        /// <see cref="ACCEPTED_INVITE_KEY"/> in <see cref="RefreshAsync"/>),
        /// then republishes all pending invites with the real session id.
        /// Recipients poll for the replacement before calling JoinSessionByIdAsync.
        /// </summary>
        private const string PENDING_SESSION_ID = "PENDING";

        /// <summary>
        /// Presence-lobby player property written by a client to signal that
        /// they accepted an invite from a specific host. Value = the host's
        /// player id. The host's <see cref="RefreshAsync"/> scans for this and
        /// lazily creates the Relay party session when first detected, then
        /// republishes all pending invite payloads with the real session id.
        /// Cleared when the client leaves the party.
        /// </summary>
        private const string ACCEPTED_INVITE_KEY = "accepted_invite";

        /// <summary>
        /// Milliseconds to wait after creating a lobby before re-querying,
        /// giving a near-simultaneous second instance time to also create.
        /// If a rival lobby is detected, we merge into it.
        /// </summary>
        private const int LOBBY_RACE_SETTLE_MS = 1500;

        /// <summary>
        /// After this many consecutive RefreshAsync failures, abandon the
        /// stale lobby reference and attempt to rejoin.
        /// </summary>
        private const int MAX_REFRESH_ERRORS_BEFORE_RECONNECT = 3;
        private int _consecutiveRefreshErrors;

        private const int RATE_LIMIT_MAX_RETRIES = 3;
        private const int RATE_LIMIT_BASE_DELAY_MS = 2000;
        private float _rateLimitBackoffUntil;

        /// <summary>
        /// While <see cref="Time.unscaledTime"/> &lt; this value, the refresh
        /// loop ticks at <see cref="BOOSTED_REFRESH_INTERVAL_SECONDS"/> instead
        /// of <see cref="refreshIntervalSeconds"/>. Used after
        /// <see cref="SendInviteAsync"/> so the sender's PartyMembers list
        /// picks up the joining player within ~1s rather than 3s.
        /// </summary>
        private float _boostedRefreshUntil;

        private const float BOOSTED_REFRESH_INTERVAL_SECONDS = 0.75f;
        private const float BOOSTED_REFRESH_WINDOW_SECONDS = 15f;

        /// <summary>
        /// Minimum gap between consecutive <see cref="ForceRefreshNow"/> requests.
        /// Prevents bursty UI open / invite / accept cascades from slamming the
        /// lobby service past its per-client rate limit.
        /// </summary>
        private const float FORCE_REFRESH_COOLDOWN_SECONDS = 0.5f;
        private float _nextForcedRefreshAllowed;

        /// <summary>
        /// Max time to wait for <see cref="PlayerDataService.IsInitialized"/>
        /// before joining the presence lobby. CloudSave usually resolves in
        /// a few hundred ms, so 5s covers cold starts; if we time out we
        /// proceed with the local default name and rely on the profile-change
        /// republish to correct it.
        /// </summary>
        private const int PROFILE_INIT_TIMEOUT_MS = 5000;

        /// <summary>Last published party-size value so we only push on change.</summary>
        private int _publishedPartyCount = -1;

        /// <summary>
        /// Last published <c>matchName</c> presence value. Sentinel "&lt;UNSET&gt;"
        /// (rather than empty string) ensures the first publish always fires
        /// even when the local player is not in a match.
        /// </summary>
        private string _publishedMatchName = "<UNSET>";

        /// <summary>Tracks whether <see cref="GameDataSO.OnLaunchGame"/> is wired so we don't double-subscribe.</summary>
        private bool _gameLaunchSubscribed;

        private static bool IsRateLimitException(Exception e)
        {
            return e.Message != null && e.Message.Contains("Too Many Requests");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InstallLobbyLogFilter();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        async void Start()
        {
            while (!IsAuthSignedInAndHasId())
                await Task.Delay(300);

            // Subscribe to profile changes so a name/avatar update mid-session
            // is republished to the presence lobby. Without this, players who
            // join before the cloud profile loads would advertise the default
            // "Pilot" name forever.
            SubscribeToProfileChanges();

            // Subscribe to OnLaunchGame so we can push MATCH_NAME_KEY presence
            // before the scene transition suspends the refresh loop. Without
            // this, remote players would never see "IN A MATCH — {gameMode}"
            // for a player who just left for a game scene.
            SubscribeToGameLaunch();

            // HandleSignedInEvent may have already completed via SOAP event,
            // or may currently be running (_joining). Both paths call
            // CreatePartySessionAsync — concurrent calls cause the second
            // to find a running host, shut it down, and reload Menu_Main.
            if (_initialized || _joining) return;

            // Wait for PlayerDataService to merge the cloud profile before
            // joining the presence lobby — otherwise we advertise the local
            // "Pilot{XXXX}" default name until the next profile-change event
            // forces a republish (and remote players see a stale name in the
            // meantime). Bounded so a slow CloudSave never blocks lobby join.
            await WaitForProfileInitAsync(PROFILE_INIT_TIMEOUT_MS);

            SyncLocalIdentity();
            await JoinPresenceLobbyAsync();

            // Defensive republish: if the profile resolved *during*
            // JoinPresenceLobbyAsync, HandleProfileChanged's republish was
            // a no-op (lobby was still null at that moment). Do one more
            // sync+republish now that the lobby exists.
            SyncLocalIdentity();
            RepublishLocalIdentityAsync().Forget();

            // NOTE: party session is created lazily on first SendInviteAsync
            // (which triggers nm.Shutdown + Relay-backed StartHost — destroys
            // and respawns every vessel). Eagerly creating it here would do
            // that on every app launch, and recreating it on every refresh
            // failure would do it periodically forever. Lazy is the only sane
            // policy: solo menu = local host = stable vessels.

            _initialized = true;
            DebugExtensions.LogColored(
                $"[HostConnectionService] Initialized (Start) — lobby: {_presenceLobby?.Id ?? "NULL"}, " +
                $"partySession: {_partySession?.Id ?? "NULL"}, " +
                $"localId: {connectionData.LocalPlayerId}",
                Color.green);

            // Immediate first refresh so OnlinePlayers is populated
            // before the user opens the panel (don't wait 3 seconds).
            RefreshAsync().Forget();
        }

        void Update()
        {
            if (!_initialized || _presenceLobby == null || _lobbyBusy) return;
            if (Time.unscaledTime < _rateLimitBackoffUntil) return;

            // Only run lobby refresh while on Menu_Main. During game scenes the
            // party session is irrelevant.
            if (!IsOnMenuScene()) return;

            // NOTE: party session is NOT auto-recreated here. Each recreation
            // calls nm.Shutdown + Relay-backed StartHost, which destroys and
            // respawns every vessel — flashing stale UI on each refresh-failure
            // cycle. The session is created lazily on first SendInviteAsync;
            // if it dies, it stays dead until the next outgoing invite. Keeps
            // the menu's local-host vessel stable indefinitely.

            // Drop expired outgoing invites before scheduling the next refresh so
            // the slot is reusable as soon as the timeout window closes. We don't
            // need to await — the actual property save kicks off async and the
            // UI revert event already fired.
            ExpireOutgoingInvites();

            _refreshTimer += Time.unscaledDeltaTime;
            float interval = Time.unscaledTime < _boostedRefreshUntil
                ? BOOSTED_REFRESH_INTERVAL_SECONDS
                : refreshIntervalSeconds;
            if (_refreshTimer >= interval)
            {
                _refreshTimer = 0f;
                RefreshAsync().Forget();
            }
        }

        /// <summary>
        /// Walks the outgoing-invite map and clears any entry past its deadline.
        /// Cheap to run every frame — typical map size is 0-3 entries.
        /// </summary>
        private void ExpireOutgoingInvites()
        {
            if (_outgoingInvites.Count == 0) return;

            float now = Time.unscaledTime;
            List<string> expired = null;
            foreach (var kv in _outgoingInvites)
            {
                if (now >= kv.Value.ExpiresAt)
                {
                    expired ??= new List<string>();
                    expired.Add(kv.Key);
                }
            }
            if (expired == null) return;

            foreach (var id in expired)
                _ = ClearOutgoingInviteIfPresentAsync(id, "timeout");
        }

        /// <summary>
        /// Returns true when the active scene is Menu_Main (or Authentication,
        /// where initial setup runs). False during game scenes so the refresh
        /// loop and party-session recreation are suspended.
        /// </summary>
        private static bool IsOnMenuScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            return sceneName == "Menu_Main" || sceneName == "Authentication";
        }


        async void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeFromProfileChanges();
            UnsubscribeFromGameLaunch();
            UninstallLobbyLogFilter();
            await LeavePresenceLobbyAsync();

            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Resets invite dedup state when Menu_Main loads so stale
        /// <see cref="_lastFiredInvite"/> from a prior session doesn't
        /// block new invite detection. Also pushes an immediate presence
        /// publish so remote players see this user back out of the match
        /// without waiting up to a full refresh interval.
        ///
        /// IMPORTANT: <see cref="HostConnectionDataSO.PartyMembers"/> is an
        /// Obvious.Soap <c>ScriptableList</c>, which clears itself on every
        /// <c>LoadSceneMode.Single</c> scene load. The invite flow no longer
        /// triggers a Menu_Main reload, but returning to Menu_Main from a game
        /// scene (<c>SceneLoader.ReturnToMainMenu</c>) still uses
        /// <c>LoadSceneMode.Single</c> and wipes the list — so repopulate from
        /// the authoritative <c>_partySession.Players</c> whenever Menu_Main
        /// comes back into focus. Safe no-op when no party session exists.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Menu_Main")
            {
                _lastFiredInvite = null;
                _lastInviteResolved = false;
                PublishPresenceImmediateAsync().Forget();
                RepopulatePartyMembersFromSession();
            }
        }

        /// <summary>
        /// Rebuilds <see cref="HostConnectionDataSO.PartyMembers"/> from the
        /// current <see cref="_partySession"/>'s Players list. Fires
        /// <see cref="HostConnectionDataSO.OnPartyMemberJoined"/> for each
        /// re-added remote member so subscribed UI (<c>ArcadeLobbyList</c>,
        /// <c>FriendsListPanel</c>) re-renders slots. Safe to call with a null
        /// session — no-op.
        /// </summary>
        private void RepopulatePartyMembersFromSession()
        {
            if (_partySession == null || connectionData == null || connectionData.PartyMembers == null)
                return;

            connectionData.PartyMembers.Clear();
            connectionData.PartyMembers.Add(connectionData.LocalPlayerData);

            foreach (var p in _partySession.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;

                string displayName = "Unknown Pilot";
                int avatarId = 0;

                if (p.Properties != null)
                {
                    if (p.Properties.TryGetValue(DISPLAY_NAME_KEY, out var dn) &&
                        !string.IsNullOrEmpty(dn.Value))
                        displayName = dn.Value;
                    if (p.Properties.TryGetValue(AVATAR_ID_KEY, out var av) &&
                        int.TryParse(av.Value, out int parsed))
                        avatarId = parsed;
                }

                var memberData = new PartyPlayerData(p.Id, displayName, avatarId);
                connectionData.PartyMembers.Add(memberData);
                connectionData.OnPartyMemberJoined?.Raise(memberData);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public: Auth hooks (wire via SOAP EventListenerNoParam in inspector)
        // ─────────────────────────────────────────────────────────────────────

        public async void HandleSignedInEvent()
        {
            if (_initialized || _joining) return;
            if (!IsAuthSignedInAndHasId()) return;

            SubscribeToProfileChanges();
            SubscribeToGameLaunch();

            // Wait for PlayerDataService to merge the cloud profile before
            // SyncLocalIdentity freezes the display name — without this, a
            // sign-in that beats cloud-save loading would advertise the local
            // "Pilot{XXXX}" default until the next profile-change republish.
            await WaitForProfileInitAsync(PROFILE_INIT_TIMEOUT_MS);
            SyncLocalIdentity();

            _joining = true;
            try
            {
                await JoinPresenceLobbyAsync();

                // Catch late profile resolution (fired while _presenceLobby
                // was still null inside JoinPresenceLobbyAsync) — re-read
                // identity and push it to the just-joined lobby.
                SyncLocalIdentity();
                RepublishLocalIdentityAsync().Forget();

                // Party session is created lazily on first SendInviteAsync —
                // see Start() for the rationale. Don't burn a Relay allocation
                // (and a vessel respawn) just because we signed in.

                _initialized = true;
                DebugExtensions.LogColored(
                    $"[HostConnectionService] Initialized (HandleSignedInEvent) — lobby: {_presenceLobby?.Id ?? "NULL"}, " +
                    $"partySession: {_partySession?.Id ?? "NULL"}, " +
                    $"localId: {connectionData.LocalPlayerId}",
                    Color.green);

                // Immediate refresh so OnlinePlayers is populated right away.
                RefreshAsync().Forget();
            }
            finally { _joining = false; }
        }

        public async void HandleSignedOutEvent()
        {
            _initialized = false;
            connectionData.ResetRuntimeData();
            await LeavePresenceLobbyAsync();
            connectionData.OnHostConnectionLost?.Raise();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public: Invite API
        // ─────────────────────────────────────────────────────────────────────

        public async Task SendInviteAsync(string targetPlayerId)
        {
            DebugExtensions.LogColored(
                $"[INVITE-SEND] SendInviteAsync called — target: {targetPlayerId}", Color.cyan);

            if (_presenceLobby == null)
            {
                DebugExtensions.LogErrorColored(
                    "[INVITE-SEND] ABORT — _presenceLobby is null", Color.red);
                throw new InvalidOperationException("Presence lobby unavailable.");
            }

            // Idempotency: re-clicking an already-pending row is a no-op rather than
            // a stack of duplicate save operations.
            if (_outgoingInvites.ContainsKey(targetPlayerId))
            {
                DebugExtensions.LogColored(
                    $"[INVITE-SEND] {targetPlayerId} already has a pending invite — refreshing timeout",
                    Color.yellow);
                var existing = _outgoingInvites[targetPlayerId];
                existing.ExpiresAt = Time.unscaledTime + OUTGOING_INVITE_TIMEOUT_SECONDS;
                _outgoingInvites[targetPlayerId] = existing;
                return;
            }

            // Wait for any in-flight RefreshAsync to finish so the SDK's
            // internal player index is stable before we call SaveCurrentPlayerDataAsync.
            while (_lobbyBusy)
                await Task.Yield();
            _lobbyBusy = true;

            bool inviteAdded = false;
            try
            {
                SyncLocalIdentity();
                DebugExtensions.LogColored(
                    $"[INVITE-SEND] LocalPlayerId: {connectionData.LocalPlayerId}, " +
                    $"DisplayName: {connectionData.LocalDisplayName}", Color.cyan);

                // No session creation here — the Relay party session is created
                // lazily when the first recipient accepts (detected by
                // ScanForAcceptanceSignalsAsync in RefreshAsync). Sending an invite
                // must never touch the NetworkManager so the host's vessel is not
                // destroyed.
                DebugExtensions.LogColored(
                    $"[INVITE-SEND] PartySession ID: {_partySession?.Id ?? PENDING_SESSION_ID} " +
                    $"(PENDING until first accept)", Color.cyan);

                string payload = BuildInvitePayload(targetPlayerId);

                // Add to local map BEFORE the network save so the composite
                // serialization picks up this invite. Roll back on failure.
                _outgoingInvites[targetPlayerId] = new OutgoingInvite
                {
                    Payload = payload,
                    ExpiresAt = Time.unscaledTime + OUTGOING_INVITE_TIMEOUT_SECONDS,
                };
                inviteAdded = true;

                DebugExtensions.LogColored(
                    $"[INVITE-SEND] target='{targetPlayerId}', payload='{payload}'", Color.cyan);

                // Best-effort refresh to sync SDK player list cache before setting
                // properties. Without this, SaveCurrentPlayerDataAsync can fail
                // silently if the SDK's internal player index is stale.
                try { await _presenceLobby.RefreshAsync(); }
                catch { /* non-fatal — SaveWithRetryAsync handles stale state */ }

                PublishInvitePayloadsToCurrentPlayer();
                await SaveWithRetryAsync();

                DebugExtensions.LogColored(
                    "[INVITE-SEND] SaveCurrentPlayerDataAsync completed — properties persisted",
                    Color.green);

                foreach (var player in connectionData.OnlinePlayers.ToList())
                {
                    if (player.PlayerId == targetPlayerId)
                    {
                        connectionData.OnInviteSent?.Raise(player);
                        DebugExtensions.LogColored(
                            $"[INVITE-SEND] OnInviteSent raised for {player.DisplayName}",
                            Color.green);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                DebugExtensions.LogErrorColored(
                    $"[INVITE-SEND] ERROR: {e.Message}\n{e.StackTrace}", Color.red);

                // Roll back the local entry so the row reverts to ONLINE.
                if (inviteAdded)
                {
                    _outgoingInvites.Remove(targetPlayerId);
                    OutgoingInviteCleared?.Invoke(targetPlayerId);
                }

                // Surface the failure so callers (FriendsListPanel.OnInviteClicked)
                // hit their catch block and reset the row visually.
                throw;
            }
            finally
            {
                // Push the next polling refresh a full interval out so we don't
                // immediately hit the rate limit with a back-to-back request.
                _refreshTimer = 0f;

                // Boost the refresh cadence for a short window so the sender
                // detects the invitee's joined_party presence (and the party
                // session player list) within ~1s instead of ~3s. Critical for
                // the arcade lobby list to populate promptly after a successful
                // accept.
                _boostedRefreshUntil = Time.unscaledTime + BOOSTED_REFRESH_WINDOW_SECONDS;

                _lobbyBusy = false;
            }
        }

        /// <summary>
        /// Builds the per-invite payload line:
        /// <c>targetPlayerId|hostPlayerId|sessionId|hostDisplayName|avatarId</c>.
        /// When <see cref="_partySession"/> is null (Relay session not yet created),
        /// <see cref="PENDING_SESSION_ID"/> is used. Recipients must check for this
        /// sentinel and poll <see cref="WaitForRealSessionIdAsync"/> before joining.
        /// </summary>
        private string BuildInvitePayload(string targetPlayerId)
        {
            string sessionIdField = _partySession?.Id ?? PENDING_SESSION_ID;
            return $"{targetPlayerId}{INVITE_FIELD_SEPARATOR}" +
                   $"{connectionData.LocalPlayerId}{INVITE_FIELD_SEPARATOR}" +
                   $"{sessionIdField}{INVITE_FIELD_SEPARATOR}" +
                   $"{connectionData.LocalDisplayName}{INVITE_FIELD_SEPARATOR}" +
                   $"{connectionData.LocalAvatarId}";
        }

        /// <summary>
        /// Serializes the current outgoing-invite map into the composite
        /// <see cref="INVITE_PAYLOADS_KEY"/> property and stages it on
        /// CurrentPlayer. The actual save is the caller's responsibility.
        /// </summary>
        private void PublishInvitePayloadsToCurrentPlayer()
        {
            string composite = string.Empty;
            if (_outgoingInvites.Count > 0)
            {
                var lines = new List<string>(_outgoingInvites.Count);
                foreach (var kv in _outgoingInvites)
                    lines.Add(kv.Value.Payload);
                composite = string.Join(INVITE_LINE_SEPARATOR.ToString(), lines);
            }

            _presenceLobby.CurrentPlayer.SetProperty(INVITE_PAYLOADS_KEY,
                new PlayerProperty(composite, VisibilityPropertyOptions.Public));
        }

        public async Task AcceptInviteAsync(PartyInviteData invite)
        {
            // Mark resolved up-front so a re-opened FriendsListPanel doesn't
            // re-spawn a row for the invite the user just accepted, even if
            // _lastFiredInvite lingers for SDK-side dedup.
            _lastInviteResolved = true;
            connectionData.OnInviteResolved?.Raise();
            try
            {
                SyncLocalIdentity();

                // Step 1: Tell the host we accepted so they create the Relay
                // session. This must reach the lobby before the host's next
                // RefreshAsync tick so the session is ready when we poll below.
                await PublishAcceptanceSignalAsync(invite.HostPlayerId);

                // Step 2: Resolve the real session id.
                // If the invite already carries a real id (host had a pre-existing
                // session from a prior invite), skip polling entirely.
                string realSessionId = invite.PartySessionId;
                if (string.IsNullOrEmpty(realSessionId) || realSessionId == PENDING_SESSION_ID)
                {
                    Debug.Log("[HostConnectionService] Invite has PENDING session — polling for real id...");
                    // Boost refresh cadence so the lobby data updates fast.
                    _boostedRefreshUntil = Time.unscaledTime + BOOSTED_REFRESH_WINDOW_SECONDS;
                    realSessionId = await WaitForRealSessionIdAsync(invite.HostPlayerId);
                }

                // Step 3: Join the now-real session.
                _partySession = await MultiplayerService.Instance.JoinSessionByIdAsync(
                    realSessionId,
                    new JoinSessionOptions { PlayerProperties = BuildLocalPlayerProperties() });

                connectionData.IsHost = false;

                // Add self + host to party members
                connectionData.PartyMembers?.Clear();
                connectionData.PartyMembers?.Add(connectionData.LocalPlayerData);
                var hostData = new PartyPlayerData(invite.HostPlayerId, invite.HostDisplayName, invite.HostAvatarId);
                connectionData.PartyMembers?.Add(hostData);
                connectionData.OnPartyMemberJoined?.Raise(hostData);

                // Give the new session a grace period before the refresh loop
                // hits it — the join itself already consumed API quota and an
                // immediate RefreshAsync would trigger HTTP 429 Too Many Requests.
                _refreshTimer = -(refreshIntervalSeconds);

                // Keep _lastFiredInvite set so the dedup guard prevents
                // re-triggering if the host is slow to clear their properties.
                Debug.Log($"[HostConnectionService] Joined party {_partySession.Id}");

                // Stay in fast-poll mode long enough for the party-session
                // Players list to replicate so remote members (including the
                // host) appear in the local arcade list within ~1s instead of
                // waiting up to the full slow-polling interval.
                _boostedRefreshUntil = Time.unscaledTime + BOOSTED_REFRESH_WINDOW_SECONDS;

                // Advertise this join on the presence lobby so the sender's
                // RefreshAsync() picks us up even before their party-session
                // Players list catches up. See JOINED_PARTY_KEY docstring.
                PublishJoinedPartyAsync(realSessionId).Forget();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] AcceptInvite error: {e.Message}");
            }
        }

        /// <summary>
        /// Writes <see cref="JOINED_PARTY_KEY"/> = sessionId to the presence
        /// lobby so the party host can detect the join via their presence
        /// refresh. Honors the <see cref="_lobbyBusy"/> mutex.
        /// </summary>
        private async UniTaskVoid PublishJoinedPartyAsync(string partySessionId)
        {
            if (_presenceLobby == null || string.IsNullOrEmpty(partySessionId)) return;

            while (_lobbyBusy)
                await Task.Yield();
            _lobbyBusy = true;
            try
            {
                await _presenceLobby.RefreshAsync();
                _presenceLobby.CurrentPlayer.SetProperty(JOINED_PARTY_KEY,
                    new PlayerProperty(partySessionId, VisibilityPropertyOptions.Public));
                await SaveWithRetryAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] PublishJoinedParty error: {e.Message}");
            }
            finally
            {
                _lobbyBusy = false;
            }
        }

        /// <summary>
        /// Clears this client's <see cref="JOINED_PARTY_KEY"/> property so the
        /// host's presence-scan no longer sees them as a member. Fire-and-forget
        /// from leave/clear paths.
        /// </summary>
        private async UniTaskVoid ClearJoinedPartyAsync()
        {
            if (_presenceLobby == null) return;

            while (_lobbyBusy)
                await Task.Yield();
            _lobbyBusy = true;
            try
            {
                await _presenceLobby.RefreshAsync();
                _presenceLobby.CurrentPlayer.SetProperty(JOINED_PARTY_KEY,
                    new PlayerProperty(string.Empty, VisibilityPropertyOptions.Public));
                await SaveWithRetryAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] ClearJoinedParty error: {e.Message}");
            }
            finally
            {
                _lobbyBusy = false;
            }
        }

        /// <summary>
        /// Writes <see cref="ACCEPTED_INVITE_KEY"/> = hostPlayerId to the
        /// presence lobby, signalling to the host that this client accepted
        /// their invite and the host should create the Relay party session.
        /// Mirrors <see cref="PublishJoinedPartyAsync"/>; honors the
        /// <see cref="_lobbyBusy"/> mutex.
        /// </summary>
        private async Task PublishAcceptanceSignalAsync(string hostPlayerId)
        {
            if (_presenceLobby == null || string.IsNullOrEmpty(hostPlayerId)) return;

            while (_lobbyBusy)
                await Task.Yield();
            _lobbyBusy = true;
            try
            {
                await _presenceLobby.RefreshAsync();
                _presenceLobby.CurrentPlayer.SetProperty(ACCEPTED_INVITE_KEY,
                    new PlayerProperty(hostPlayerId, VisibilityPropertyOptions.Public));
                await SaveWithRetryAsync();
                Debug.Log($"[HostConnectionService] Published acceptance signal to host {hostPlayerId}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] PublishAcceptanceSignal error: {e.Message}");
            }
            finally
            {
                _lobbyBusy = false;
            }
        }

        /// <summary>
        /// Polls the host's presence-lobby invite_payloads property until the
        /// invite line for the local player contains a non-PENDING session id,
        /// then returns it. Piggybacks on the boosted refresh (0.75s) that is
        /// already running after <see cref="AcceptInviteAsync"/>. Does NOT hold
        /// <see cref="_lobbyBusy"/> — reads only cached <see cref="_presenceLobby"/>
        /// data, which RefreshAsync populates on a separate lock cycle.
        /// </summary>
        private async Task<string> WaitForRealSessionIdAsync(string hostPlayerId, float timeoutSeconds = 7f)
        {
            float deadline = Time.unscaledTime + timeoutSeconds;
            while (Time.unscaledTime < deadline)
            {
                await Task.Delay(400);
                if (_presenceLobby == null) continue;

                foreach (var p in _presenceLobby.Players)
                {
                    if (p.Id != hostPlayerId) continue;
                    if (!p.Properties.TryGetValue(INVITE_PAYLOADS_KEY, out var prop)) break;
                    if (string.IsNullOrEmpty(prop?.Value)) break;

                    foreach (var line in prop.Value.Split(INVITE_LINE_SEPARATOR))
                    {
                        var parsed = ParseInviteLine(line);
                        if (!parsed.HasValue) continue;
                        if (parsed.Value.targetId != connectionData.LocalPlayerId) continue;

                        var sid = parsed.Value.invite.PartySessionId;
                        if (!string.IsNullOrEmpty(sid) && sid != PENDING_SESSION_ID)
                        {
                            Debug.Log($"[HostConnectionService] Real session id resolved: {sid}");
                            return sid;
                        }
                    }
                    break;
                }
            }
            throw new TimeoutException(
                $"[HostConnectionService] Host {hostPlayerId} did not publish a real session id within {timeoutSeconds}s.");
        }

        /// <summary>
        /// Host-only. Scans the presence lobby for players who have set
        /// <see cref="ACCEPTED_INVITE_KEY"/> = our player id AND still appear
        /// in <see cref="_outgoingInvites"/>. On first detection, lazily creates
        /// the Relay party session and republishes all pending invite payloads
        /// with the real session id so recipients can join.
        /// Called from <see cref="RefreshAsync"/> while <see cref="_lobbyBusy"/>
        /// is held; <see cref="CreatePartySessionAsync"/> manages its own NM
        /// lifecycle safely within that context.
        /// </summary>
        private async Task ScanForAcceptanceSignalsAsync()
        {
            foreach (var p in _presenceLobby.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;
                if (!_outgoingInvites.ContainsKey(p.Id)) continue;

                if (!p.Properties.TryGetValue(ACCEPTED_INVITE_KEY, out var prop)) continue;
                if (prop?.Value != connectionData.LocalPlayerId) continue;

                Debug.Log($"[ACCEPT-SCAN] Acceptance signal from {p.Id} — creating party session...");

                if (_partySession == null)
                {
                    // Release the lobby mutex before calling CreatePartySessionAsync,
                    // which acquires its own NM-shutdown/Relay-create pipeline and may
                    // take several seconds. RefreshAsync re-acquires _lobbyBusy after
                    // this await via normal flow.
                    _lobbyBusy = false;
                    try
                    {
                        await CreatePartySessionAsync();
                    }
                    finally
                    {
                        _lobbyBusy = true;
                    }
                }

                await RepublishOutgoingInvitesWithRealSessionIdAsync();
                return;
            }
        }

        /// <summary>
        /// Rewrites all entries in <see cref="_outgoingInvites"/> to embed the
        /// real <see cref="_partySession"/> id, then republishes the composite
        /// <see cref="INVITE_PAYLOADS_KEY"/> property. Called after the Relay
        /// session is created so recipients polling for the real id can resolve.
        /// </summary>
        private async Task RepublishOutgoingInvitesWithRealSessionIdAsync()
        {
            if (_partySession == null || _outgoingInvites.Count == 0) return;

            foreach (var targetId in new List<string>(_outgoingInvites.Keys))
            {
                var entry = _outgoingInvites[targetId];
                entry.Payload = BuildInvitePayload(targetId); // now uses _partySession.Id
                _outgoingInvites[targetId] = entry;
            }

            PublishInvitePayloadsToCurrentPlayer();
            await SaveWithRetryAsync();
            Debug.Log($"[HostConnectionService] Republished {_outgoingInvites.Count} pending invite(s) with real session id {_partySession.Id}");
        }

        public Task DeclineInviteAsync()
        {
            // Pure local state reset on the recipient side. The sender's slot is
            // freed by their own timeout (OUTGOING_INVITE_TIMEOUT_SECONDS) since
            // UGS lobbies give us no decline signal back to the sender.
            _lastFiredInvite = null;
            _lastInviteResolved = true;
            connectionData.OnInviteResolved?.Raise();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Leaves the current party and returns the local player to Menu_Main.
        /// Delegates to <see cref="PartyInviteController"/> which handles the
        /// Netcode shutdown + fresh host restart. Intended for UI leave-party
        /// buttons (e.g. <c>ArcadeLobbyList</c>).
        ///
        /// Reuses the <see cref="HostConnectionDataSO.OnInviteResolved"/> SOAP
        /// channel so any open invite popup (PartyInviteNotificationPanel) and
        /// any pending-invite rows in <see cref="UI.FriendsListPanel"/> dismiss
        /// the moment the user leaves, instead of waiting up to a full refresh
        /// interval to repopulate. Mirrors the Accept/Decline contract.
        /// </summary>
        public async Task LeavePartyAsync()
        {
            var controller = PartyInviteController.Instance;
            if (controller == null)
            {
                Debug.LogWarning("[HostConnectionService] PartyInviteController not available.");
                return;
            }

            // Treat "leave" as a resolution event — any stale invite from the
            // host we are leaving must not re-fire as a fresh notification, and
            // any popup currently visible should clear.
            _lastFiredInvite = null;
            _lastInviteResolved = true;
            connectionData.OnInviteResolved?.Raise();

            // Locally fire OnPartyMemberLeft for every remote member so panels
            // that key off party-membership SOAP events (ArcadeLobbyList,
            // PartyAreaPanel, PartyArcadeView) clear their slots immediately
            // instead of waiting for the next 3-second refresh tick.
            if (connectionData.PartyMembers != null && connectionData.OnPartyMemberLeft != null)
            {
                foreach (var member in connectionData.PartyMembers.ToList())
                {
                    if (member.PlayerId == connectionData.LocalPlayerId) continue;
                    connectionData.OnPartyMemberLeft.Raise(member);
                }
            }

            // If this client advertised a joined_party presence property,
            // clear it so the host's presence scan stops treating us as a
            // member as soon as we leave.
            ClearJoinedPartyAsync().Forget();

            await controller.LeavePartyAndReturnToMenuAsync();
        }

        /// <summary>
        /// Kicks a remote player from the party. Host-only.
        /// Removes from the local PartyMembers list and fires OnPartyMemberKicked.
        /// </summary>
        public async Task KickPartyMemberAsync(string playerId)
        {
            if (!connectionData.IsHost)
            {
                Debug.LogWarning("[HostConnectionService] Only the host can kick party members.");
                return;
            }

            if (playerId == connectionData.LocalPlayerId)
            {
                Debug.LogWarning("[HostConnectionService] Cannot kick yourself from the party.");
                return;
            }

            connectionData.RemovePartyMember(playerId);

            // If we have a party session, attempt to remove the player from it
            if (_partySession != null)
            {
                try
                {
                    await _partySession.AsHost().RemovePlayerAsync(playerId);
                    Debug.Log($"[HostConnectionService] Kicked {playerId} from party session.");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[HostConnectionService] KickPartyMember session error: {e.Message}");
                }
            }
        }

        public ISession PartySession => _partySession;

        /// <summary>
        /// On-demand refresh trigger. Safe to call from any UI code (panel open,
        /// user pressed refresh, just-accepted an invite, just-sent an invite).
        /// Respects the <see cref="FORCE_REFRESH_COOLDOWN_SECONDS"/> debounce and
        /// the existing <see cref="_lobbyBusy"/> mutex, so a burst of calls
        /// collapses to at most one actual lobby hit. Also extends the boost
        /// window so the next few polling ticks are fast.
        /// </summary>
        public void ForceRefreshNow()
        {
            if (!_initialized || _presenceLobby == null) return;

            _boostedRefreshUntil = Time.unscaledTime + BOOSTED_REFRESH_WINDOW_SECONDS;

            if (Time.unscaledTime < _nextForcedRefreshAllowed) return;
            _nextForcedRefreshAllowed = Time.unscaledTime + FORCE_REFRESH_COOLDOWN_SECONDS;

            _refreshTimer = 0f;
            if (_lobbyBusy) return;
            RefreshAsync().Forget();
        }

        /// <summary>
        /// Most recently detected incoming party invite, or null if no invite is
        /// currently pending. Cleared when the user accepts/declines (via
        /// <see cref="AcceptInviteAsync"/> or <see cref="DeclineInviteAsync"/>)
        /// or when Menu_Main reloads.
        ///
        /// UI panels that subscribe to <see cref="HostConnectionDataSO.OnInviteReceived"/>
        /// via OnEnable should also read this on show, so invites that arrived while
        /// the panel was hidden are not missed.
        /// </summary>
        public PartyInviteData? LastPendingInvite =>
            _lastInviteResolved ? null : _lastFiredInvite;

        /// <summary>
        /// Clears the stale party session reference so the next refresh cycle
        /// (or explicit call to <see cref="CreatePartySessionPublicAsync"/>)
        /// will create a fresh Relay-backed session.
        /// Called by <see cref="Core.SceneLoader"/> when returning to Menu_Main
        /// after a game, since the game's LeaveSession() deleted the UGS game
        /// session and shut down the NetworkManager.
        /// </summary>
        public void ClearStalePartySession()
        {
            if (_partySession == null) return;
            Debug.Log("[HostConnectionService] Clearing stale party session reference.");
            _partySession = null;
            _lastFiredInvite = null;
            connectionData.PartyMembers?.Clear();

            // If we were a client advertising a joined party, clear that
            // property so we don't get re-added to the stale host's party
            // member list on their next presence scan.
            ClearJoinedPartyAsync().Forget();
        }

        /// <summary>
        /// Public wrapper for party session creation.
        /// Used by <see cref="PartyInviteController"/> after shutting down the local
        /// NetworkManager, so the Relay session can start a fresh host.
        /// </summary>
        public async Task CreatePartySessionPublicAsync()
        {
            if (_partySession != null)
            {
                Debug.Log("[HostConnectionService] Party session already exists.");
                return;
            }

            SyncLocalIdentity();
            await CreatePartySessionAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Presence Lobby
        // ─────────────────────────────────────────────────────────────────────

        private async Task JoinPresenceLobbyAsync()
        {
            if (_presenceLobby != null) return;

            // The presence lobby is a lobby-only session (no Relay) used purely
            // for player discovery and invite property exchange. It coexists
            // safely with an active NetworkManager host/client.

            try
            {
                _presenceLobby = await TryQueryAndJoinLobbyAsync();

                if (_presenceLobby == null)
                {
                    // No existing lobby found — create one, then re-query.
                    // Another instance may have created one at the same time
                    // (race condition with MPPM or near-simultaneous launches).
                    await CreatePresenceLobbyAsync();

                    // Re-query after a short settle to detect a race.
                    await Task.Delay(LOBBY_RACE_SETTLE_MS);
                    var rival = await TryQueryAndJoinLobbyAsync();

                    if (rival != null)
                    {
                        // Another lobby appeared — abandon ours and join theirs.
                        Debug.Log("[HostConnectionService] Race detected — merging into existing lobby.");
                        await DeleteOwnLobbyQuietly();
                        _presenceLobby = rival;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] Join failed, creating new lobby: {e.Message}");
                if (_presenceLobby == null)
                    await CreatePresenceLobbyAsync();
            }

            if (_presenceLobby != null)
            {
                connectionData.IsConnected = true;
                connectionData.IsHost = _presenceLobby.IsHost;

                // Seed party members with self
                connectionData.PartyMembers?.Clear();
                connectionData.PartyMembers?.Add(connectionData.LocalPlayerData);

                connectionData.OnHostConnectionEstablished?.Raise();
            }
        }

        /// <summary>
        /// Queries for an existing PRESENCE_LOBBY session and joins the first one found.
        /// Returns the joined session, or null if none exist.
        /// </summary>
        private async Task<ISession> TryQueryAndJoinLobbyAsync()
        {
            var queryOptions = new QuerySessionsOptions();
            queryOptions.FilterOptions.Add(
                new FilterOption(FilterField.StringIndex1, PRESENCE_LOBBY_GAME_MODE, FilterOperation.Equal));

            IList<ISessionInfo> sessions = null;
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    var results = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);
                    sessions = results.Sessions;
                    break;
                }
                catch (Exception qe) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(qe))
                {
                    int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                    Debug.LogWarning($"[HostConnectionService] Rate limited querying lobby — retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                    await Task.Delay(delay);
                }
            }

            if (sessions.Count == 0)
                return null;

            // Try each session — the first one may be our own (skip it).
            foreach (var session in sessions)
            {
                // Skip sessions we already own.
                if (_presenceLobby != null && session.Id == _presenceLobby.Id)
                    continue;

                try
                {
                    var joined = await MultiplayerService.Instance.JoinSessionByIdAsync(
                        session.Id,
                        new JoinSessionOptions { PlayerProperties = BuildLocalPlayerProperties() });

                    Debug.Log($"[HostConnectionService] Joined presence lobby {joined.Id}");
                    return joined;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HostConnectionService] Join session {session.Id} failed: {e.Message}");
                    if (IsRateLimitException(e))
                        await Task.Delay(RATE_LIMIT_BASE_DELAY_MS);
                }
            }

            return null;
        }

        /// <summary>
        /// Deletes the locally-created presence lobby without throwing.
        /// Used when a race condition is detected and we need to merge into another lobby.
        /// </summary>
        private async Task DeleteOwnLobbyQuietly()
        {
            if (_presenceLobby == null) return;
            try
            {
                if (_presenceLobby.IsHost)
                    await _presenceLobby.AsHost().DeleteAsync();
                else
                    await _presenceLobby.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] DeleteOwnLobby error: {e.Message}");
            }
        }

        private async Task CreatePresenceLobbyAsync()
        {
            try
            {
                // Lobby-only session: no WithRelayNetwork() because this session
                // is used purely for player discovery and invite property exchange.
                // Relay is only needed on the party session (actual gameplay).
                var opts = new SessionOptions
                {
                    MaxPlayers = presenceLobbyMaxPlayers,
                    IsLocked = false,
                    IsPrivate = false,
                    PlayerProperties = BuildLocalPlayerProperties(),
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        {
                            "gameMode",
                            new SessionProperty(PRESENCE_LOBBY_GAME_MODE,
                                VisibilityPropertyOptions.Public,
                                PropertyIndex.String1)
                        }
                    }
                };

                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        _presenceLobby = await MultiplayerService.Instance.CreateSessionAsync(opts);
                        connectionData.IsHost = true;
                        Debug.Log($"[HostConnectionService] Created presence lobby {_presenceLobby.Id}");
                        return;
                    }
                    catch (Exception re) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(re))
                    {
                        int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                        Debug.LogWarning($"[HostConnectionService] Rate limited creating presence lobby — retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                        await Task.Delay(delay);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[HostConnectionService] Could not create presence lobby: {e.Message}");
            }
        }

        private async Task LeavePresenceLobbyAsync()
        {
            if (_presenceLobby == null || _leaving) return;
            _leaving = true;
            try
            {
                if (_presenceLobby.IsHost)
                    await _presenceLobby.AsHost().DeleteAsync();
                else
                    await _presenceLobby.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] Leave error: {e.Message}");
            }
            finally
            {
                _presenceLobby = null;
                _leaving = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Refresh
        // ─────────────────────────────────────────────────────────────────────

        private async UniTaskVoid RefreshAsync()
        {
            if (_presenceLobby == null || _lobbyBusy) return;

            _lobbyBusy = true;
            bool shouldReconnect = false;
            try
            {
                await _presenceLobby.RefreshAsync();

                // ── Online player list (diff-based) ─────────────────────────
                // Build the fresh set, then add/remove only what changed.
                // This avoids the Clear() + re-Add() pattern that causes
                // the OnlinePlayersPanel to flicker and rebuild every cycle.
                if (connectionData.OnlinePlayers != null)
                    RefreshOnlinePlayersDiff();

                // ── Invite check (scan player properties) ──────────────────
                // Each player publishes their outstanding invites in a single
                // composite INVITE_PAYLOADS_KEY property — one line per invite,
                // first field of each line is the target id. We're invited if any
                // sender has a line targeting our local player id.
                foreach (var p in _presenceLobby.Players)
                {
                    if (p.Id == connectionData.LocalPlayerId) continue;

                    if (TryFindIncomingInvite(p, out var invite))
                        TryRaiseIncomingInvite(invite);
                }

                // ── Acceptance signal scan (host-only, lazy session creation) ──
                // When outgoing invites carry PENDING session ids, clients write
                // ACCEPTED_INVITE_KEY = our player id to signal acceptance.
                // Detect it here, create the Relay session once, then republish
                // all pending invites with the real session id so recipients can join.
                // Must run BEFORE the JOINED_PARTY_KEY scan below because recipients
                // won't set JOINED_PARTY_KEY until after they read the real session id.
                if (connectionData.IsHost && _outgoingInvites.Count > 0)
                    await ScanForAcceptanceSignalsAsync();

                // ── Presence-lobby party-join scan (host only) ──────────────
                // Clients advertise their party join via JOINED_PARTY_KEY so we
                // can detect them even when the party-session Players list is
                // still stale. This is the authoritative fast path for the
                // sender's arcade lobby list.
                if (_partySession != null && connectionData.IsHost)
                    ScanPresenceForJoinedPartyMembers();

                // ── Party session member tracking ───────────────────────────
                if (_partySession != null)
                    await RefreshPartyMembersAsync();

                // ── Publish local party state to presence lobby if changed ──
                await PublishPartyStateIfChangedAsync();

                _consecutiveRefreshErrors = 0;
            }
            catch (Exception e)
            {
                if (IsRateLimitException(e))
                {
                    _rateLimitBackoffUntil = Time.unscaledTime + refreshIntervalSeconds * 2;
                    Debug.LogWarning("[HostConnectionService] Rate limited during refresh — backing off");
                }
                else
                {
                    Debug.LogWarning($"[HostConnectionService] Refresh error: {e.Message}");
                    _consecutiveRefreshErrors++;
                    if (_consecutiveRefreshErrors >= MAX_REFRESH_ERRORS_BEFORE_RECONNECT)
                    {
                        Debug.LogWarning($"[HostConnectionService] {_consecutiveRefreshErrors} consecutive refresh errors — reconnecting to presence lobby");
                        _consecutiveRefreshErrors = 0;
                        _presenceLobby = null;
                        shouldReconnect = true;
                    }
                }
            }
            finally
            {
                _lobbyBusy = false;
            }

            // Reconnect outside the try/finally so _lobbyBusy is released first.
            if (shouldReconnect)
                await JoinPresenceLobbyAsync();
        }

        /// <summary>
        /// Walks <paramref name="sender"/>'s composite invite_payloads property
        /// looking for a line whose target is the local player. Returns true and
        /// sets <paramref name="invite"/> to the parsed payload on the first hit;
        /// false otherwise.
        /// </summary>
        private bool TryFindIncomingInvite(Unity.Services.Multiplayer.IReadOnlyPlayer sender, out PartyInviteData invite)
        {
            invite = default;
            if (!sender.Properties.TryGetValue(INVITE_PAYLOADS_KEY, out var payloadsProp))
                return false;
            if (payloadsProp == null || string.IsNullOrEmpty(payloadsProp.Value))
                return false;

            var lines = payloadsProp.Value.Split(INVITE_LINE_SEPARATOR);
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;

                var parsed = ParseInviteLine(line);
                if (!parsed.HasValue)
                {
                    DebugExtensions.LogErrorColored(
                        $"[INVITE-RECV] ParseInviteLine FAILED for line: '{line}'",
                        Color.red);
                    continue;
                }
                if (parsed.Value.targetId != connectionData.LocalPlayerId) continue;

                invite = parsed.Value.invite;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Parses a single invite line. Format:
        /// <c>targetPlayerId|hostPlayerId|sessionId|hostDisplayName|avatarId</c>.
        /// Returns null on any parse failure.
        /// </summary>
        private static (string targetId, PartyInviteData invite)? ParseInviteLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            var parts = line.Split(INVITE_FIELD_SEPARATOR);
            if (parts.Length < 5) return null;
            if (!int.TryParse(parts[4], out int avatarId)) return null;

            return (parts[0], new PartyInviteData(parts[1], parts[2], parts[3], avatarId));
        }

        /// <summary>
        /// Applies dedup + already-joined suppression and raises
        /// <see cref="HostConnectionDataSO.OnInviteReceived"/> for genuinely new
        /// invites. Pulled out of the refresh loop for readability and so unit
        /// tests can exercise the dedup logic without spinning a real lobby.
        /// </summary>
        private void TryRaiseIncomingInvite(PartyInviteData invite)
        {
            // Suppress invites that point at a party we're already a client in.
            // Without this, the Menu_Main reload during accept resets
            // _lastFiredInvite, and the next refresh would re-fire the same
            // invite before the sender has had a chance to clear their slot.
            if (_partySession != null &&
                !connectionData.IsHost &&
                _partySession.Id == invite.PartySessionId)
            {
                _lastFiredInvite = invite;
                _lastInviteResolved = true;
                return;
            }

            // Suppress re-fire when the host updates a PENDING invite to a real
            // session id after the local player has already accepted. The popup
            // is already visible / was already acted on; silently update the
            // cached record so WaitForRealSessionIdAsync can observe the change.
            if (_lastInviteResolved &&
                _lastFiredInvite.HasValue &&
                _lastFiredInvite.Value.HostPlayerId == invite.HostPlayerId)
            {
                _lastFiredInvite = invite;
                return;
            }

            bool isDuplicate = _lastFiredInvite.HasValue &&
                _lastFiredInvite.Value.PartySessionId == invite.PartySessionId &&
                _lastFiredInvite.Value.HostPlayerId == invite.HostPlayerId;

            if (isDuplicate) return;

            DebugExtensions.LogColored(
                $"[INVITE-RECV] New invite from '{invite.HostDisplayName}' " +
                $"(sessionId: {invite.PartySessionId})",
                Color.green);
            _lastFiredInvite = invite;
            _lastInviteResolved = false;
            connectionData.OnInviteReceived?.Raise(invite);

            // Keep the recipient in fast-poll mode while an invite is pending so
            // sender presence updates ("joined party", "cancelled") arrive within
            // ~1s instead of the slow 1.5s cadence.
            _boostedRefreshUntil = Time.unscaledTime + BOOSTED_REFRESH_WINDOW_SECONDS;
        }

        /// <summary>
        /// Diff-based update of the OnlinePlayers list.
        /// Adds new players, removes stale ones, without clearing the whole list.
        /// This prevents SOAP list events from firing OnCleared → OnItemAdded
        /// every cycle, which would cause UI to flicker.
        /// </summary>
        private void RefreshOnlinePlayersDiff()
        {
            // Build the fresh set from the lobby.
            var freshPlayerIds = new HashSet<string>();

            foreach (var p in _presenceLobby.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;
                freshPlayerIds.Add(p.Id);

                string displayName = "Unknown Pilot";
                int avatarId = 0;
                int partyCount = 0;
                int partyMax = 0;
                string matchName = string.Empty;

                if (p.Properties.TryGetValue(DISPLAY_NAME_KEY, out var dn) &&
                    !string.IsNullOrEmpty(dn.Value))
                    displayName = dn.Value;
                if (p.Properties.TryGetValue(AVATAR_ID_KEY, out var av) &&
                    int.TryParse(av.Value, out int parsedAv))
                    avatarId = parsedAv;
                if (p.Properties.TryGetValue(PARTY_COUNT_KEY, out var pc) &&
                    int.TryParse(pc.Value, out int parsedPc))
                    partyCount = parsedPc;
                if (p.Properties.TryGetValue(PARTY_MAX_KEY, out var pm) &&
                    int.TryParse(pm.Value, out int parsedPm))
                    partyMax = parsedPm;
                if (p.Properties.TryGetValue(MATCH_NAME_KEY, out var mn))
                    matchName = mn.Value ?? string.Empty;

                var playerData = new PartyPlayerData(
                    p.Id, displayName, avatarId, partyCount, partyMax, matchName);

                // Upsert: equality is by PlayerId, so we must replace to pick up
                // updated party-state fields even if the player was already present.
                int existingIdx = -1;
                for (int i = 0; i < connectionData.OnlinePlayers.Count; i++)
                {
                    if (connectionData.OnlinePlayers[i].PlayerId == p.Id)
                    {
                        existingIdx = i;
                        break;
                    }
                }

                if (existingIdx < 0)
                {
                    connectionData.OnlinePlayers.Add(playerData);
                }
                else
                {
                    var existing = connectionData.OnlinePlayers[existingIdx];
                    if (existing.DisplayName != playerData.DisplayName ||
                        existing.AvatarId != playerData.AvatarId ||
                        existing.PartyMemberCount != playerData.PartyMemberCount ||
                        existing.PartyMaxSlots != playerData.PartyMaxSlots ||
                        existing.MatchName != playerData.MatchName)
                    {
                        // Obvious.Soap's ScriptableList<T> indexer set
                        // (_list[index] = value) silently mutates without
                        // raising any event, so subscribers like
                        // FriendsListPanel.HandleOnlinePlayerChanged never
                        // observe the update — the user has to close+reopen
                        // the panel for PopulateOnlineSection to re-read the
                        // fresh partyCount/partyMax/matchName. RemoveAt +
                        // Insert fires OnItemRemoved then OnItemAdded at the
                        // same index, so the panel's handlers (which already
                        // do an in-place re-Populate on upsert) update the
                        // row in real time without flicker.
                        connectionData.OnlinePlayers.RemoveAt(existingIdx);
                        connectionData.OnlinePlayers.Insert(existingIdx, playerData);
                    }
                }
            }

            // Remove players no longer in the lobby.
            for (int i = connectionData.OnlinePlayers.Count - 1; i >= 0; i--)
            {
                if (!freshPlayerIds.Contains(connectionData.OnlinePlayers[i].PlayerId))
                    connectionData.OnlinePlayers.RemoveAt(i);
            }

            // If any of those departed players had an outstanding invite from us,
            // free the slot now so the row no longer carries a pending tint and
            // the slot is usable for a new invite. Done after the OnlinePlayers
            // diff above so the UI gets the row-removal first and the cleared
            // event second (idempotent if the row is already gone).
            if (_outgoingInvites.Count > 0)
            {
                List<string> departed = null;
                foreach (var kv in _outgoingInvites)
                {
                    if (!freshPlayerIds.Contains(kv.Key))
                    {
                        departed ??= new List<string>();
                        departed.Add(kv.Key);
                    }
                }
                if (departed != null)
                {
                    foreach (var id in departed)
                        _ = ClearOutgoingInviteIfPresentAsync(id, "presence-leave");
                }
            }
        }

        /// <summary>
        /// Host-side fallback that reads the presence lobby (not the party
        /// session) looking for players advertising
        /// <see cref="JOINED_PARTY_KEY"/> equal to our party session id.
        /// Any match is upserted into <see cref="HostConnectionDataSO.PartyMembers"/>
        /// and <see cref="HostConnectionDataSO.OnPartyMemberJoined"/> is raised.
        ///
        /// The UGS party-session <c>RefreshAsync</c> can lag the actual join by
        /// a refresh tick; the presence lobby reflects the property change on
        /// the next refresh. This keeps the sender's UI (arcade lobby list)
        /// from showing a blank slot after a successful invite accept, and
        /// also clears the stale invite properties as soon as the invited
        /// player appears.
        /// </summary>
        private void ScanPresenceForJoinedPartyMembers()
        {
            if (_presenceLobby == null || _partySession == null) return;
            if (connectionData.PartyMembers == null) return;

            var joinedPlayerIds = new List<string>();

            foreach (var p in _presenceLobby.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;

                if (!p.Properties.TryGetValue(JOINED_PARTY_KEY, out var joinedProp)) continue;
                if (string.IsNullOrEmpty(joinedProp.Value)) continue;
                if (joinedProp.Value != _partySession.Id) continue;

                string displayName = "Unknown Pilot";
                int avatarId = 0;

                if (p.Properties.TryGetValue(DISPLAY_NAME_KEY, out var dn) &&
                    !string.IsNullOrEmpty(dn.Value))
                    displayName = dn.Value;
                if (p.Properties.TryGetValue(AVATAR_ID_KEY, out var av) &&
                    int.TryParse(av.Value, out int parsed))
                    avatarId = parsed;

                var memberData = new PartyPlayerData(p.Id, displayName, avatarId);

                if (!connectionData.PartyMembers.Contains(memberData))
                {
                    connectionData.PartyMembers.Add(memberData);
                    connectionData.OnPartyMemberJoined?.Raise(memberData);
                    DebugExtensions.LogColored(
                        $"[INVITE-SEND] Presence scan detected joined member '{displayName}' ({p.Id})",
                        Color.green);

                    joinedPlayerIds.Add(p.Id);
                }
            }

            // Clear matching invite slots for players that just joined. Discard
            // the returned Task because we already own _lobbyBusy in this stack
            // and don't want to await mid-refresh.
            foreach (var joinedId in joinedPlayerIds)
                _ = ClearOutgoingInviteIfPresentAsync(joinedId, "presence-join");
        }

        private async Task RefreshPartyMembersAsync()
        {
            if (_partySession == null) return;
            if (connectionData.PartyMembers == null) return;

            // Refresh the party session so Players list is up-to-date.
            try { await _partySession.RefreshAsync(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] Party session refresh error: {e.Message}");

                if (IsRateLimitException(e))
                {
                    _rateLimitBackoffUntil = Time.unscaledTime + refreshIntervalSeconds * 2;
                    return;
                }

                // Non-recoverable error — drop the session reference. There is no
                // auto-recreate; the user must re-invite to spin one up. This is
                // intentional: every recreation costs a NetworkManager Shutdown+StartHost
                // and a vessel respawn, so silent background recreation is forbidden.
                _partySession = null;
                connectionData.PartyMembers?.Clear();
                return;
            }

            // Build a set of player IDs currently in the session
            var sessionPlayerIds = new HashSet<string>();
            foreach (var p in _partySession.Players)
                sessionPlayerIds.Add(p.Id);

            // Detect and add new members
            var joinedPlayerIds = new List<string>();
            foreach (var p in _partySession.Players)
            {
                if (p.Id == connectionData.LocalPlayerId) continue;

                string displayName = "Unknown Pilot";
                int avatarId = 0;

                if (p.Properties.TryGetValue(DISPLAY_NAME_KEY, out var dn) &&
                    !string.IsNullOrEmpty(dn.Value))
                    displayName = dn.Value;
                if (p.Properties.TryGetValue(AVATAR_ID_KEY, out var av) &&
                    int.TryParse(av.Value, out int parsed))
                    avatarId = parsed;

                var memberData = new PartyPlayerData(p.Id, displayName, avatarId);

                if (!connectionData.PartyMembers.Contains(memberData))
                {
                    connectionData.PartyMembers.Add(memberData);
                    connectionData.OnPartyMemberJoined?.Raise(memberData);

                    joinedPlayerIds.Add(p.Id);
                }
            }

            // Invited players joined — clear their slots so receivers stop seeing
            // the stale invite on every refresh cycle.
            foreach (var joinedId in joinedPlayerIds)
                await ClearOutgoingInviteIfPresentAsync(joinedId, "party-join");

            // Detect and remove members who left the session
            for (int i = connectionData.PartyMembers.Count - 1; i >= 0; i--)
            {
                var member = connectionData.PartyMembers[i];
                if (member.PlayerId == connectionData.LocalPlayerId) continue;

                if (!sessionPlayerIds.Contains(member.PlayerId))
                {
                    connectionData.PartyMembers.RemoveAt(i);
                    connectionData.OnPartyMemberLeft?.Raise(member);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Party Session
        // ─────────────────────────────────────────────────────────────────────

        private const int HOST_CONFLICT_MAX_RETRIES = 2;

        private static bool IsHostConflictException(Exception e)
        {
            return e.Message != null &&
                   e.Message.Contains("Failed to start NetworkManager component as host");
        }

        private async Task CreatePartySessionAsync()
        {
            if (_partySession != null) return;

            // If another caller is already creating the session, await that
            // operation instead of returning with _partySession still null.
            if (_creatingPartySessionTask != null)
            {
                await _creatingPartySessionTask;
                return;
            }

            _creatingPartySessionTask = CreatePartySessionCoreAsync();
            try
            {
                await _creatingPartySessionTask;
            }
            finally
            {
                _creatingPartySessionTask = null;
            }
        }

        private async Task CreatePartySessionCoreAsync()
        {
            var opts = new SessionOptions
            {
                MaxPlayers = connectionData.MaxPartySlots,
                IsLocked = false,
                IsPrivate = true,
                PlayerProperties = BuildLocalPlayerProperties()
            }.WithRelayNetwork();

            for (int attempt = 0; ; attempt++)
            {
                // The UGS Multiplayer SDK calls NetworkManager.StartHost()
                // internally when creating a Relay-backed session. If a local
                // host is already running (started by AuthenticationSceneController
                // as a fallback), that call fails. Shut it down before each attempt.
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsListening)
                {
                    Debug.Log("[HostConnectionService] Shutting down local host before Relay party session creation...");
                    nm.Shutdown();

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    while (nm != null && nm.IsListening && sw.ElapsedMilliseconds < 5000)
                        await Task.Delay(100);

                    // Allow transport cleanup to settle.
                    await Task.Delay(200);
                }

                try
                {
                    _partySession = await MultiplayerService.Instance.CreateSessionAsync(opts);
                    connectionData.IsHost = true;

                    // Give the new session a grace period before the refresh loop
                    // tries RefreshAsync() on it — avoids immediate "stale" errors
                    // that would null the session and trigger another recreation.
                    _refreshTimer = -(refreshIntervalSeconds);

                    Debug.Log($"[HostConnectionService] Created party session {_partySession.Id}");

                    // NOTE: We intentionally do NOT reload Menu_Main as a network
                    // scene here. Reloading caused a visible "big load time" and
                    // broke in-menu state when a client joined (ScriptableList
                    // OnSceneLoaded wipes PartyMembers on LoadSceneMode.Single).
                    // The client's post-accept flow stays on its local Menu_Main;
                    // existing NetworkObjects replicate via the normal connection-
                    // approval spawn sync without any scene handshake.
                    return;
                }
                catch (Exception e) when (attempt < HOST_CONFLICT_MAX_RETRIES && IsHostConflictException(e))
                {
                    // A local host was started by another system (e.g.
                    // AuthenticationSceneController) during Relay allocation.
                    // The next iteration's pre-check will shut it down.
                    Debug.LogWarning($"[HostConnectionService] Host conflict during Relay session creation — retry {attempt + 1}/{HOST_CONFLICT_MAX_RETRIES}");
                }
                catch (Exception e) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(e))
                {
                    int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                    Debug.LogWarning($"[HostConnectionService] Rate limited creating party session — retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                    await Task.Delay(delay);
                }
            }
        }

        /// <summary>
        /// Pushes the local player's current party count/max + active match name to
        /// the presence lobby player properties, so remote clients can render this
        /// player's row with the right status text ("IN LOBBY X/4", "LOBBY FULL",
        /// "IN A MATCH — {gameMode}", etc).
        /// Called from within <see cref="RefreshAsync"/> while _lobbyBusy is held —
        /// safe to call SaveCurrentPlayerData from here.
        /// No-op when nothing has changed since the last publish.
        /// </summary>
        private async Task PublishPartyStateIfChangedAsync()
        {
            if (_presenceLobby == null) return;

            int currentCount = connectionData.PartyMembers != null ? connectionData.PartyMembers.Count : 0;
            string currentMatch = ResolveCurrentMatchName();

            if (currentCount == _publishedPartyCount && currentMatch == _publishedMatchName) return;

            try
            {
                _presenceLobby.CurrentPlayer.SetProperty(PARTY_COUNT_KEY,
                    new PlayerProperty(currentCount.ToString(), VisibilityPropertyOptions.Public));
                _presenceLobby.CurrentPlayer.SetProperty(PARTY_MAX_KEY,
                    new PlayerProperty(connectionData.MaxPartySlots.ToString(), VisibilityPropertyOptions.Public));
                _presenceLobby.CurrentPlayer.SetProperty(MATCH_NAME_KEY,
                    new PlayerProperty(currentMatch ?? string.Empty, VisibilityPropertyOptions.Public));

                await SaveWithRetryAsync();
                _publishedPartyCount = currentCount;
                _publishedMatchName = currentMatch;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] PublishPartyState error: {e.Message}");
            }
        }

        /// <summary>
        /// Returns the friendly game-mode name when the local player is in an
        /// active multiplayer match, or empty string when on the menu / not in a
        /// multiplayer game. Used as the value for <see cref="MATCH_NAME_KEY"/>.
        /// </summary>
        private string ResolveCurrentMatchName()
        {
            if (_gameData == null) return string.Empty;
            if (IsOnMenuScene()) return string.Empty;
            if (!_gameData.IsMultiplayerMode) return string.Empty;
            return _gameData.GameMode.ToString();
        }

        /// <summary>
        /// Removes the outgoing invite to <paramref name="playerId"/> (if any) from
        /// the local map, notifies UI subscribers via <see cref="OutgoingInviteCleared"/>,
        /// and re-publishes the composite invite_payloads property so receivers stop
        /// seeing the stale entry. No-op when the player has no outstanding invite.
        /// Tolerates being called from inside RefreshAsync (already holds _lobbyBusy)
        /// or from outside (claims the mutex itself).
        /// </summary>
        /// <param name="reason">Short tag for diagnostic logging (e.g. "party-join",
        /// "presence-leave", "timeout", "manual"). Helps trace which path cleared
        /// the invite when debugging.</param>
        private async Task ClearOutgoingInviteIfPresentAsync(string playerId, string reason)
        {
            if (_presenceLobby == null) return;
            if (string.IsNullOrEmpty(playerId)) return;
            if (!_outgoingInvites.ContainsKey(playerId)) return;

            // Remove locally first so the re-serialization below excludes this entry
            // and a concurrent SendInviteAsync for the same target proceeds normally.
            _outgoingInvites.Remove(playerId);

            DebugExtensions.LogColored(
                $"[INVITE-SEND] Clearing invite for '{playerId}' (reason: {reason})",
                Color.green);

            // Fire the UI event before the network round-trip so the row reverts
            // to ONLINE immediately rather than waiting for the save to confirm.
            OutgoingInviteCleared?.Invoke(playerId);

            bool ownsLobby = !_lobbyBusy;
            if (ownsLobby) _lobbyBusy = true;
            try
            {
                PublishInvitePayloadsToCurrentPlayer();
                await SaveWithRetryAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] ClearOutgoingInvite error: {e.Message}");
            }
            finally
            {
                if (ownsLobby) _lobbyBusy = false;
            }
        }

        /// <summary>
        /// Saves current player data with retry on UGS rate-limit (HTTP 429).
        /// The Lobby service rate-limits at ~1 request per 1.5s. If a refresh
        /// just ran, the save can hit 429. Retries up to 3 times with 2s backoff.
        /// </summary>
        private async Task SaveWithRetryAsync()
        {
            const int maxRetries = 3;
            const int retryDelayMs = 2000;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await _presenceLobby.SaveCurrentPlayerDataAsync();

                    // Post-save refresh: sync the SDK's cached lobby state with the
                    // server immediately after the save. This reduces the window where
                    // incoming WebSocket lobby-change deltas reference stale player
                    // indices, which triggers ArgumentOutOfRangeException in the SDK's
                    // internal LobbyPatcher.
                    try { await _presenceLobby.RefreshAsync(); }
                    catch { /* best-effort — polling corrects state on next cycle */ }

                    return;
                }
                catch (Exception e) when (attempt < maxRetries &&
                    (e.Message.Contains("Too Many Requests") ||
                     e.Message.Contains("Index was out of range")))
                {
                    Debug.LogWarning($"[HostConnectionService] SaveCurrentPlayerData failed ({e.Message}) — retry {attempt + 1}/{maxRetries} in {retryDelayMs}ms");
                    await Task.Delay(retryDelayMs);

                    // Re-sync the SDK's cached player list before retrying.
                    try { await _presenceLobby.RefreshAsync(); }
                    catch { /* best-effort refresh */ }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Waits for <see cref="PlayerDataService.IsInitialized"/> (cloud
        /// profile merge complete) or for the given timeout, whichever comes
        /// first. No-ops if the service is null or already initialized.
        /// </summary>
        private async Task WaitForProfileInitAsync(int timeoutMs)
        {
            if (playerDataService == null) return;
            if (playerDataService.IsInitialized) return;

            int elapsed = 0;
            const int stepMs = 100;
            while (!playerDataService.IsInitialized && elapsed < timeoutMs)
            {
                await Task.Delay(stepMs);
                elapsed += stepMs;
            }

            if (!playerDataService.IsInitialized)
                Debug.LogWarning(
                    $"[HostConnectionService] PlayerDataService.IsInitialized " +
                    $"still false after {timeoutMs}ms — proceeding with local " +
                    $"default identity; profile-change republish will correct it.");
        }

        private void SyncLocalIdentity()
        {
            connectionData.LocalPlayerId = AuthData.PlayerId;

            if (playerDataService?.CurrentProfile != null)
            {
                connectionData.LocalDisplayName = playerDataService.CurrentProfile.displayName;
                connectionData.LocalAvatarId = playerDataService.CurrentProfile.avatarId;
            }

            // Fallback chain so LocalDisplayName is NEVER empty when used to
            // construct invite payloads or presence properties. Without this,
            // an invite sent before the cloud profile resolves would reach the
            // recipient with an empty host name and avatar id — causing the
            // receiver's party slot to render blank (FriendInfoSlot disables
            // the text label and avatar image when the string is empty).
            if (string.IsNullOrEmpty(connectionData.LocalDisplayName))
            {
                try
                {
                    var ugsName = Unity.Services.Authentication.AuthenticationService.Instance?.PlayerName;
                    if (!string.IsNullOrEmpty(ugsName))
                    {
                        int hashIndex = ugsName.LastIndexOf('#');
                        connectionData.LocalDisplayName = hashIndex > 0
                            ? ugsName.Substring(0, hashIndex)
                            : ugsName;
                    }
                }
                catch { /* best-effort — Authentication may not be initialized */ }
            }

            if (string.IsNullOrEmpty(connectionData.LocalDisplayName))
                connectionData.LocalDisplayName = "Pilot";
        }

        /// <summary>
        /// Tracks whether we've already wired the profile-change subscription so
        /// repeated calls (Start + HandleSignedInEvent) don't stack handlers.
        /// </summary>
        private bool _profileSubscribed;

        /// <summary>
        /// Subscribes to <see cref="PlayerDataService.OnProfileChanged"/> once.
        /// Called from Start and HandleSignedInEvent. Ensures that when the
        /// cloud profile resolves (or the user edits their name/avatar), the
        /// presence lobby and party session both see the updated values.
        /// </summary>
        private void SubscribeToProfileChanges()
        {
            if (_profileSubscribed || playerDataService == null) return;
            playerDataService.OnProfileChanged += HandleProfileChanged;
            _profileSubscribed = true;
        }

        private void UnsubscribeFromProfileChanges()
        {
            if (!_profileSubscribed || playerDataService == null) return;
            playerDataService.OnProfileChanged -= HandleProfileChanged;
            _profileSubscribed = false;
        }

        private void HandleProfileChanged(PlayerProfileData profile)
        {
            if (profile == null) return;

            SyncLocalIdentity();

            // Republish identity to the presence lobby so remote players see
            // the correct display name and avatar instead of the bootstrap
            // "Pilot" fallback. Fire-and-forget — the refresh loop will recover
            // from any transient error.
            RepublishLocalIdentityAsync().Forget();
        }

        /// <summary>
        /// Writes the current LocalDisplayName + LocalAvatarId into the presence
        /// lobby's player properties so remote clients see the up-to-date values.
        /// Wrapped with the <see cref="_lobbyBusy"/> mutex to avoid racing the
        /// refresh loop or in-flight invite sends.
        /// </summary>
        private async UniTaskVoid RepublishLocalIdentityAsync()
        {
            if (_presenceLobby == null) return;

            while (_lobbyBusy)
                await Task.Yield();
            _lobbyBusy = true;
            try
            {
                await _presenceLobby.RefreshAsync();
                _presenceLobby.CurrentPlayer.SetProperty(DISPLAY_NAME_KEY,
                    new PlayerProperty(connectionData.LocalDisplayName ?? "Pilot",
                        VisibilityPropertyOptions.Public));
                _presenceLobby.CurrentPlayer.SetProperty(AVATAR_ID_KEY,
                    new PlayerProperty(connectionData.LocalAvatarId.ToString(),
                        VisibilityPropertyOptions.Public));
                await SaveWithRetryAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] RepublishLocalIdentity error: {e.Message}");
            }
            finally
            {
                _lobbyBusy = false;
            }
        }

        /// <summary>
        /// Subscribes once to <see cref="GameDataSO.OnLaunchGame"/> so we can push
        /// <see cref="MATCH_NAME_KEY"/> presence the moment the local player launches
        /// a multiplayer game. Without this, the refresh loop (which suspends
        /// outside Menu_Main) would never publish the new match name and remote
        /// players would not see "IN A MATCH — {gameMode}".
        /// </summary>
        private void SubscribeToGameLaunch()
        {
            if (_gameLaunchSubscribed || _gameData == null || _gameData.OnLaunchGame == null) return;
            _gameData.OnLaunchGame.OnRaised += HandleGameLaunch;
            _gameLaunchSubscribed = true;
        }

        private void UnsubscribeFromGameLaunch()
        {
            if (!_gameLaunchSubscribed || _gameData == null || _gameData.OnLaunchGame == null) return;
            _gameData.OnLaunchGame.OnRaised -= HandleGameLaunch;
            _gameLaunchSubscribed = false;
        }

        private void HandleGameLaunch() => PublishPresenceImmediateAsync().Forget();

        /// <summary>
        /// One-shot presence publish independent of the refresh interval. Used at
        /// game-launch and on Menu_Main reload so remote players see the local
        /// player's match-name change without waiting for the next polling tick
        /// (and to handle the case where the refresh loop is suspended inside a
        /// game scene). Honors the <see cref="_lobbyBusy"/> mutex to stay
        /// serialized with refresh / invite-send / leave operations.
        /// </summary>
        private async UniTaskVoid PublishPresenceImmediateAsync()
        {
            if (_presenceLobby == null) return;

            while (_lobbyBusy)
                await Task.Yield();
            _lobbyBusy = true;
            try
            {
                await PublishPartyStateIfChangedAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostConnectionService] PublishPresenceImmediate error: {e.Message}");
            }
            finally
            {
                _lobbyBusy = false;
            }
        }

        private Dictionary<string, PlayerProperty> BuildLocalPlayerProperties()
        {
            int partyCount = connectionData.PartyMembers != null ? connectionData.PartyMembers.Count : 0;
            int partyMax = connectionData.MaxPartySlots;

            // 8 properties total — UGS lobbies cap player.data at 10. The composite
            // INVITE_PAYLOADS_KEY holds an unbounded number of outstanding invites
            // in a single property, so the cap is no longer a constraint on how
            // many people the host can invite at once.
            return new Dictionary<string, PlayerProperty>
            {
                { DISPLAY_NAME_KEY,    new PlayerProperty(connectionData.LocalDisplayName ?? "Pilot", VisibilityPropertyOptions.Public) },
                { AVATAR_ID_KEY,       new PlayerProperty(connectionData.LocalAvatarId.ToString(),    VisibilityPropertyOptions.Public) },
                { PARTY_COUNT_KEY,     new PlayerProperty(partyCount.ToString(), VisibilityPropertyOptions.Public) },
                { PARTY_MAX_KEY,       new PlayerProperty(partyMax.ToString(),   VisibilityPropertyOptions.Public) },
                { MATCH_NAME_KEY,      new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { JOINED_PARTY_KEY,    new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { INVITE_PAYLOADS_KEY, new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { ACCEPTED_INVITE_KEY, new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
            };
        }

        private bool IsAuthSignedInAndHasId()
        {
            if (AuthData == null) return false;

            bool signedIn =
                AuthData.IsSignedIn ||
                AuthData.State == AuthenticationData.AuthState.SignedIn;

            return signedIn && !string.IsNullOrEmpty(AuthData.PlayerId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Lobby SDK Log Filter
        // ─────────────────────────────────────────────────────────────────────

        private void InstallLobbyLogFilter()
        {
            _originalLogHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = new LobbyPatcherLogFilter(_originalLogHandler);
        }

        private void UninstallLobbyLogFilter()
        {
            if (_originalLogHandler != null && Debug.unityLogger.logHandler is LobbyPatcherLogFilter)
                Debug.unityLogger.logHandler = _originalLogHandler;
            _originalLogHandler = null;
        }

        /// <summary>
        /// Suppresses the known UGS SDK <see cref="ArgumentOutOfRangeException"/> thrown
        /// by <c>LobbyPatcher.ApplyPatchesToLobby</c> when a WebSocket lobby-change delta
        /// references a player index that is stale in the local cache.
        ///
        /// This is a race condition in the SDK (com.unity.services.multiplayer): the server
        /// computes a change delta against the current lobby state, but by the time the
        /// client receives it, a player may have joined or left, shifting indices. The SDK
        /// catches this internally at <c>LobbyChannel.HandleLobbyChanges</c> and logs it,
        /// but the lobby self-corrects on the next <see cref="ISession.RefreshAsync"/> poll.
        ///
        /// This filter only suppresses that specific harmless error — all other log messages
        /// pass through unmodified.
        /// </summary>
        private class LobbyPatcherLogFilter : ILogHandler
        {
            private readonly ILogHandler _inner;

            public LobbyPatcherLogFilter(ILogHandler inner) => _inner = inner;

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                if (exception is ArgumentOutOfRangeException &&
                    exception.StackTrace?.Contains("LobbyPatcher") == true)
                    return;

                _inner.LogException(exception, context);
            }

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                // The SDK funnels its caught exception through Logger.Log(LogType, string, object)
                // which Unity routes here as LogFormat(Error, null, "<concatenated>", null/{}). The
                // marker substrings are in `format`, not in `args`. Earlier filters that only
                // checked args[0] missed this and let the noise through.
                if (logType is LogType.Error or LogType.Exception or LogType.Warning)
                {
                    if (ContainsLobbyPatcherIndexError(format)) return;
                    if (args != null)
                    {
                        for (int i = 0; i < args.Length; i++)
                            if (ContainsLobbyPatcherIndexError(args[i]?.ToString())) return;
                    }
                }

                _inner.LogFormat(logType, context, format, args);
            }

            private static bool ContainsLobbyPatcherIndexError(string s)
            {
                if (string.IsNullOrEmpty(s)) return false;
                return s.Contains("LobbyPatcher") && s.Contains("Index was out of range");
            }
        }
    }
}
