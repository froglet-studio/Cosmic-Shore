using CosmicShore.Game;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Core
{
    public class NetworkGameManager : GameManager
    {
        public override void RestartGame()
        {
            RestartGame_ServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        void RestartGame_ServerRpc()
        {
            miniGameData.ResetDataForReplay();
            RestartGame_ClientRpc();
        }

        [ClientRpc]
        void RestartGame_ClientRpc()
        {
            VesselPrismController.NukeTheTrails();
            InvokeOnResetForReplay();
        }
    }
}