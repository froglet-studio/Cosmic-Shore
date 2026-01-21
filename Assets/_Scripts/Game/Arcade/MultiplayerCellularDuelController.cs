using System;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerCellularDuelController : MultiplayerDomainGamesController
    {
        protected override void SetupNewRound()
        {
            bool allowSwap = gameData.RoundsPlayed > 0;
            if (allowSwap)
            {
                if (IsServer)
                    ChangeOwnershipOfVessels();
                
                gameData.SwapVessels();
            }
            
            base.SetupNewRound();
        }
        
        protected override void OnResetForReplay()
        {
            if (IsServer)
                ChangeOwnershipOfVessels();    
            gameData.SwapVessels();
            base.OnResetForReplay();
        }

        void ChangeOwnershipOfVessels()
        {
            var player0 = Player.NppList[0];
            var player1 = Player.NppList[1];
            
            // swap the vessel types from player.NetDefaultVesselType.Value
            if (!player0.Vessel.Transform.TryGetComponent(out NetworkObject no0))
            {
                Debug.LogError("No network object found in vessel. This should not happen!");
                return;
            }
            
            if (!player1.Vessel.Transform.TryGetComponent(out NetworkObject no1))
            {
                Debug.LogError("No network object found in vessel. This should not happen!");
                return;
            }
            
            var no0_OwnerClientId = no0.OwnerClientId;
            no0.ChangeOwnership(no1.OwnerClientId);
            no1.ChangeOwnership(no0_OwnerClientId);
        }
    }
}