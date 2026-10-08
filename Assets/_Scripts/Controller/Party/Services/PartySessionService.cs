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
//   Every UGS call runs through UgsRequestPolicy.ExecuteAsync (Assets/_Scripts/Utility/UgsRequestPolicy.cs):
//   one classifier, exponential back-off with jitter per failure class, single-flight by operation
//   key ("party:create", "party:join:{id}") and a per-client retry budget. This file used to carry
//   three retry loops of its own with three private classifiers; since 2026-10-07 it carries none
//   (Docs/MultiplayerArchitecture/REVIEW_INVITE_AND_RESILIENCE.md §5.4). Non-retryable errors
//   (Gone / Full / Fatal) propagate to the caller (AcceptInviteAsync), which logs and rethrows them
//   for fail-fast recovery.
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


        // Lobby player-property keys - written during session create/join so
        // other lobby members can see our display name, party info, etc.
        private const string DISPLAY_NAME_KEY    = "displayName";
        private const string AVATAR_ID_KEY       = "avatarId";
        private const string PARTY_COUNT_KEY     = "partyCount";
        private const string PARTY_MAX_KEY       = "partyMax";
        private const string MATCH_NAME_KEY      = "matchName";
        private const string JOINED_PARTY_KEY    = "joined_party";
        private const string INVITE_PAYLOADS_KEY = "invite_payloads";

        /// <summary>
        /// Session player-property key a SPECTATOR sets to "1" on join. Read by
        /// <see cref="IsSpectator"/> on every peer so the party roster, the host's admit
        /// scan and the party-size publish all leave spectators out. Absent or empty on
        /// every ordinary member.
        /// </summary>
        public const string SPECTATOR_KEY = "spectator";

        /// <summary>True when the session player joined as a spectator (see <see cref="SPECTATOR_KEY"/>).</summary>
        public static bool IsSpectator(IReadOnlyPlayer p) =>
            p != null &&
            p.Properties != null &&
            p.Properties.TryGetValue(SPECTATOR_KEY, out var prop) &&
            prop.Value == "1";

        // ─────────────────────────────────────────────────────────────────────
        // Dependencies + state
        // ─────────────────────────────────────────────────────────────────────

        private readonly HostConnectionDataSO _connectionData;
        private readonly GameDataSO _gameData;
        private readonly UgsRequestPolicy _policy;

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
        private void OnSessionPlayerLeaving(string playerId) => PlayerLeaving?.Invoke(playerId);

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
        public PartySessionService(HostConnectionDataSO connectionData, GameDataSO gameData, UgsRequestPolicy policy)
        {
            _connectionData = connectionData;
            _gameData = gameData;
            _policy   = policy ?? UgsRequestPolicy.CreateDefault();
        }

        // ─────────────────────────────────────────────────────────────────────
        // IPartySessionService - session lifecycle
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new private Relay-backed party session and sets
        /// <see cref="ActiveSession"/>.  No-op if a session is already active.
        ///
        /// <para>
        /// Runs under <see cref="UgsRequestPolicy.ExecuteAsync{T}"/> (key <c>party:create</c>):
        /// jittered back-off on rate-limit / transient failures, one retry on a host conflict (NM
        /// still shutting down). Caller is responsible for shutting down the local NetworkManager
        /// BEFORE calling this method.
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

            // Single-flight under "party:create": two callers racing to create collapse into one
            // request and both observe the same session. The policy retries RateLimited / Transient
            // / Benign failures with jittered back-off and a Conflict (NetworkManager still shutting
            // down) exactly once; everything else propagates.
            var session = await _policy.ExecuteAsync("party:create",
                async () => await _multiplayerService.CreateSessionAsync(opts).AsMainThread());
            if (ReferenceEquals(ActiveSession, session)) return; // the coalesced second caller
            ActiveSession          = session;
            CreatedAtUnscaledTime  = Time.unscaledTime;
            ActiveSession.PlayerLeaving += OnSessionPlayerLeaving;
            CSDebug.LogVerbose(CSLogChannel.Party, $"[PartySessionService] Created party session {ActiveSession.Id} (maxPlayers={maxPlayers}).");
        }

        /// <summary>
        /// Joins an existing party session by its UGS session ID and sets
        /// <see cref="ActiveSession"/>.
        ///
        /// </summary>
        /// <param name="sessionId">
        /// The UGS Relay session id published by the host after they call
        /// <see cref="CreateAsync"/>.
        /// </param>
        public UniTask JoinByIdAsync(string sessionId) => JoinByIdAsync(sessionId, asSpectator: false);

        /// <inheritdoc/>
        public async UniTask JoinByIdAsync(string sessionId, bool asSpectator)
        {
            var opts = new JoinSessionOptions { PlayerProperties = BuildLocalPlayerProperties(asSpectator) };

            // Two clients accepting the same host's invite near-simultaneously can collide on the
            // host's session state, so one join throws a transient error before the NM client even
            // starts; the policy retries it. Non-retryable errors propagate to the caller
            // (HostConnectionService.AcceptInviteAsync), which logs and rethrows so
            // PartyInviteController fails fast. See Docs/PartySystem/ARCHITECTURE.md (Q5).
            var session = await _policy.ExecuteAsync($"party:join:{sessionId}",
                async () => await _multiplayerService.JoinSessionByIdAsync(sessionId, opts).AsMainThread());
            if (ReferenceEquals(ActiveSession, session)) return; // the coalesced second caller
            ActiveSession = session;
            ActiveSession.PlayerLeaving += OnSessionPlayerLeaving;
            CSDebug.LogVerbose(CSLogChannel.Party, $"[PartySessionService] Joined party session {ActiveSession.Id}.");
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
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartySessionService] Left party session {session.Id}.");
            }
            catch (Exception e)
            {
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartySessionService] Leave error (session already gone?): {e.Message}");
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartySessionService] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
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
            UgsRequestTelemetry.Count(UgsRequestCounter.LobbyReads);
            await ActiveSession.RefreshAsync().AsMainThread();
        }

        /// <inheritdoc/>
        public async UniTask UpdateLocalPlayerPropertiesAsync(string displayName, int avatarId)
        {
            var session = ActiveSession;
            if (session == null) return;

            try
            {
                // Through the policy like every other UGS write (LobbyPropertyWriter.SaveAsync is the
                // model): no single-flight key - two saves may carry different names - and on a retry
                // the session is re-read first (a stale player index is the usual reason a save
                // fails, Docs/PresenceSystem/BUGS.md B1). The properties are set INSIDE the attempt so
                // a re-read can never leave the retry saving without them.
                bool firstAttempt = true;
                await _policy.ExecuteAsync(null, async () =>
                {
                    if (!firstAttempt)
                    {
                        UgsRequestTelemetry.Count(UgsRequestCounter.LobbyReads);
                        try { await session.RefreshAsync().AsMainThread(); } catch { /* best-effort resync before the retry */ }
                    }
                    firstAttempt = false;
                    session.CurrentPlayer.SetProperty(DISPLAY_NAME_KEY,
                        new PlayerProperty(string.IsNullOrEmpty(displayName) ? "Pilot" : displayName,
                            VisibilityPropertyOptions.Public));
                    session.CurrentPlayer.SetProperty(AVATAR_ID_KEY,
                        new PlayerProperty(avatarId.ToString(), VisibilityPropertyOptions.Public));
                    await session.SaveCurrentPlayerDataAsync().AsMainThread();
                });
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartySessionService] Local player properties updated (displayName='{displayName}').");
            }
            catch (Exception e)
            {
                // Non-fatal: peers keep the stale name until the next session
                // (re)join. Never throw into the profile-change event chain.
                CSDebug.LogWarning(
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
            CSDebug.LogVerbose(CSLogChannel.Party, $"[PartySessionService] Clearing session reference {ActiveSession.Id}.");
            ActiveSession.PlayerLeaving -= OnSessionPlayerLeaving;
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
        private Dictionary<string, PlayerProperty> BuildLocalPlayerProperties(bool asSpectator = false)
        {
            int partyCount = _connectionData.PartyMembers != null ? _connectionData.PartyMembers.Count : 0;
            // Displayed party size, not transport capacity - see PresenceLobbyService.
            int partyMax   = _connectionData.MaxPartySlots;

            return new Dictionary<string, PlayerProperty>
            {
                // Written on EVERY join (empty for a member) so a stale "1" can never survive a
                // re-join of the same identity as a member.
                { SPECTATOR_KEY,       new PlayerProperty(asSpectator ? "1" : string.Empty, VisibilityPropertyOptions.Public) },
                { DISPLAY_NAME_KEY,    new PlayerProperty(string.IsNullOrEmpty(_connectionData.LocalDisplayName) ? "Pilot" : _connectionData.LocalDisplayName, VisibilityPropertyOptions.Public) },
                { AVATAR_ID_KEY,       new PlayerProperty(_connectionData.LocalAvatarId.ToString(),    VisibilityPropertyOptions.Public) },
                { PARTY_COUNT_KEY,     new PlayerProperty(partyCount.ToString(), VisibilityPropertyOptions.Public) },
                { PARTY_MAX_KEY,       new PlayerProperty(partyMax.ToString(),   VisibilityPropertyOptions.Public) },
                { MATCH_NAME_KEY,      new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { JOINED_PARTY_KEY,    new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
                { INVITE_PAYLOADS_KEY, new PlayerProperty(string.Empty,          VisibilityPropertyOptions.Public) },
            };
        }
    }
}
