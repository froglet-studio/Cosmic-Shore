using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lightweight NetworkBehaviour that relays arcade game configuration UI state
    /// between host and clients. The host configures PC + DC + intensity privately
    /// on Screen 1 (ConfigurationDetailView). When the host clicks "Confirm
    /// Configuration", the server commits DC into shared game state, resets every
    /// human's NetDomain to Jade, and broadcasts a single ClientRpc to open the
    /// modal directly at GameDetailView on every client.
    ///
    /// Each player (host and clients) then independently selects their domain and
    /// vessel from GameDetailView, then presses Start to confirm. Once all human
    /// players have confirmed, the host automatically launches the game.
    ///
    /// Place on a scene-level GameObject in Menu_Main alongside the existing
    /// ServerPlayerVesselInitializer hierarchy.
    ///
    /// <para>
    /// <b>The open lobby is replicated STATE, not a one-shot message.</b> Open / close /
    /// intensity / roster used to travel as ClientRpcs, and a ClientRpc reaches exactly the
    /// clients that are synchronized at the instant it is sent: a guest still inside Netcode
    /// scene synchronization when the host opened a card had the RPC deferred and then dropped
    /// (the "[Deferred OnSpawn]" lines in a joiner's log), and a guest who joined AFTER the
    /// host opened a card was never told at all - so the client sat on the lava lamp while the
    /// host looked at a lobby, and "come out of the card and click it again" was the only way
    /// to reach them. <see cref="LobbySnapshot"/> in a server-written NetworkVariable is the
    /// whole answer: every peer holds the current lobby, a late joiner receives it with the
    /// spawn and applies it in <see cref="OnNetworkSpawn"/>, and the C# events this class has
    /// always raised are now DERIVED by diffing the previous value against the new one, so the
    /// modal did not have to change. The ready-up count stays an RPC - it is a transient
    /// acknowledgement, not a fact a late joiner needs to catch up on.
    /// </para>
    /// </summary>
    public class ArcadeConfigSyncManager : NetworkBehaviour
    {
        /// <summary>
        /// Scene-unique resolution handle. This lives on the network-spawned "Game" object in
        /// Menu_Main, and the arcade card grid needs to reach it from a prefab that cannot carry
        /// a scene reference. Resolution ONLY - every notification still travels as one of the
        /// C# events below, matching the rest of this class, rather than through a static.
        /// </summary>
        public static ArcadeConfigSyncManager Instance { get; private set; }

        [Inject] GameDataSO gameData;

        [Inject] SO_GameList gameList;

        /// <summary>
        /// One party member's standing request to play a game mode - the arcade card chip.
        /// The avatar id travels WITH the pick rather than being looked up per peer, because a
        /// pick can arrive before that member's Player object has replicated its NetAvatarId and
        /// a chip that renders blank for a second reads as a bug.
        /// </summary>
        public struct ArcadeGamePick : INetworkSerializable, System.IEquatable<ArcadeGamePick>
        {
            public ulong ClientId;
            public int GameMode;
            public int AvatarId;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref ClientId);
                serializer.SerializeValue(ref GameMode);
                serializer.SerializeValue(ref AvatarId);
            }

            public bool Equals(ArcadeGamePick other) =>
                ClientId == other.ClientId && GameMode == other.GameMode && AvatarId == other.AvatarId;
        }

        NetworkList<ArcadeGamePick> _gamePicks;

        /// <summary>
        /// Raised on every peer whenever the set of standing game picks changes. The arcade grid
        /// redraws its chips from <see cref="GamePicks"/>.
        /// </summary>
        public event System.Action OnGamePicksChanged;

        /// <summary>Every party member's standing pick. Empty outside a party.</summary>
        public IReadOnlyList<ArcadeGamePick> GamePicks => _gamePicksView;
        readonly List<ArcadeGamePick> _gamePicksView = new();

        /// <summary>
        /// The host's open lobby as one value: which card, at what intensity, how many seats,
        /// how many domains, and which domains the host has placed an AI on. <c>Generation</c>
        /// climbs on every OPEN so a close-and-reopen of the same card still reads as a new
        /// open on a peer that never saw the close. AI placements ride as four fixed slots
        /// rather than an array so the struct stays unmanaged (a NetworkVariable compares and
        /// copies it by value): a match seats at most <c>ArcadeGameConfigureModal.MaxMatchSeats</c>
        /// (4), and one of those is always the host, so four slots is one more than can ever be
        /// used.
        /// </summary>
        public struct LobbySnapshot : INetworkSerializable, System.IEquatable<LobbySnapshot>
        {
            public const int MaxAiSlots = 4;

            public int  Generation;
            public bool IsOpen;
            public int  GameMode;
            public int  Intensity;
            public int  PlayerCount;
            public int  MaxPlayers;
            public int  DomainCount;
            public int  AiCount;
            public int  Ai0, Ai1, Ai2, Ai3;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Generation);
                serializer.SerializeValue(ref IsOpen);
                serializer.SerializeValue(ref GameMode);
                serializer.SerializeValue(ref Intensity);
                serializer.SerializeValue(ref PlayerCount);
                serializer.SerializeValue(ref MaxPlayers);
                serializer.SerializeValue(ref DomainCount);
                serializer.SerializeValue(ref AiCount);
                serializer.SerializeValue(ref Ai0);
                serializer.SerializeValue(ref Ai1);
                serializer.SerializeValue(ref Ai2);
                serializer.SerializeValue(ref Ai3);
            }

            public bool Equals(LobbySnapshot o) =>
                Generation == o.Generation && IsOpen == o.IsOpen && GameMode == o.GameMode &&
                Intensity == o.Intensity && PlayerCount == o.PlayerCount && MaxPlayers == o.MaxPlayers &&
                DomainCount == o.DomainCount && SameAi(o);

            public bool SameAi(LobbySnapshot o) =>
                AiCount == o.AiCount && Ai0 == o.Ai0 && Ai1 == o.Ai1 && Ai2 == o.Ai2 && Ai3 == o.Ai3;

            /// <summary>The placed AI domains as the modal consumes them (Domains as ints, placement order).</summary>
            public int[] PlacedAiDomains()
            {
                int n = Mathf.Clamp(AiCount, 0, MaxAiSlots);
                var result = new int[n];
                for (int i = 0; i < n; i++) result[i] = Slot(i);
                return result;
            }

            public void SetPlacedAiDomains(int[] placed)
            {
                int n = placed == null ? 0 : Mathf.Min(placed.Length, MaxAiSlots);
                if (placed != null && placed.Length > MaxAiSlots)
                    Debug.LogWarning($"[ArcadeConfigSync] {placed.Length} placed AI exceed the {MaxAiSlots} replicated slots - truncating.");
                AiCount = n;
                Ai0 = n > 0 ? placed[0] : 0;
                Ai1 = n > 1 ? placed[1] : 0;
                Ai2 = n > 2 ? placed[2] : 0;
                Ai3 = n > 3 ? placed[3] : 0;
            }

            int Slot(int i) => i switch { 0 => Ai0, 1 => Ai1, 2 => Ai2, _ => Ai3 };
        }

        /// <summary>Server-written; every peer reads. See the class summary for why this is state.</summary>
        readonly NetworkVariable<LobbySnapshot> _lobby = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>The lobby as this peer currently sees it. <c>IsOpen</c> false outside a lobby.</summary>
        public LobbySnapshot CurrentLobby => _lobby.Value;

        readonly HashSet<ulong> _readyClients = new();

        /// <summary>
        /// The human head-count the host committed with. The LIVE expectation is
        /// <see cref="ExpectedHumanCount"/>: a guest who joins the party after the host opened
        /// the card is a human whose ready press the launch must wait for, and one who leaves
        /// must stop being waited on - so the count is read off the connected clients at every
        /// check rather than frozen at commit.
        /// </summary>
        int _committedHumanCount;

        int ExpectedHumanCount
        {
            get
            {
                var nm = NetworkManager.Singleton;
                int connected = nm != null && nm.IsListening ? nm.ConnectedClientsIds.Count : 0;
                return Mathf.Max(_committedHumanCount, connected);
            }
        }

        // Server-side single-shot guard. Host spam-clicking the Confirm button on
        // Screen 1 must not re-broadcast the open RPC, re-write gameData, or
        // re-reset NetDomains (which would silently yank a client's just-made
        // domain pick back to Jade). Reset on modal close so the next session
        // can commit fresh.
        bool _isCommitted;

        /// <summary>
        /// Raised on clients when the host commits configuration (Screen 1 → Screen 2).
        /// Args: gameMode, intensity, playerCount, maxPlayers, domainCount
        /// </summary>
        public event System.Action<int, int, int, int, int> OnConfigOpenedOnClient;

        /// <summary>
        /// Raised on all clients when the host closes/cancels the config modal.
        /// </summary>
        public event System.Action OnConfigClosedOnClient;

        /// <summary>
        /// Raised on all instances (host + clients) when a player confirms ready.
        /// Args: readyCount, totalExpected
        /// </summary>
        public event System.Action<int, int> OnPlayerReadyCountChanged;

        /// <summary>
        /// Raised on all instances when every human player has confirmed ready.
        /// The host uses this to auto-launch the game.
        /// </summary>
        public event System.Action OnAllPlayersReady;

        /// <summary>
        /// Raised on clients when the host navigates between modal screens.
        /// Arg: screen index (0=config, 1=gameDetail, 2=vesselSelection, 3=squadMate)
        /// </summary>
        public event System.Action<int> OnScreenChangedOnClient;

        /// <summary>
        /// Raised on clients when the host moves the intensity row while the lobby is open.
        /// Arg: intensity. The preview microgame itself is deliberately UNSYNCED - each machine
        /// flies its own local satellite - but intensity decides WHICH arena that is, so a
        /// client's modal follows the host's number and rebuilds (or exits) its own preview.
        /// </summary>
        public event System.Action<int> OnIntensityChangedOnClient;

        /// <summary>
        /// Raised on clients when the host reshapes the roster mid-lobby - an AI placed on a
        /// domain, or one kicked. Args: playerCount, domainCount, placed AI domains (as ints,
        /// in placement order), so a client's tile chips redraw in real time to exactly what
        /// the host is looking at.
        /// </summary>
        public event System.Action<int, int, int[]> OnRosterChangedOnClient;

        void Awake()
        {
            Instance = this;
            // Constructed in Awake, never in a field initializer: a NetworkList must exist
            // before OnNetworkSpawn and must not be re-made on a re-spawn.
            _gamePicks ??= new NetworkList<ArcadeGamePick>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            if (_gamePicks != null) _gamePicks.OnListChanged += HandleGamePicksChanged;

            // Late join: OnListChanged does not fire for entries that existed before this peer
            // subscribed, so read the standing picks by hand. A joiner should walk in seeing
            // which cards the party is already queuing for.
            RebuildGamePicksView();
            OnGamePicksChanged?.Invoke();

            _lobby.OnValueChanged += HandleLobbyChanged;

            // Late join: the initial value arrives WITH the spawn and OnValueChanged does not fire
            // for it. A guest who connected while the host was already sitting in a card is
            // pulled into that card here - the case the one-shot open RPC could never reach.
            if (!IsServer)
                ApplyLobbyDelta(default, _lobby.Value);

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
                NetworkManager.OnClientConnectedCallback  += HandleClientConnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_gamePicks != null) _gamePicks.OnListChanged -= HandleGamePicksChanged;
            _lobby.OnValueChanged -= HandleLobbyChanged;

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                NetworkManager.OnClientConnectedCallback  -= HandleClientConnected;
            }

            // The party is gone; nothing should keep drawing its chips.
            _gamePicksView.Clear();
            OnGamePicksChanged?.Invoke();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Game picks - "this party member wants to play THIS card"
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// True when this peer is a CLIENT in a live party - the one case in which tapping an
        /// arcade card must NOT open the configure modal, because only the host configures.
        ///
        /// Deliberately NOT <c>HostConnectionDataSO.IsPartyHost</c>: that field is written by
        /// nothing outside the edit-mode tests, so a gate built on it would read false for the
        /// host as well and silently stand the whole arcade down. Under the locked EAGER-Relay
        /// design a solo player IS the server, so this is also correctly false offline and in
        /// single player.
        /// </summary>
        public static bool IsPartyClient
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && nm.IsListening && !nm.IsServer;
            }
        }

        /// <summary>
        /// Ask the host to record (or withdraw) this peer's interest in a game mode. A second
        /// request for the SAME mode withdraws it, so the card the player is standing on is a
        /// toggle. Safe to call on the host: it is a no-op there, because a host opens the card
        /// rather than queuing for it.
        /// </summary>
        public void RequestGamePick(int gameMode, int avatarId)
        {
            if (!IsSpawned || !IsPartyClient) return;
            RequestGamePick_ServerRpc(gameMode, avatarId);
        }

        [ServerRpc(RequireOwnership = false)]
        void RequestGamePick_ServerRpc(int gameMode, int avatarId, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || _gamePicks == null) return;
            ulong clientId = rpcParams.Receive.SenderClientId;

            for (int i = 0; i < _gamePicks.Count; i++)
            {
                if (_gamePicks[i].ClientId != clientId) continue;

                // Same card twice = withdraw. Any other card = move the chip, because a member
                // wants ONE game at a time and two chips for one player would misreport the
                // party's appetite to the host.
                if (_gamePicks[i].GameMode == gameMode) _gamePicks.RemoveAt(i);
                else _gamePicks[i] = new ArcadeGamePick { ClientId = clientId, GameMode = gameMode, AvatarId = avatarId };
                return;
            }

            _gamePicks.Add(new ArcadeGamePick { ClientId = clientId, GameMode = gameMode, AvatarId = avatarId });
        }

        /// <summary>
        /// Drops every standing pick. Called when the host actually commits a configuration -
        /// the request has been answered, so the board resets for the next round rather than
        /// leaving stale chips on cards nobody is waiting for any more.
        /// </summary>
        public void ClearGamePicks()
        {
            if (!IsServer || _gamePicks == null || _gamePicks.Count == 0) return;
            _gamePicks.Clear();
        }

        /// <summary>
        /// A member who leaves must not leave a ghost chip behind. Server-side; the list change
        /// replicates the removal to everyone still here.
        /// </summary>
        void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            if (_gamePicks != null)
            {
                for (int i = _gamePicks.Count - 1; i >= 0; i--)
                    if (_gamePicks[i].ClientId == clientId)
                        _gamePicks.RemoveAt(i);
            }

            // A member who leaves mid-lobby is neither ready nor expected any more. Only the
            // count is re-announced - a launch is something a PRESS causes, never a departure.
            if (_isCommitted && (_readyClients.Remove(clientId) || _lobby.Value.IsOpen))
                SyncReadyCount_ClientRpc(_readyClients.Count, ExpectedHumanCount);
        }

        /// <summary>
        /// A member who joins mid-lobby raises the head-count the launch waits for; tell every
        /// peer so the ready lights show the new denominator. (The joiner's own modal opens off
        /// the replicated <see cref="LobbySnapshot"/> in its OnNetworkSpawn.)
        /// </summary>
        void HandleClientConnected(ulong clientId)
        {
            if (!IsServer || !_isCommitted) return;
            SyncReadyCount_ClientRpc(_readyClients.Count, ExpectedHumanCount);
        }

        void HandleGamePicksChanged(NetworkListEvent<ArcadeGamePick> _)
        {
            RebuildGamePicksView();
            OnGamePicksChanged?.Invoke();
        }

        void RebuildGamePicksView()
        {
            _gamePicksView.Clear();
            if (_gamePicks == null) return;
            for (int i = 0; i < _gamePicks.Count; i++) _gamePicksView.Add(_gamePicks[i]);
        }

        /// <summary>
        /// Whether the LOCAL peer is the one who picked this mode - what the card reads to mark
        /// a chip as "yours" rather than a teammate's.
        /// </summary>
        public bool LocalPlayerPicked(int gameMode)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return false;
            for (int i = 0; i < _gamePicksView.Count; i++)
                if (_gamePicksView[i].ClientId == nm.LocalClientId && _gamePicksView[i].GameMode == gameMode)
                    return true;
            return false;
        }

        #region Host → Client: Config commit / close

        /// <summary>
        /// Called by ArcadeGameConfigureModal on the host when the host clicks
        /// "Confirm Configuration" on Screen 1. Commits PC + DC + intensity
        /// before broadcasting modal-open to clients:
        ///   1. Writes gameData.RequestedDomainCount so Player.RequestSetDomain_ServerRpc
        ///      validates against the live value from now on.
        ///   2. Resets every human's NetDomain to Jade so GameDetailView opens
        ///      with all chips on the Jade tile across every client.
        ///   3. Writes the open lobby into the replicated <see cref="LobbySnapshot"/> -
        ///      every client (present or arriving later) opens the modal at
        ///      GameDetailView with the back button hidden, tiles outside
        ///      [0..DC-1] dimmed/non-interactable.
        ///
        /// Idempotent - repeated calls (host spam-clicks Confirm) short-circuit
        /// at the _isCommitted gate.
        /// </summary>
        public void CommitConfiguration(int gameMode, int intensity, int playerCount,
                                        int maxPlayers, int humanCount, int domainCount)
        {
            if (!IsServer) return;
            if (_isCommitted) return;
            _isCommitted = true;

            _readyClients.Clear();
            _committedHumanCount = humanCount;

            if (gameData != null)
            {
                gameData.RequestedDomainCount = domainCount;

                foreach (var ip in gameData.Players)
                {
                    if (ip is Player pl && !pl.NetIsAI.Value)
                        pl.NetDomain.Value = Domains.Jade;
                }
            }

            // The party's requests have been answered - drop the chips so the grid does not
            // keep advertising a vote that is now history.
            ClearGamePicks();

            // One value, replicated: open the lobby on every peer that is here AND every peer
            // that arrives later. Generation climbs so a reopen after a close is a new open even
            // on a client that never saw the close land.
            var previous = _lobby.Value;
            _lobby.Value = new LobbySnapshot
            {
                Generation  = previous.Generation + 1,
                IsOpen      = true,
                GameMode    = gameMode,
                Intensity   = intensity,
                PlayerCount = playerCount,
                MaxPlayers  = maxPlayers,
                DomainCount = domainCount,
            };
        }

        /// <summary>
        /// Called by ArcadeGameConfigureModal on the host when the modal closes
        /// (back button or cancel - NOT game start). Re-arms the commit guard so
        /// the next configuration session can broadcast.
        /// </summary>
        public void NotifyConfigClosed()
        {
            if (!IsServer) return;
            _isCommitted = false;
            _readyClients.Clear();

            var snapshot = _lobby.Value;
            if (!snapshot.IsOpen) return;
            snapshot.IsOpen = false;
            _lobby.Value = snapshot;
        }

        // ── Replicated lobby → the client-side events ────────────────────────

        void HandleLobbyChanged(LobbySnapshot previous, LobbySnapshot next)
        {
            if (IsServer) return; // The host drew all of it locally
            ApplyLobbyDelta(previous, next);
        }

        /// <summary>
        /// Turns a change of the replicated lobby into the events the modal has always listened
        /// to. An OPEN (closed→open, or a new generation) is the open event followed by the
        /// roster, so a late joiner also sees the AI the host has already placed; a CLOSE is the
        /// close event; anything else is the intensity and/or roster moving under an open lobby.
        /// </summary>
        void ApplyLobbyDelta(LobbySnapshot previous, LobbySnapshot next)
        {
            if (next.IsOpen && (!previous.IsOpen || previous.Generation != next.Generation))
            {
                RaiseOpened(next);
                return;
            }

            if (!next.IsOpen)
            {
                if (previous.IsOpen) OnConfigClosedOnClient?.Invoke();
                return;
            }

            if (previous.Intensity != next.Intensity)
                OnIntensityChangedOnClient?.Invoke(next.Intensity);

            if (previous.PlayerCount != next.PlayerCount || previous.DomainCount != next.DomainCount || !previous.SameAi(next))
                OnRosterChangedOnClient?.Invoke(next.PlayerCount, next.DomainCount, next.PlacedAiDomains());
        }

        void RaiseOpened(LobbySnapshot lobby)
        {
            int subscriberCount = OnConfigOpenedOnClient?.GetInvocationList().Length ?? 0;
            Debug.Log($"[ArcadeConfigSync] Lobby open on client - gameMode={lobby.GameMode}, gen={lobby.Generation}, subscribers={subscriberCount}");

            if (subscriberCount == 0)
                Debug.LogWarning("[ArcadeConfigSync] No subscribers on OnConfigOpenedOnClient - modal will not open now. " +
                                 "ArcadeGameConfigureModal.OnEnable re-reads CurrentLobby when it subscribes, so it catches up then.");

            OnConfigOpenedOnClient?.Invoke(lobby.GameMode, lobby.Intensity, lobby.PlayerCount, lobby.MaxPlayers, lobby.DomainCount);

            if (lobby.AiCount > 0)
                OnRosterChangedOnClient?.Invoke(lobby.PlayerCount, lobby.DomainCount, lobby.PlacedAiDomains());
        }

        /// <summary>
        /// For a subscriber that attached AFTER the lobby value landed (the modal re-enabling):
        /// re-deliver the current open lobby to it. No-op on the host and when nothing is open.
        /// </summary>
        public void ReplayLobbyToSubscribers()
        {
            if (!IsSpawned || IsServer) return;
            var lobby = _lobby.Value;
            if (lobby.IsOpen) RaiseOpened(lobby);
        }

        /// <summary>
        /// Called by ArcadeGameConfigureModal on the host when navigating between
        /// modal screens so clients follow the same screen transitions.
        /// </summary>
        public void NotifyScreenChanged(int screenIndex)
        {
            if (!IsServer) return;
            ChangeScreenOnClients_ClientRpc(screenIndex);
        }

        /// <summary>
        /// Called by ArcadeGameConfigureModal on the host whenever the intensity changes while
        /// the lobby is open, so every client's modal - and its own local preview - follows.
        /// </summary>
        public void NotifyIntensityChanged(int intensity)
        {
            if (!IsServer) return;
            var snapshot = _lobby.Value;
            if (!snapshot.IsOpen || snapshot.Intensity == intensity) return;
            snapshot.Intensity = intensity;
            _lobby.Value = snapshot;
        }

        /// <summary>
        /// Called by ArcadeGameConfigureModal on the host after every Add AI placement or kick,
        /// so clients' chips follow the host's roster live rather than freezing at the counts
        /// the open RPC carried.
        /// </summary>
        public void NotifyRosterChanged(int playerCount, int domainCount, int[] placedAiDomains)
        {
            if (!IsServer) return;
            var snapshot = _lobby.Value;
            if (!snapshot.IsOpen) return;
            snapshot.PlayerCount = playerCount;
            snapshot.DomainCount = domainCount;
            snapshot.SetPlacedAiDomains(placedAiDomains);
            if (snapshot.Equals(_lobby.Value)) return;
            _lobby.Value = snapshot;
        }

        [ClientRpc]
        void ChangeScreenOnClients_ClientRpc(int screenIndex)
        {
            if (IsServer) return;
            OnScreenChangedOnClient?.Invoke(screenIndex);
        }

        #endregion

        #region Ready-up system

        /// <summary>
        /// Called by ArcadeGameConfigureModal when ANY player (host or client)
        /// presses the Start/Confirm button to lock in their team + vessel choices.
        /// Clients send a ServerRpc; the host confirms locally.
        /// </summary>
        public void ConfirmLocalPlayerReady()
        {
            if (IsServer)
            {
                // Host confirms directly
                HandlePlayerReady(NetworkManager.Singleton.LocalClientId);
            }
            else
            {
                ConfirmReady_ServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void ConfirmReady_ServerRpc(ServerRpcParams rpcParams = default)
        {
            HandlePlayerReady(rpcParams.Receive.SenderClientId);
        }

        void HandlePlayerReady(ulong clientId)
        {
            if (!_readyClients.Add(clientId))
                return; // Already confirmed

            int expected = ExpectedHumanCount;
            Debug.Log($"[ArcadeConfigSync] Player {clientId} confirmed ready ({_readyClients.Count}/{expected})");

            // Notify all clients of the updated ready count
            SyncReadyCount_ClientRpc(_readyClients.Count, expected);

            if (_readyClients.Count >= expected)
            {
                Debug.Log("[ArcadeConfigSync] All players ready - launching game");
                AllPlayersReady_ClientRpc();
            }
        }

        [ClientRpc]
        void SyncReadyCount_ClientRpc(int readyCount, int totalExpected)
        {
            OnPlayerReadyCountChanged?.Invoke(readyCount, totalExpected);
        }

        [ClientRpc]
        void AllPlayersReady_ClientRpc()
        {
            OnAllPlayersReady?.Invoke();
        }

        #endregion

        #region Utility

        /// <summary>
        /// Helper for clients to look up an SO_ArcadeGame by its GameModes int value.
        /// Returns null if not found.
        /// </summary>
        public SO_ArcadeGame FindGameByMode(int gameModeInt)
        {
            if (gameList == null || gameList.Games == null) return null;
            var mode = (GameModes)gameModeInt;
            foreach (var game in gameList.Games)
            {
                if (game != null && game.Mode == mode)
                    return game;
            }
            return null;
        }

        #endregion
    }
}
