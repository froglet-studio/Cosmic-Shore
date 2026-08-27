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
        [Inject] GameDataSO gameData;

        [Inject] SO_GameList gameList;

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
