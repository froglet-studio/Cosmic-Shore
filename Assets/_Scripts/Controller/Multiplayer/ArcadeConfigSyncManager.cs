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

        readonly HashSet<ulong> _readyClients = new();
        int _expectedHumanCount;

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

            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (_gamePicks != null) _gamePicks.OnListChanged -= HandleGamePicksChanged;

            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;

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
            if (!IsServer || _gamePicks == null) return;
            for (int i = _gamePicks.Count - 1; i >= 0; i--)
                if (_gamePicks[i].ClientId == clientId)
                    _gamePicks.RemoveAt(i);
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
        ///   3. Broadcasts OpenConfigOnClients_ClientRpc - clients open modal at
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
            _expectedHumanCount = Mathf.Max(humanCount, NetworkManager.Singleton.ConnectedClientsIds.Count);

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

            OpenConfigOnClients_ClientRpc(gameMode, intensity, playerCount, maxPlayers, domainCount);
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
            CloseConfigOnClients_ClientRpc();
        }

        [ClientRpc]
        void OpenConfigOnClients_ClientRpc(int gameMode, int intensity, int playerCount, int maxPlayers, int domainCount)
        {
            if (IsServer) return; // Host already has the modal open

            int subscriberCount = OnConfigOpenedOnClient?.GetInvocationList().Length ?? 0;
            Debug.Log($"[ArcadeConfigSync] ClientRpc received - gameMode={gameMode}, subscribers={subscriberCount}");

            if (subscriberCount == 0)
                Debug.LogWarning("[ArcadeConfigSync] No subscribers on OnConfigOpenedOnClient - modal will not open. " +
                                 "Is ArcadeGameConfigureModal.OnEnable() running? Is ModalWindows active?");

            OnConfigOpenedOnClient?.Invoke(gameMode, intensity, playerCount, maxPlayers, domainCount);
        }

        [ClientRpc]
        void CloseConfigOnClients_ClientRpc()
        {
            if (IsServer) return;
            OnConfigClosedOnClient?.Invoke();
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
            IntensityChangedOnClients_ClientRpc(intensity);
        }

        [ClientRpc]
        void IntensityChangedOnClients_ClientRpc(int intensity)
        {
            if (IsServer) return; // The host already applied it locally
            OnIntensityChangedOnClient?.Invoke(intensity);
        }

        /// <summary>
        /// Called by ArcadeGameConfigureModal on the host after every Add AI placement or kick,
        /// so clients' chips follow the host's roster live rather than freezing at the counts
        /// the open RPC carried.
        /// </summary>
        public void NotifyRosterChanged(int playerCount, int domainCount, int[] placedAiDomains)
        {
            if (!IsServer) return;
            RosterChangedOnClients_ClientRpc(playerCount, domainCount,
                                             placedAiDomains ?? System.Array.Empty<int>());
        }

        [ClientRpc]
        void RosterChangedOnClients_ClientRpc(int playerCount, int domainCount, int[] placedAiDomains)
        {
            if (IsServer) return; // The host already drew it locally
            OnRosterChangedOnClient?.Invoke(playerCount, domainCount, placedAiDomains);
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

            Debug.Log($"[ArcadeConfigSync] Player {clientId} confirmed ready ({_readyClients.Count}/{_expectedHumanCount})");

            // Notify all clients of the updated ready count
            SyncReadyCount_ClientRpc(_readyClients.Count, _expectedHumanCount);

            if (_readyClients.Count >= _expectedHumanCount)
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
