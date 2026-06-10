using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class MultiplayerFreestyleController : MultiplayerMiniGameControllerBase
    {
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.OnClientReady.OnRaised += OnClientReady;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            gameData.OnClientReady.OnRaised -= OnClientReady;
        }

        void OnClientReady() => gameData.SetNonOwnerPlayersActiveInNewClient();

        protected override void OnCountdownTimerEnded()
        {
            OnCountdownTimerEnded_ServerRpc(gameData.LocalPlayer.Name);
        }

        [ServerRpc(RequireOwnership = false)]
        void OnCountdownTimerEnded_ServerRpc(FixedString128Bytes playerName)
        {
            OnCountdownTimerEnded_ClientRpc(playerName);
        }

        [ClientRpc]
        void OnCountdownTimerEnded_ClientRpc(FixedString128Bytes playerName)
        {
            string name = playerName.ToString();
            gameData.SetNewPlayerActive(name);
            gameData.StartTurn();

            // If it's the local player who just activated, force their input live
            // so a replay-race leaves them controllable.
            if (gameData.LocalPlayer != null && gameData.LocalPlayer.Name == name)
                EnsureLocalHumanCanMove();
        }
    }
}
