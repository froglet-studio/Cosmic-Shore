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
    /// this manager sends a ClientRpc so all party clients also open the modal
    /// (with host-only fields read-only). When the host closes or starts the game,
    /// clients close their modals.
    ///
    /// Place on a scene-level GameObject in Menu_Main alongside the existing
    /// ServerPlayerVesselInitializer hierarchy.
    /// </summary>
    public class ArcadeConfigSyncManager : NetworkBehaviour
    {
        [Inject] GameDataSO gameData;

        [Header("Game List (for client-side game lookup by mode)")]
        [SerializeField] SO_GameList gameList;

        /// <summary>
        /// Raised on clients when the host opens the arcade config modal.
        /// Payload is the GameModes int so clients can look up the SO_ArcadeGame.
        /// </summary>
        public event System.Action<int, int, int, int> OnConfigOpenedOnClient;

        /// <summary>
        /// Raised on clients when the host closes the arcade config modal
        /// or starts the game.
        /// </summary>
        public event System.Action OnConfigClosedOnClient;

        /// <summary>
        /// Raised on clients when the host changes intensity or player count.
        /// </summary>
        public event System.Action<int, int> OnConfigUpdatedOnClient;

        /// <summary>
        /// Called by ArcadeGameConfigureModal on the host when the modal opens.
        /// Sends game mode, intensity, player count, and max players to all clients.
        /// </summary>
        public void NotifyConfigOpened(int gameMode, int intensity, int playerCount, int maxPlayers)
        {
            if (!IsServer) return;
            OpenConfigOnClients_ClientRpc(gameMode, intensity, playerCount, maxPlayers);
        }

        /// <summary>
        /// Called by ArcadeGameConfigureModal on the host when the modal closes
        /// (back button or game start).
        /// </summary>
        public void NotifyConfigClosed()
        {
            if (!IsServer) return;
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
    }
}
