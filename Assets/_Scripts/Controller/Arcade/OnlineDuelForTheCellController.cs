using System;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class OnlineDuelForTheCellController : MultiplayerDomainGamesController
    {
        protected override void SetupNewRound()
        {
            bool allowSwap = gameData.RoundsPlayed > 0;
            if (allowSwap)
            {
                if (IsServer)
                    ChangeOwnershipOfVessels();
                
                gameData.SwapVessels();

                // SetupNewRound only ever runs on the SERVER (InitializeAfterDelay's server branch,
                // the round-end and replay paths), so the exchange above used to happen on the host
                // alone: ownership moved to the client while the client's Player.Vessel still named
                // its old hull - now host-owned - and the hull it now owned was bound to the host's
                // Player. Every peer has to apply the exchange, as PilotSwap.ApplyLocal does.
                if (IsServer)
                    SwapVessels_ClientRpc();
            }
            
            base.SetupNewRound();
        }

        [ClientRpc]
        void SwapVessels_ClientRpc()
        {
            if (IsServer) return;   // applied synchronously above, in order with the round setup
            if (gameData.Players == null || gameData.Players.Count < 2) return;
            // Two players: the exchange is symmetric, so the order of this peer's roster does not
            // matter.
            gameData.SwapVessels();
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
            var player0 = gameData.Players[0];
            var player1 = gameData.Players[1];
            
            // swap the vessel types from player.NetDefaultVesselType.Value
            if (!player0.Vessel.Transform.TryGetComponent(out NetworkObject no0))
            {
                CSDebug.LogError("No network object found in vessel. This should not happen!");
                return;
            }
            
            if (!player1.Vessel.Transform.TryGetComponent(out NetworkObject no1))
            {
                CSDebug.LogError("No network object found in vessel. This should not happen!");
                return;
            }
            
            var no0_OwnerClientId = no0.OwnerClientId;
            no0.ChangeOwnership(no1.OwnerClientId);
            no1.ChangeOwnership(no0_OwnerClientId);
        }
    }
}