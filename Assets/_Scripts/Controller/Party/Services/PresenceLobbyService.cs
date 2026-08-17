// ─────────────────────────────────────────────────────────────────────────────
// PresenceLobbyService.cs
// Manages the UGS lobby-only session used for player discovery and invite
// property exchange.
//
// WHY this class exists:
//   Before extraction, HostConnectionService owned the full lifecycle of the
//   presence lobby: the _presenceLobby field, JoinPresenceLobbyAsync,
//   TryQueryAndJoinLobbyAsync, CreatePresenceLobbyAsync, DeleteOwnLobbyQuietly,
//   LeavePresenceLobbyAsync, and BuildLocalPlayerProperties.  All of these are
//   pure UGS session mechanics - none of them touch NetworkManager, invite
//   payloads, or member lists.  Extracting them here means:
//     1. HostConnectionService can hold the session reference through the
//        interface (IPresenceLobbyService.ActiveLobby) without owning it.
//     2. The join/create/race-condition logic is testable in isolation.
//     3. The player-property dict format is defined in one place.
//
// KEY CONSTRAINT:
//   This service must NOT touch NetworkManager, Relay allocations, or party
//   session logic.  Those are IPartySessionService's domain.  The presence
//   lobby is a lobby-only UGS session (no Relay) that coexists with any NM.
//
// LIFETIME:
//   Pure C# - no MonoBehaviour.  Instantiated as a field on
//   HostConnectionService for Phases 7-11.  Phase 12 registers it in Reflex DI.
//
// THREAD SAFETY:
//   Main-thread only, with ONE deliberate exception: the ISession push handlers
//   (OnPush*) may be dispatched by the UGS SDK from an arbitrary thread. They do
//   nothing but Interlocked-set an int flag - no Unity API, no SOAP raise, no
//   logging - and HostConnectionService.Update() drains those flags on the main
//   thread. Do not add anything else to those handlers; see WireSessionEvents.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages the UGS lobby-only session used for player discovery and invite
    /// property exchange.  Handles the join-or-create flow (including the
    /// simultaneous-create race condition), graceful leave/delete, refresh, and
    /// property writes.  Does NOT touch NetworkManager or Relay.
    ///
    /// <para>
    /// The active session is exposed via <see cref="ActiveLobby"/>.  Call
    /// <see cref="JoinOrCreateAsync"/> once on auth sign-in.  Call
    /// <see cref="ForceReset"/> before attempting a reconnect (sets
    /// <see cref="ActiveLobby"/> to <c>null</c> so <see cref="JoinOrCreateAsync"/>
    /// will proceed rather than returning early).
    /// </para>
    ///
    /// Lifetime: pure C# - no MonoBehaviour.  Created as a field on
    /// <see cref="HostConnectionService"/>; will be DI-registered in Phase 12.
    /// Thread-safety: main-thread only, except the flag-only ISession push
    /// handlers - see the file header.
    /// </summary>
    public sealed class PresenceLobbyService : IPresenceLobbyService
    {
        // ─────────────────────────────────────────────────────────────────────
        // Constants - keys and tuning
        // ─────────────────────────────────────────────────────────────────────

        private const string PRESENCE_LOBBY_GAME_MODE = PartyLobbyKeys.PresenceLobbyGameMode;
        private const string DISPLAY_NAME_KEY         = PartyLobbyKeys.DisplayName;
        private const string AVATAR_ID_KEY            = PartyLobbyKeys.AvatarId;
        private const string PARTY_COUNT_KEY          = PartyLobbyKeys.PartyCount;
        private const string PARTY_MAX_KEY            = PartyLobbyKeys.PartyMax;
        private const string MATCH_NAME_KEY           = PartyLobbyKeys.MatchName;
        private const string INVITE_PAYLOADS_KEY      = PartyLobbyKeys.InvitePayloads;
        private const string JOINED_PARTY_KEY         = PartyLobbyKeys.JoinedParty;
        private const string ACCEPTED_INVITE_KEY      = PartyLobbyKeys.AcceptedInvite;

        /// <summary>
        /// After creating a lobby, wait this long before re-querying to detect
        /// a simultaneous-creation race (two clients both create a lobby at the
        /// same millisecond in MPPM or near-simultaneous launches).
        /// </summary>
        private const int LOBBY_RACE_SETTLE_MS   = 1500;
        private const int RATE_LIMIT_MAX_RETRIES = 3;
        private const int RATE_LIMIT_BASE_DELAY_MS = 2000;

        // ─────────────────────────────────────────────────────────────────────
        // Private state
        // ─────────────────────────────────────────────────────────────────────

        private readonly HostConnectionDataSO _connectionData;
        private readonly LobbyPropertyWriter  _propertyWriter;

        /// <summary>
        /// UGS multiplayer service, resolved fresh at use time. Never cache
        /// <see cref="MultiplayerService.Instance"/> in the constructor - this
        /// service is a lazy DI singleton constructed during Bootstrap DI
        /// resolution, before <c>UnityServices.InitializeAsync()</c> completes,
        /// so a constructor-time read would pin null. See
        /// Docs/PartySystem/ARCHITECTURE.md (Investigation answers Q10).
        /// </summary>
        private IMultiplayerService _multiplayerService => MultiplayerService.Instance;
        private ISession _activeLobby;
        private bool     _leaving;

        // Push-channel flags. Set from ISession callbacks (which may arrive off
        // the main thread), drained from Update(). int + Interlocked rather than
        // bool because the SDK gives no thread guarantee for its dispatch, and a
        // torn read here would silently drop a roster update.
        private int _rosterDirty;
        private int _membershipLost;

        /// <summary>
        /// Player ids UGS has told us are leaving or have left, via the
        /// <c>PlayerLeaving</c> / <c>PlayerHasLeft</c> pushes - which carry the
        /// departing player's id as their payload.
        ///
        /// <para>
        /// This is authoritative departure evidence, not an inference from a
        /// short read, so the consumer may evict immediately instead of waiting
        /// out the two-strike corroboration rule. Without consuming the id the
        /// only removal path was "absent from N consecutive reads", which turned
        /// an instant, explicit leave into a multi-second disappearance.
        /// </para>
        ///
        /// <para>
        /// Guarded by <see cref="_departedLock"/>: the SDK gives no thread
        /// guarantee for its callbacks, and unlike the plain int flags a HashSet
        /// cannot be updated atomically.
        /// </para>
        /// </summary>
        private readonly HashSet<string> _departedPlayerIds = new();
        private readonly object _departedLock = new();

        // ─────────────────────────────────────────────────────────────────────
        // Construction
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="connectionData">
        /// Source of local player identity fields used when building player
        /// properties (LocalDisplayName, LocalAvatarId, PartyMembers, etc.).
        /// </param>
        /// <param name="propertyWriter">
        /// Owns the lobby mutex and SaveWithRetry pattern; used by
        /// <see cref="SavePropertiesAsync"/> to write player properties safely.
        /// </param>
        public PresenceLobbyService(HostConnectionDataSO connectionData, LobbyPropertyWriter propertyWriter)
        {
            _connectionData = connectionData;
            _propertyWriter = propertyWriter;
        }

        // ─────────────────────────────────────────────────────────────────────
        // IPresenceLobbyService - public API
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public ISession ActiveLobby => _activeLobby;

        /// <inheritdoc/>
        public Func<IReadOnlyDictionary<string, string>> LivePropertySource { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// Three-step algorithm:
        /// <list type="number">
        ///   <item>Query UGS for an existing presence lobby.</item>
        ///   <item>If none, create one; then re-query after a settling delay to detect
        ///         a simultaneous creation by another client (MPPM / near-simultaneous
        ///         launch). If a rival lobby is found, delete own and merge into it.</item>
        ///   <item>On any exception, fall back to creating a new lobby.</item>
        /// </list>
        /// </remarks>
        public async UniTask JoinOrCreateAsync(int maxPlayers)
        {
            if (_activeLobby != null) return;

            Debug.Log("[PresenceLobbyService] JoinOrCreateAsync - joining presence lobby...");
            try
            {
                SetActiveLobby(await TryQueryAndJoinAsync(maxPlayers));

                if (_activeLobby == null)
                {
                    await CreateAsync(maxPlayers);

                    // A rival may have created a lobby at the same instant (MPPM or
                    // near-simultaneous launch).  After a short settle for UGS session
                    // indexing, converge on the canonical (smallest-id) lobby so both
                    // sides deterministically pick the SAME one instead of staying
                    // split.  A symmetric "join the first rival" merge could have both
                    // sides swap into each other's lobby and end up split again.
                    await UniTask.Delay(LOBBY_RACE_SETTLE_MS);
                    await ConvergeToCanonicalAsync(maxPlayers);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PresenceLobbyService] Join failed ({e.Message}) - creating new lobby as fallback.");
                LogNetDiag("JoinOrCreate", e);
                if (_activeLobby == null)
                    await CreateAsync(maxPlayers);
            }

            Debug.Log($"[PresenceLobbyService] JoinOrCreateAsync complete - lobby: {_activeLobby?.Id ?? "NULL"}");
        }

        /// <inheritdoc/>
        public async UniTask ConvergeToCanonicalAsync(int maxPlayers)
        {
            // Query the full presence-lobby set (rate-limit aware, like the join path).
            var queryOptions = new QuerySessionsOptions();
            queryOptions.FilterOptions.Add(
                new FilterOption(FilterField.StringIndex1, PRESENCE_LOBBY_GAME_MODE, FilterOperation.Equal));

            IList<ISessionInfo> sessions = null;
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    var results = await _multiplayerService.QuerySessionsAsync(queryOptions).AsMainThread();
                    sessions = results.Sessions;
                    break;
                }
                catch (Exception qe) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(qe))
                {
                    int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                    Debug.LogWarning($"[PresenceLobbyService] Rate limited during converge query - retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                    await UniTask.Delay(delay);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PresenceLobbyService] Converge query failed ({e.GetType().Name}): {e.Message}");
                    LogNetDiag("ConvergeQuery", e);
                    return;
                }
            }

            // Canonical id = smallest (ordinal) over the visible set ∪ our own lobby.
            // The smallest-id holder is the single point everyone migrates toward.
            string canonicalId = _activeLobby?.Id;
            if (sessions != null)
            {
                foreach (var s in sessions)
                {
                    if (string.IsNullOrEmpty(s.Id)) continue;
                    if (canonicalId == null || string.CompareOrdinal(s.Id, canonicalId) < 0)
                        canonicalId = s.Id;
                }
            }

            // Nothing to converge to, or we already hold the canonical lobby.
            if (canonicalId == null) return;
            if (_activeLobby != null && _activeLobby.Id == canonicalId) return;

            // Migrate down to the canonical lobby: join it FIRST so a failed join
            // never leaves us lobby-less, then release the one we were holding.
            try
            {
                var joined = await _multiplayerService.JoinSessionByIdAsync(
                    canonicalId,
                    new JoinSessionOptions { PlayerProperties = BuildLocalPlayerProperties() }).AsMainThread();

                await DeleteOwnLobbyQuietlyAsync();   // releases the previous _activeLobby
                SetActiveLobby(joined);
                Debug.Log($"[PresenceLobbyService] Converged to canonical presence lobby {joined.Id}.");
            }
            catch (Exception e)
            {
                // Canonical lobby may have died between query and join - keep our
                // current lobby; the next periodic converge retries.
                Debug.LogWarning($"[PresenceLobbyService] Converge join to {canonicalId} failed ({e.GetType().Name}): {e.Message}");
                LogNetDiag("ConvergeJoin", e);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Gracefully leaves (or deletes if host) the active lobby.  Exceptions
        /// from the UGS SDK are caught and swallowed so callers always reach the
        /// <c>finally</c> cleanup even on broken connections.
        /// </remarks>
        public async UniTask LeaveAsync()
        {
            if (_activeLobby == null || _leaving) return;
            _leaving = true;
            Debug.Log($"[PresenceLobbyService] LeaveAsync - lobby: {_activeLobby.Id}, IsHost: {_activeLobby.IsHost}");
            try
            {
                if (_activeLobby.IsHost)
                    await _activeLobby.AsHost().DeleteAsync().AsMainThread();
                else
                    await _activeLobby.LeaveAsync().AsMainThread();
            }
            catch (Exception e)
            {
                CSDebug.Log($"[PresenceLobbyService] Leave error (session may already be gone) ({e.GetType().Name}): {e}");
                LogNetDiag("Leave", e);
            }
            finally
            {
                SetActiveLobby(null);
                _leaving     = false;
            }
        }

        /// <inheritdoc/>
        public async UniTask RefreshAsync()
        {
            if (_activeLobby == null) return;
            await _activeLobby.RefreshAsync().AsMainThread();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Acquires the lobby mutex via <see cref="LobbyPropertyWriter.WriteAsync"/>;
        /// do NOT call while already holding the mutex (e.g. from inside
        /// <see cref="HostConnectionService"/>'s RefreshAsync cycle).  For in-mutex
        /// writes, set properties directly then call
        /// <see cref="LobbyPropertyWriter.SaveWithRetryAsync"/> explicitly.
        /// </remarks>
        public async UniTask SavePropertiesAsync(
            Dictionary<string, PlayerProperty> properties,
            string operationName)
        {
            if (_activeLobby == null || properties == null || properties.Count == 0) return;

            // Capture the lobby reference so the lambda doesn't re-evaluate the
            // property on each call - the session reference is stable during a write.
            var lobby = _activeLobby;
            await _propertyWriter.WriteAsync(
                lobby,
                () =>
                {
                    foreach (var kv in properties)
                        lobby.CurrentPlayer.SetProperty(kv.Key, kv.Value);
                },
                operationName);

            Debug.Log($"[PresenceLobbyService] SavePropertiesAsync({operationName}) - {properties.Count} props written.");
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Does NOT call the UGS SDK - just clears the internal reference so
        /// <see cref="JoinOrCreateAsync"/> will proceed rather than returning early.
        /// Call this when a broken connection is detected before triggering a
        /// reconnect via <see cref="JoinOrCreateAsync"/>.
        /// </remarks>
        public void ForceReset()
        {
            Debug.Log("[PresenceLobbyService] ForceReset - clearing active lobby reference for reconnect.");
            SetActiveLobby(null);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal - player property builder
        //
        // Kept internal (not on the interface) because it is also called from
        // HostConnectionService for party session joins.  Phase 9 (PartySessionService)
        // will absorb those call sites, at which point this method becomes private.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the standard player-property dictionary used when joining or
        /// creating UGS sessions (both presence lobby and party session).
        ///
        /// All 8 properties are always present so no key is missing on the first
        /// refresh; absent keys in UGS look the same as empty-value keys, which
        /// causes "invite_payloads not found" false negatives.
        /// </summary>
        internal Dictionary<string, PlayerProperty> BuildLocalPlayerProperties()
        {
            int partyCount = _connectionData.PartyMembers != null ? _connectionData.PartyMembers.Count : 0;
            int partyMax   = _connectionData.MaxPartySlots;

            var props = new Dictionary<string, PlayerProperty>
            {
                { DISPLAY_NAME_KEY,    new PlayerProperty(string.IsNullOrEmpty(_connectionData.LocalDisplayName) ? "Pilot" : _connectionData.LocalDisplayName, VisibilityPropertyOptions.Public) },
                { AVATAR_ID_KEY,       new PlayerProperty(_connectionData.LocalAvatarId.ToString(),    VisibilityPropertyOptions.Public) },
                { PARTY_COUNT_KEY,     new PlayerProperty(partyCount.ToString(), VisibilityPropertyOptions.Public) },
                { PARTY_MAX_KEY,       new PlayerProperty(partyMax.ToString(),   VisibilityPropertyOptions.Public) },
                { MATCH_NAME_KEY,      new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { JOINED_PARTY_KEY,    new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { INVITE_PAYLOADS_KEY, new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { ACCEPTED_INVITE_KEY, new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
            };

            // State-preserving rejoin (Docs/PresenceSystem/BUGS.md B4): a lobby
            // rejoin used to reset the stateful keys to empty, wiping in-flight
            // invites and a guest's joined_party advertisement - which is why
            // convergence had to be PAUSED while an invite was outstanding or a
            // party had formed, freezing lobby splits exactly when a 3rd player
            // was being invited. Overlaying live values here makes every rejoin
            // path (initial, reconnect, converge-migration) state-safe, so the
            // pause is gone. The source is HostConnectionService - still the
            // single writer of these values; this service only carries them.
            var live = LivePropertySource?.Invoke();
            if (live != null)
            {
                foreach (var kv in live)
                {
                    if (string.IsNullOrEmpty(kv.Key) || string.IsNullOrEmpty(kv.Value)) continue;
                    props[kv.Key] = new PlayerProperty(kv.Value, VisibilityPropertyOptions.Public);
                }
            }

            return props;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Queries the UGS session list for an existing presence lobby and joins the
        /// first available one.  Returns <c>null</c> if no lobby exists or all join
        /// attempts fail.
        /// </summary>
        private async UniTask<ISession> TryQueryAndJoinAsync(int maxPlayers)
        {
            var queryOptions = new QuerySessionsOptions();
            queryOptions.FilterOptions.Add(
                new FilterOption(FilterField.StringIndex1, PRESENCE_LOBBY_GAME_MODE, FilterOperation.Equal));

            IList<ISessionInfo> sessions = null;
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    var results = await _multiplayerService.QuerySessionsAsync(queryOptions).AsMainThread();
                    sessions = results.Sessions;
                    break;
                }
                catch (Exception qe) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(qe))
                {
                    int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                    Debug.LogWarning($"[PresenceLobbyService] Rate limited querying lobby - retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                    await UniTask.Delay(delay);
                }
            }

            if (sessions == null || sessions.Count == 0) return null;

            // Deterministic order across all clients: always attempt the
            // lexicographically smallest id first so simultaneous joiners converge
            // on the same lobby instead of fanning out across different rivals.
            var ordered = new List<ISessionInfo>(sessions);
            ordered.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            foreach (var session in ordered)
            {
                // Skip if we somehow already hold a session with this id.
                if (_activeLobby != null && session.Id == _activeLobby.Id) continue;

                try
                {
                    var joined = await _multiplayerService.JoinSessionByIdAsync(
                        session.Id,
                        new JoinSessionOptions { PlayerProperties = BuildLocalPlayerProperties() }).AsMainThread();
                    Debug.Log($"[PresenceLobbyService] Joined existing presence lobby {joined.Id} (capacity {maxPlayers}).");
                    return joined;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PresenceLobbyService] Failed to join session {session.Id}: {e.Message}");
                    LogNetDiag($"JoinSession({session.Id})", e);
                    if (IsRateLimitException(e))
                        await UniTask.Delay(RATE_LIMIT_BASE_DELAY_MS);
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a new presence lobby and stores it in <see cref="_activeLobby"/>.
        /// Retries up to <see cref="RATE_LIMIT_MAX_RETRIES"/> times on HTTP 429.
        /// </summary>
        private async UniTask CreateAsync(int maxPlayers)
        {
            Debug.Log($"[PresenceLobbyService] Creating new presence lobby (maxPlayers={maxPlayers})...");
            try
            {
                var opts = new SessionOptions
                {
                    MaxPlayers        = maxPlayers,
                    IsLocked          = false,
                    IsPrivate         = false,
                    PlayerProperties  = BuildLocalPlayerProperties(),
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        {
                            PartyLobbyKeys.GameMode,
                            new SessionProperty(
                                PRESENCE_LOBBY_GAME_MODE,
                                VisibilityPropertyOptions.Public,
                                PropertyIndex.String1)
                        }
                    }
                };

                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        SetActiveLobby(await _multiplayerService.CreateSessionAsync(opts).AsMainThread());
                        Debug.Log($"[PresenceLobbyService] Created presence lobby {_activeLobby.Id}.");
                        return;
                    }
                    catch (Exception re) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(re))
                    {
                        int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                        Debug.LogWarning($"[PresenceLobbyService] Rate limited creating lobby - retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                        await UniTask.Delay(delay);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PresenceLobbyService] Could not create presence lobby: {e.Message}");
                LogNetDiag("CreateLobby", e);
            }
        }

        /// <summary>
        /// Leaves or deletes the active lobby without throwing.  Used to release
        /// a lobby that lost a race condition (a rival lobby was created at the
        /// same moment and we are merging into theirs).
        /// </summary>
        private async UniTask DeleteOwnLobbyQuietlyAsync()
        {
            if (_activeLobby == null) return;
            Debug.Log($"[PresenceLobbyService] DeleteOwnLobbyQuietly - releasing race-lost lobby {_activeLobby.Id}.");
            try
            {
                if (_activeLobby.IsHost)
                    await _activeLobby.AsHost().DeleteAsync().AsMainThread();
                else
                    await _activeLobby.LeaveAsync().AsMainThread();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PresenceLobbyService] DeleteOwnLobby error: {e.Message}");
                LogNetDiag("DeleteOwnLobby", e);
            }
            finally
            {
                SetActiveLobby(null);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Push channel - ISession event subscriptions
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The ONLY place <see cref="_activeLobby"/> is written. Unsubscribes the
        /// push handlers from the outgoing session and subscribes them to the
        /// incoming one.
        ///
        /// <para>
        /// A single choke point rather than wire/unwire calls beside each of the
        /// six assignment sites (initial join, create, converge migration, leave,
        /// quiet delete, force-reset): missing one would either leak a
        /// subscription onto a session we no longer hold - marking the roster
        /// dirty from a lobby we left - or leave a freshly joined lobby with no
        /// push at all, silently reverting that path to poll-only.
        /// </para>
        ///
        /// <para>
        /// Marks the roster dirty on any non-null assignment so the first read of
        /// a newly joined lobby happens immediately rather than waiting for the
        /// first push or the next poll tick.
        /// </para>
        /// </summary>
        private void SetActiveLobby(ISession next)
        {
            if (ReferenceEquals(_activeLobby, next)) return;

            UnwireSessionEvents(_activeLobby);
            _activeLobby = next;
            WireSessionEvents(_activeLobby);

            if (next != null) MarkRosterDirty();
        }

        /// <inheritdoc/>
        public bool ConsumeRosterDirty() => Interlocked.Exchange(ref _rosterDirty, 0) == 1;

        /// <inheritdoc/>
        public bool ConsumeMembershipLost() => Interlocked.Exchange(ref _membershipLost, 0) == 1;

        /// <summary>
        /// Subscribes the push-channel handlers to a session we have just taken
        /// ownership of. Called at EVERY point <see cref="_activeLobby"/> is
        /// assigned - initial join, create, and the converge migration - because
        /// a lobby we migrated into pushes to us just as the first one did.
        ///
        /// <para>
        /// Every handler does exactly one thing: set an <c>int</c> flag. No Unity
        /// API, no SOAP raise, no logging. The UGS SDK gives no guarantee about
        /// which thread it dispatches these on, and per <c>Docs/THREADING.md</c> a
        /// SOAP <c>Raise()</c> invokes its listeners INLINE - so raising from here
        /// would run <c>FriendsListPanel</c>'s <c>Instantiate</c>/<c>Destroy</c>
        /// handlers off the main thread and throw
        /// <c>EnsureRunningOnMainThread</c>. Draining the flag from
        /// <c>HostConnectionService.Update()</c> is the main-thread guarantee.
        /// </para>
        ///
        /// <para>
        /// Note on <c>PlayerLeaving</c>: UGS documents it as firing BEFORE the
        /// session updates, so the departing player is still present in
        /// <see cref="ISession.Players"/> at callback time. That is precisely why
        /// this sets a flag instead of reading the roster inline - the drain runs
        /// on a later frame, by which point the session has updated.
        /// <c>PlayerHasLeft</c> fires after and is subscribed too; either one
        /// arriving is enough to schedule the re-read.
        /// </para>
        /// </summary>
        private void WireSessionEvents(ISession session)
        {
            if (session == null) return;

            session.PlayerJoined            += OnPushPlayerJoined;
            session.PlayerLeaving           += OnPushPlayerLeaving;
            session.PlayerHasLeft           += OnPushPlayerHasLeft;
            session.PlayerPropertiesChanged += OnPushPlayerPropertiesChanged;
            session.Changed                 += OnPushChanged;
            session.RemovedFromSession      += OnPushRemovedFromSession;
            session.Deleted                 += OnPushDeleted;
        }

        /// <summary>
        /// Mirror of <see cref="WireSessionEvents"/>. Called wherever
        /// <see cref="_activeLobby"/> stops being ours - leave, quiet delete,
        /// converge migration, and <see cref="ForceReset"/>. Missing one of these
        /// would leak a subscription onto a dead session object and keep marking
        /// the roster dirty from a lobby we are no longer in.
        /// </summary>
        private void UnwireSessionEvents(ISession session)
        {
            if (session == null) return;

            session.PlayerJoined            -= OnPushPlayerJoined;
            session.PlayerLeaving           -= OnPushPlayerLeaving;
            session.PlayerHasLeft           -= OnPushPlayerHasLeft;
            session.PlayerPropertiesChanged -= OnPushPlayerPropertiesChanged;
            session.Changed                 -= OnPushChanged;
            session.RemovedFromSession      -= OnPushRemovedFromSession;
            session.Deleted                 -= OnPushDeleted;
        }

        // Flag-only handlers. Named methods (not lambdas) so the -= in
        // UnwireSessionEvents actually removes them - a lambda would create a new
        // delegate instance and silently unsubscribe nothing.
        private void OnPushPlayerJoined(string _)            => MarkRosterDirty();
        private void OnPushPlayerLeaving(string playerId)    => MarkDeparted(playerId);
        private void OnPushPlayerHasLeft(string playerId)    => MarkDeparted(playerId);
        private void OnPushPlayerPropertiesChanged()         => MarkRosterDirty();
        private void OnPushChanged()                         => MarkRosterDirty();
        private void OnPushRemovedFromSession()              => MarkMembershipLost();
        private void OnPushDeleted()                         => MarkMembershipLost();

        private void MarkRosterDirty()    => Interlocked.Exchange(ref _rosterDirty, 1);
        private void MarkMembershipLost() => Interlocked.Exchange(ref _membershipLost, 1);

        private void MarkDeparted(string playerId)
        {
            if (!string.IsNullOrEmpty(playerId))
            {
                lock (_departedLock) _departedPlayerIds.Add(playerId);
            }

            MarkRosterDirty();
        }

        /// <inheritdoc/>
        public bool TryConsumeDepartedPlayerIds(List<string> into)
        {
            if (into == null) return false;

            lock (_departedLock)
            {
                if (_departedPlayerIds.Count == 0) return false;
                into.AddRange(_departedPlayerIds);
                _departedPlayerIds.Clear();
            }
            return true;
        }

        /// <summary>
        /// Emits the one-line NetDiag classification + reachability snapshot for
        /// a caught exception.
        ///
        /// <para>
        /// Every catch in this service now carries one. Before, only three did
        /// (join-fallback, leave, create) - the three that were easiest to reach
        /// in a smoke test. The four that did NOT are the ones that matter most
        /// for diagnosing a split or partial online list: both halves of
        /// <see cref="ConvergeToCanonicalAsync"/> (the self-heal for a
        /// simultaneous-create race) and the per-session join attempt inside
        /// <see cref="TryQueryAndJoinAsync"/>. When convergence silently failed,
        /// two clients sat in different presence lobbies seeing each other's
        /// panels as empty, and the only evidence was an unclassified
        /// <c>LogWarning</c> with no indication of whether the cause was
        /// <c>Offline</c>, <c>RateLimit</c>, <c>SessionGone</c> or something else.
        /// That distinction is exactly what
        /// <c>Docs/PresenceSystem/REFACTOR.md</c>'s <c>LobbyMembershipMonitor</c>
        /// extraction is waiting on.
        /// </para>
        /// </summary>
        private static void LogNetDiag(string operation, Exception e) =>
            CSDebug.Log($"[PresenceLobbyService] {operation} NetDiag: " +
                        $"class={NetworkDiagnostics.ClassifyException(e)} | {NetworkDiagnostics.GetSnapshot()}");

        /// <summary>
        /// True when the exception is a UGS HTTP 429 Too Many Requests response.
        /// Used in <c>catch ... when (...)</c> clauses to distinguish rate-limit
        /// errors (retry-able) from other errors (propagate).
        ///
        /// <para>
        /// Delegates to <see cref="UgsErrorClassifier.IsRateLimit"/> so this
        /// service, <see cref="HostConnectionService"/> and
        /// <see cref="PartySessionService"/> agree. The previous local version
        /// tested only <c>e.Message.Contains("Too Many Requests")</c> on the
        /// OUTER exception, so a wrapped 429 - the common shape, since UGS and
        /// UniTask both wrap - fell through every <c>catch ... when</c> filter
        /// here and propagated as a hard failure instead of retrying.
        /// </para>
        /// </summary>
        private static bool IsRateLimitException(Exception e) =>
            UgsErrorClassifier.IsRateLimit(e);
    }
}
