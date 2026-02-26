using CosmicShore.Game.Environment;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Game.IO;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Multiplayer;
using CosmicShore.Game.Player;
using CosmicShore.Game.Prisms;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.Ship;
using CosmicShore.Game.UI;
using CosmicShore.Models.Enums;
using CosmicShore.Models.ScriptableObjects;
using CosmicShore.UI.Modals;
using CosmicShore.Utility.DataContainers;
using CosmicShore.Utility.Effects;
using CosmicShore.Utility.SOAP;
using CosmicShore.VesselHUD.Controller;
using CosmicShore.VesselHUD.Interfaces;
using CosmicShore.VesselHUD.View;
namespace CosmicShore.Game.Managers
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
            gameData.ResetStatsDataForReplay();
            RestartGame_ClientRpc();
        }

        [ClientRpc]
        void RestartGame_ClientRpc()
        {
            InvokeOnResetForReplay();

            if (CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();
        }
    }
}