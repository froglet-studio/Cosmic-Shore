// ─────────────────────────────────────────────────────────────────────────────
// PartySessionService.cs
// Owns the UGS Relay-backed party session lifecycle.
//
// WHY this class exists:
//   Before extraction, all party session state (_partySession, _partySessionCreatedAt)
//   and the session create/join logic lived in HostConnectionService alongside
//   lobby code, member-sync code, invite code, and refresh scheduling.  Extracting
//   the session lifecycle here gives it a single, documentable home and makes
//   every session state change observable through ActiveSession and
//   CreatedAtUnscaledTime.
//
// KEY CONSTRAINT: this service does NOT touch NetworkManager.
//   Netcode startup/shutdown (NM.StartHost(), NM.Shutdown()) is
//   HostConnectionService's responsibility for Phases 9-10 and will move to
//   INetworkTransitionService in Phase 11.  This service only manages the UGS
//   session object returned by MultiplayerService.Instance.
//
// RETRY POLICY:
//   CreateAsync retries on host-conflict (happens when the local NM is still
//   shutting down) and rate-limit (HTTP 429) errors with exponential back-off.
//   JoinByIdAsync retries transient errors (rate-limit / SDK SessionException NRE /
//   lobby-events 23006) - two clients accepting the same host invite can collide on
//   the host's session state. Non-transient join errors propagate to the caller
//   (AcceptInviteAsync), which logs and rethrows them for fail-fast recovery.
//
// LIFETIME:
//   Pure C# - no MonoBehaviour.  Instantiated as a field on
//   HostConnectionService for Phases 9-11.  Phase 12 registers it in Reflex DI.
//
// THREAD SAFETY:
//   Main-thread only.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages the UGS Relay-backed party session: create, join, leave, refresh.
    ///
    /// <para>
    /// Owns the <see cref="ActiveSession"/> reference and the
    /// <see cref="CreatedAtUnscaledTime"/> timestamp used to enforce the
    /// post-creation grace period.  All NetworkManager lifecycle operations
    /// (Shutdown, StartHost) remain in the caller for Phase 9 and will move to
    /// <see cref="NetworkTransitionService"/> in Phase 11.
    /// </para>
    ///
    /// Lifetime: pure C# - no MonoBehaviour.  Created as a field on
    /// <see cref="HostConnectionService"/>; will be DI-registered in Phase 12.
    /// Thread-safety: main-thread only.
    /// </summary>
    public sealed class PartySessionService : IPartySessionService
    {
        // ─────────────────────────────────────────────────────────────────────
        // Constants
        // ─────────────────────────────────────────────────────────────────────

        private const int RATE_LIMIT_MAX_RETRIES  = 3;
        private const int RATE_LIMIT_BASE_DELAY_MS = 2000;
        private const int HOST_CONFLICT_MAX_RETRIES = 2;
        private const int TRANSIENT_MAX_RETRIES   = 5;
        private const int TRANSIENT_BASE_DELAY_MS = 1000;

        // Session player-property keys. Identity only - see
        // BuildLocalPlayerProperties for why the six party-state keys that used
        // to live here were removed rather than left as unused declarations.
        private const string DISPLAY_NAME_KEY = PartyLobbyKeys.DisplayName;
        private const string AVATAR_ID_KEY    = PartyLobbyKeys.AvatarId;

        // ─────────────────────────────────────────────────────────────────────
        // Dependencies + state
        // ─────────────────────────────────────────────────────────────────────

        private readonly HostConnectionDataSO _connectionData;
        private readonly GameDataSO _gameData;

        /// <summary>
        /// UGS multiplayer service, resolved fresh at use time. Never cache
        /// <see cref="MultiplayerService.Instance"/> in the constructor - this
        /// service is a lazy DI singleton constructed during Bootstrap DI
        /// resolution, before <c>UnityServices.InitializeAsync()</c> completes,
        /// so a constructor-time read would pin null. See
        /// Docs/PartySystem/ARCHITECTURE.md (Investigation answers Q10).
        /// </summary>
        private IMultiplayerService _multiplayerService => MultiplayerService.Instance;

        // ─────────────────────────────────────────────────────────────────────
        // IPartySessionService - state properties
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// Backed by <c>GameDataSO.ActiveSession</c> - single source of truth
        /// for the active Relay session reference, shared with every other
        /// reader (HCS, MultiplayerSetup, MultiplayerMiniGameControllerBase,
        /// Player, etc.). See Docs/PartySystem/ARCHITECTURE.md locked design.
        /// </remarks>
        public ISession ActiveSession
        {
            get => _gameData.ActiveSession;
            private set => _gameData.ActiveSession = value;
        }

        /// <inheritdoc/>
        public float CreatedAtUnscaledTime { get; private set; }

        /// <inheritdoc/>
        public event Action<string> PlayerLeaving;

        /// <summary>
        /// Relay for the underlying <c>ISession.PlayerLeaving</c>.  Wired immediately
        /// after every <see cref="ActiveSession"/> assignment (create/join) and unwired
        /// in <see cref="ClearSession"/> - the single point that nulls the reference -
        /// so no handler outlives the session object it was attached to.
        /// </summary>
        private void OnSessionPlayerLeaving(string playerId)
        {
            MarkRosterDirty();
            PlayerLeaving?.Invoke(playerId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Push channel
        //
        // The presence lobby has had one of these since PRESENCE_SYNC_PLAN
        // commit 4; the party session had exactly ONE subscription
        // (PlayerLeaving) and no roster-dirty flag at all. So a party JOIN was
        // discoverable only by RefreshPartyMembersAsync on a poll tick - and
        // that read is voided ~32% of the time by the SDK stale-index defect
        // (Docs/PresenceSystem/BUGS.md, MEASURED run 2). Two voided ticks plus
        // the 4s creation grace period is the reported "1/4 -> 2/4 took 15-20
        // seconds".
        //
        // A push tick performs NO network read: the SDK has already patched
        // ISession.Players in memory before it raises. That is the decisive
        // property - a path with no read cannot be voided by a read fault. It is
        // the same bargain TODO-P9 already banked on the presence side.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Set by any push handler; drained by
        /// <see cref="TryConsumeRosterDirty"/> on the main thread.
        ///
        /// <para>
        /// <c>int</c> + <see cref="Interlocked"/> rather than <c>bool</c>: the
        /// UGS SDK gives no guarantee about which thread it dispatches session
        /// events on. Every handler therefore does exactly one thing - set this
        /// flag. No Unity API, no SOAP raise, no logging. Per
        /// <c>Docs/THREADING.md</c> a SOAP <c>Raise()</c> invokes its listeners
        /// INLINE, so raising from a push handler would run FriendsListPanel's
        /// Instantiate/Destroy off the main thread and throw
        /// <c>EnsureRunningOnMainThread</c>.
        /// </para>
        /// </summary>
        private int _rosterDirty;

        /// <inheritdoc/>
        public bool TryConsumeRosterDirty() => Interlocked.Exchange(ref _rosterDirty, 0) == 1;

        private void MarkRosterDirty() => Interlocked.Exchange(ref _rosterDirty, 1);

        /// <summary>
        /// Subscribes the push handlers to a session we have just taken ownership
        /// of. Called at EVERY point <see cref="ActiveSession"/> is assigned -
        /// create and join - because both produce a session that pushes to us.
        ///
        /// <para>
        /// <c>PlayerLeaving</c> keeps its dedicated typed relay
        /// (<see cref="OnSessionPlayerLeaving"/>) because the host's
        /// invite-clearing and reconcile path needs the departing id, not just
        /// "something moved"; it marks the flag too.
        /// </para>
        ///
        /// <para>
        /// <c>RemovedFromSession</c> and <c>Deleted</c> are deliberately NOT
        /// subscribed here. Losing the party session is already handled, on the
        /// signal that actually matters for gameplay, by
        /// <c>MultiplayerSetup.OnClientDisconnect</c> ->
        /// <c>PartyInviteController.HandleHostLossAsync</c>. Adding a second
        /// recovery trigger on a different signal would race it, and this commit
        /// is about the join-discovery gap.
        /// </para>
        /// </summary>
        private void WireSessionEvents(ISession session)
        {
            if (session == null) return;

            session.PlayerLeaving           += OnSessionPlayerLeaving;
            session.PlayerJoined            += OnPushPlayerJoined;
            session.PlayerHasLeft           += OnPushPlayerHasLeft;
            session.PlayerPropertiesChanged += OnPushPlayerPropertiesChanged;
            session.Changed                 += OnPushChanged;
        }

        /// <summary>
        /// Mirror of <see cref="WireSessionEvents"/>, called from
        /// <see cref="ClearSession"/> - the single point that nulls the
        /// reference. Missing one of these would leak a subscription onto a dead
        /// session and keep marking the roster dirty from a party we already left.
        /// </summary>
        private void UnwireSessionEvents(ISession session)
        {
            if (session == null) return;

            session.PlayerLeaving           -= OnSessionPlayerLeaving;
            session.PlayerJoined            -= OnPushPlayerJoined;
            session.PlayerHasLeft           -= OnPushPlayerHasLeft;
            session.PlayerPropertiesChanged -= OnPushPlayerPropertiesChanged;
            session.Changed                 -= OnPushChanged;
        }

        // Named methods, not lambdas, so the -= above actually removes them - a
        // lambda would create a new delegate instance and unsubscribe nothing.
        private void OnPushPlayerJoined(string _)     => MarkRosterDirty();
        private void OnPushPlayerHasLeft(string _)    => MarkRosterDirty();
        private void OnPushPlayerPropertiesChanged()  => MarkRosterDirty();
        private void OnPushChanged()                  => MarkRosterDirty();

        // ─────────────────────────────────────────────────────────────────────
        // Construction
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the party session service.
        /// </summary>
        /// <param name="connectionData">
        /// Shared party state container.  Read for player identity (display name,
        /// avatar id) when building player properties for session create/join.
        /// </param>
        /// <param name="gameData">
        /// Shared game-data SO. Backs <see cref="ActiveSession"/> - every reader
        /// of the active session reference (this service, HCS, game controllers,
        /// MultiplayerSetup) goes through the same field.
        /// </param>
        public PartySessionService(HostConnectionDataSO connectionData, GameDataSO gameData)
        {
            _connectionData = connectionData;
            _gameData = gameData;
        }

        // ─────────────────────────────────────────────────────────────────────
        // IPartySessionService - session lifecycle
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new private Relay-backed party session and sets
        /// <see cref="ActiveSession"/>.  No-op if a session is already active.
        ///
        /// <para>
        /// Retries on host-conflict (NM still shutting down) and rate-limit (HTTP 429)
        /// errors with exponential back-off.  Caller is responsible for shutting
        /// down the local NetworkManager BEFORE calling this method.
        /// </para>
        /// </summary>
        /// <param name="maxPlayers">Maximum simultaneous players.</param>
        public async UniTask CreateAsync(int maxPlayers)
        {
            if (ActiveSession != null) return;

            var opts = new SessionOptions
            {
                MaxPlayers       = maxPlayers,
                IsLocked         = false,
                IsPrivate        = true,
                PlayerProperties = BuildLocalPlayerProperties(),
            }.WithRelayNetwork();

            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    ActiveSession          = await _multiplayerService.CreateSessionAsync(opts).AsMainThread();
                    CreatedAtUnscaledTime  = Time.unscaledTime;
                    WireSessionEvents(ActiveSession);
                    Debug.Log($"[PartySessionService] Created party session {ActiveSession.Id} (maxPlayers={maxPlayers}).");
                    return;
                }
                catch (Exception e) when (attempt < HOST_CONFLICT_MAX_RETRIES && IsHostConflictException(e))
                {
                    CosmicShore.Utility.CSDebug.Log($"[PartySessionService] Host conflict - retry {attempt + 1}/{HOST_CONFLICT_MAX_RETRIES}");
                }
                catch (Exception e) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(e))
                {
                    int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                    CosmicShore.Utility.CSDebug.Log($"[PartySessionService] Rate limited - retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                    await UniTask.Delay(delay);
                }
                catch (Exception e) when (attempt < TRANSIENT_MAX_RETRIES && IsTransientSessionException(e))
                {
                    int delay = TRANSIENT_BASE_DELAY_MS * (1 << attempt);
                    CosmicShore.Utility.CSDebug.Log($"[PartySessionService] Transient session error - retry {attempt + 1}/{TRANSIENT_MAX_RETRIES} in {delay}ms ({e.GetType().Name}): {e}");
                    CosmicShore.Utility.CSDebug.Log($"[PartySessionService] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
                    await UniTask.Delay(delay);
                }
            }
        }

        /// <summary>
        /// Joins an existing party session by its UGS session ID and sets
        /// <see cref="ActiveSession"/>.
        ///
        /// <para>
        /// The caller must ensure <paramref name="sessionId"/> is the real (non-PENDING)
        /// Relay session id before calling - use
        /// <see cref="AcceptanceSignalService.WaitForRealSessionIdAsync"/> to obtain it.
        /// </para>
        /// </summary>
        /// <param name="sessionId">
        /// The UGS Relay session id published by the host after they call
        /// <see cref="CreateAsync"/>.
        /// </param>
        public async UniTask JoinByIdAsync(string sessionId)
        {
            var opts = new JoinSessionOptions { PlayerProperties = BuildLocalPlayerProperties() };

            // Retry transient join failures (HTTP 429 / SDK SessionException NRE /
            // lobby-events 23006). Two clients accepting the same host's invite near-
            // simultaneously can collide on the host's session state, so one join throws
            // a transient error before the NM client even starts. Mirrors CreateAsync's
            // retry loop + classifiers. Non-transient errors propagate to the caller
            // (HostConnectionService.AcceptInviteAsync), which logs and rethrows so
            // PartyInviteController fails fast. See Docs/PartySystem/ARCHITECTURE.md (Q5).
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    ActiveSession = await _multiplayerService.JoinSessionByIdAsync(sessionId, opts).AsMainThread();
                    WireSessionEvents(ActiveSession);

                    // A join is itself a roster change we must not wait for a poll
                    // to notice: the host is already in ISession.Players the moment
                    // this returns.
                    MarkRosterDirty();
                    Debug.Log($"[PartySessionService] Joined party session {ActiveSession.Id}.");
                    return;
                }
                catch (Exception e) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(e))
                {
                    int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                    CosmicShore.Utility.CSDebug.Log($"[PartySessionService] Join rate limited - retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                    await UniTask.Delay(delay);
                }
                catch (Exception e) when (attempt < TRANSIENT_MAX_RETRIES && IsTransientSessionException(e))
                {
                    int delay = TRANSIENT_BASE_DELAY_MS * (1 << attempt);
                    CosmicShore.Utility.CSDebug.Log($"[PartySessionService] Join transient error - retry {attempt + 1}/{TRANSIENT_MAX_RETRIES} in {delay}ms ({e.GetType().Name}): {e.Message}");
                    CosmicShore.Utility.CSDebug.Log($"[PartySessionService] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
                    await UniTask.Delay(delay);
                }
            }
        }

        /// <summary>
        /// Leaves the active session (deletes if host, leaves if client) and clears
        /// <see cref="ActiveSession"/>.  Safe to call when no session is active.
        /// </summary>
        public async UniTask LeaveAsync()
        {
            if (ActiveSession == null) return;
            var session = ActiveSession;
            ClearSession();
            try
            {
                if (session.IsHost)
                    await session.AsHost().DeleteAsync().AsMainThread();
                else
                    await session.LeaveAsync().AsMainThread();
                Debug.Log($"[PartySessionService] Left party session {session.Id}.");
            }
            catch (Exception e)
            {
                CosmicShore.Utility.CSDebug.Log($"[PartySessionService] Leave error (session already gone?): {e.Message}");
                CosmicShore.Utility.CSDebug.Log($"[PartySessionService] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
            }
        }

        /// <summary>
        /// Refreshes the active session's player list from the UGS backend.
        /// Throws on SDK errors - caller is responsible for error handling and
        /// grace-period enforcement.
        /// </summary>
        public async UniTask RefreshAsync()
        {
            if (ActiveSession == null) return;
            await ActiveSession.RefreshAsync().AsMainThread();
        }

        /// <inheritdoc/>
        public async UniTask UpdateLocalPlayerPropertiesAsync(string displayName, int avatarId)
        {
            var session = ActiveSession;
            if (session == null) return;

            try
            {
                session.CurrentPlayer.SetProperty(DISPLAY_NAME_KEY,
                    new PlayerProperty(string.IsNullOrEmpty(displayName) ? "Pilot" : displayName,
                        VisibilityPropertyOptions.Public));
                session.CurrentPlayer.SetProperty(AVATAR_ID_KEY,
                    new PlayerProperty(avatarId.ToString(), VisibilityPropertyOptions.Public));
                await session.SaveCurrentPlayerDataAsync().AsMainThread();
                Debug.Log($"[PartySessionService] Local player properties updated (displayName='{displayName}').");
            }
            catch (Exception e)
            {
                // Non-fatal: peers keep the stale name until the next session
                // (re)join. Never throw into the profile-change event chain.
                Debug.LogWarning(
                    $"[PartySessionService] UpdateLocalPlayerProperties failed ({e.GetType().Name}): {e.Message}");
            }
        }

        /// <summary>
        /// Synchronously clears <see cref="ActiveSession"/> and
        /// <see cref="CreatedAtUnscaledTime"/> without calling the UGS SDK.
        ///
        /// <para>
        /// Use when the session reference should be discarded without a graceful
        /// leave (e.g., game→menu transition stale-session clear, or after a
        /// non-rate-limit refresh failure when retaining the session would trigger
        /// duplicate creation that kicks the joining client).
        /// </para>
        /// </summary>
        public void ClearSession()
        {
            if (ActiveSession == null) return;
            Debug.Log($"[PartySessionService] Clearing session reference {ActiveSession.Id}.");
            UnwireSessionEvents(ActiveSession);
            ActiveSession         = null;
            CreatedAtUnscaledTime = 0f;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the player-property dictionary written to the UGS session on
        /// create/join.  Reflects the current player identity snapshot from
        /// <c>_connectionData</c>.
        /// </summary>
        private Dictionary<string, PlayerProperty> BuildLocalPlayerProperties()
        {
            // Identity ONLY.
            //
            // This dictionary is written to the RELAY session, and the relay
            // session's player properties have exactly one reader in the whole
            // codebase: PartyMemberService.ReadMemberData, which takes
            // displayName and avatarId. The six other keys this used to write -
            // partyCount, partyMax, matchName, joined_party, invite_payloads,
            // accepted_invite - are the PRESENCE LOBBY's vocabulary. Every
            // consumer of them (ReadOnlinePlayerData, TryFindIncomingInvite,
            // ScanPresenceForJoinedPartyMembers, AcceptanceSignalService) reads
            // from _lobbyService.ActiveLobby.Players, never from the session.
            // They were written here and read nowhere: four of them always
            // string.Empty.
            //
            // Dropping them is not just tidiness. Property writes are the
            // measured driver of the B1/B6 stale-index defect - the fault rate
            // concentrates in the startup write burst - which is why
            // "coalesce startup property writes" (TODO-P2) is the highest-value
            // item on the presence list. Session create and join each carried
            // six dead keys; now they carry none.
            //
            // If a future feature genuinely needs party state on the session, add
            // the key back WITH its reader in the same commit.
            return new Dictionary<string, PlayerProperty>
            {
                { DISPLAY_NAME_KEY, new PlayerProperty(string.IsNullOrEmpty(_connectionData.LocalDisplayName) ? "Pilot" : _connectionData.LocalDisplayName, VisibilityPropertyOptions.Public) },
                { AVATAR_ID_KEY,    new PlayerProperty(_connectionData.LocalAvatarId.ToString(), VisibilityPropertyOptions.Public) },
            };
        }

        /// <summary>
        /// True when the exception is a UGS rate-limit (429) response.
        /// Delegates to <see cref="UgsErrorClassifier.IsRateLimit"/>; the
        /// previous local version pattern-matched only the OUTER exception as a
        /// <c>RequestFailedException</c>, so a wrapped 429 never armed the
        /// retry in <c>CreateAsync</c> / <c>JoinByIdAsync</c>.
        /// </summary>
        private static bool IsRateLimitException(Exception e) =>
            UgsErrorClassifier.IsRateLimit(e);

        private static bool IsHostConflictException(Exception e) =>
            e.Message?.Contains("NetworkManager", StringComparison.OrdinalIgnoreCase) == true ||
            e.Message?.Contains("host", StringComparison.OrdinalIgnoreCase) == true;

        private static bool IsTransientSessionException(Exception e)
        {
            if (e is not SessionException) return false;

            // Walk the chain: UGS wraps the underlying failure, so the detail that
            // identifies it as transient is usually on an inner exception rather
            // than the SessionException we were handed.
            for (var current = e; current != null; current = current.InnerException)
            {
                // NRE-flavored transient (null ref inside UGS SDK on lobby events subscription)
                if (current is NullReferenceException) return true;

                // ANY UGS 5xx is transient by definition: the request was
                // well-formed and the service failed to serve it, so the only
                // sensible response is to try again. This is the general rule the
                // message patterns below are specific instances of.
                if (current is Unity.Services.Core.RequestFailedException rfe &&
                    rfe.ErrorCode >= 500 && rfe.ErrorCode < 600)
                    return true;

                var msg = current.Message ?? string.Empty;
                if (msg.IndexOf("Object reference",        StringComparison.OrdinalIgnoreCase) >= 0) return true;

                // Lobby-events / Wire-subscription transients (error code 23006).
                // These originate in LobbyHandler.SubscribeToLobbyEventsAsync after the
                // lobby is created server-side, so retrying CreateSessionAsync is safe.
                if (msg.IndexOf("lobby service for events", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (msg.IndexOf("Error Code[23006]",        StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (msg.IndexOf("valid Lobby ID",           StringComparison.OrdinalIgnoreCase) >= 0) return true;

                // Relay allocation failure. Observed in the wild as
                //   SessionException: Failed to create allocation
                // wrapping "Internal Server Error: allocation call failure" from
                // RelayHandler.CreateAllocationAsync. Nothing about the request is
                // wrong - Relay simply failed to hand out an allocation that
                // moment - and a retry a second later normally succeeds. Before
                // this matched, the exception escaped CreateAsync, then
                // EnsurePartySessionAsync, then EnsureInitializedAsync (both of
                // which have finally-but-no-catch), and finally the async void
                // HandleSignedInEvent - so the eager session was never created,
                // OnHostConnectionEstablished never fired, and the boot sat on the
                // loading splash forever.
                if (msg.IndexOf("Failed to create allocation", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (msg.IndexOf("allocation call failure",     StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (msg.IndexOf("Internal Server Error",       StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }
    }
}
