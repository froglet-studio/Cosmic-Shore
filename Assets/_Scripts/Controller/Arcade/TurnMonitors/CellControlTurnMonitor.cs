using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.UI;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class CellControlTurnMonitor : TurnMonitor
    {
        public override bool CheckForEndOfTurn()
        {
            return gameData.GetControllingTeamStatsBasedOnVolumeRemaining().Item1 == gameData.LocalPlayer.Domain;
        }
    }
    
}