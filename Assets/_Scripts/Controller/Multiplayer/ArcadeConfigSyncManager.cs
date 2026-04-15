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
    /// between host and clients. When the host opens the ArcadeGameConfigureModal,
    /// this manager sends a ClientRpc so all party clients also open the modal.
    ///
    /// Each player (host and clients) independently selects their team and vessel,
    /// then presses Start to confirm. Once all human players have confirmed,
    /// the host automatically launches the game.
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

        /// <summary>
        /// Raised on clients when the host opens the arcade config modal.
        /// Args: gameMode, intensity, playerCount, maxPlayers
        /// </summary>
        public event System.Action<int, int, int, int> OnConfigOpenedOnClient;

        /// <summary>
        /// Raised on all clients when the host closes/cancels the config modal.
        /// </summary>
        public event System.Action OnConfigClosedOnClient;

        /// <summary>
        /// Raised on clients when the host changes intensity or player count.
        /// Args: intensity, playerCount
        /// </summary>
        public event System.Action<int, int> OnConfigUpdatedOnClient;

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

        #region Host → Client: Config modal open/close/update

        /// <summary>
        /// Called by ArcadeGameConfigureModal on the host when the modal opens.
        /// Sends game mode, intensity, player count, and max players to all clients.
        /// </summary>
        public void NotifyConfigOpened(int gameMode, int intensity, int playerCount, int maxPlayers, int humanCount)
        {
            if (!IsServer) return;

            _readyClients.Clear();
            // Use the higher of PartyMembers count and actual connected clients
            // to guard against stale PartyMembers data.
            _expectedHumanCount = Mathf.Max(humanCount, NetworkManager.Singleton.ConnectedClientsIds.Count);

            OpenConfigOnClients_ClientRpc(gameMode, intensity, playerCount, maxPlayers);
        }

        /// <summary>
        /// Called by ArcadeGameConfigureModal on the host when the modal closes
        /// (back button or cancel — NOT game start).
        /// </summary>
        public void NotifyConfigClosed()
        {
            if (!IsServer) return;
            _readyClients.Clear();
            CloseConfigOnClients_ClientRpc();
        }

        /// <summary>
        /// Called by ArcadeGameConfigureModal on the host when intensity or
        /// player count changes so clients see updated read-only values.
        /// </summary>
        public void NotifyConfigUpdated(int intensity, int playerCount)
        {
            if (!IsServer) return;
            UpdateConfigOnClients_ClientRpc(intensity, playerCount);
        }

        [ClientRpc]
        void OpenConfigOnClients_ClientRpc(int gameMode, int intensity, int playerCount, int maxPlayers)
        {
            if (IsServer) return; // Host already has the modal open

            int subscriberCount = OnConfigOpenedOnClient?.GetInvocationList().Length ?? 0;
            Debug.Log($"[ArcadeConfigSync] ClientRpc received — gameMode={gameMode}, subscribers={subscriberCount}");

            if (subscriberCount == 0)
                Debug.LogWarning("[ArcadeConfigSync] No subscribers on OnConfigOpenedOnClient — modal will not open. " +
                                 "Is ArcadeGameConfigureModal.OnEnable() running? Is ModalWindows active?");

            OnConfigOpenedOnClient?.Invoke(gameMode, intensity, playerCount, maxPlayers);
        }

        [ClientRpc]
        void CloseConfigOnClients_ClientRpc()
        {
            if (IsServer) return;
            OnConfigClosedOnClient?.Invoke();
        }

        [ClientRpc]
        void UpdateConfigOnClients_ClientRpc(int intensity, int playerCount)
        {
            if (IsServer) return;
            OnConfigUpdatedOnClient?.Invoke(intensity, playerCount);
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
                Debug.Log("[ArcadeConfigSync] All players ready — launching game");
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
