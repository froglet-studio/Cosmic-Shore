using CosmicShore.Game;
using UnityEngine;
using CosmicShore.Game.Arcade;

namespace CosmicShore
{
    public class CellControlTurnMonitor : TurnMonitor
    {
        // [SerializeField] private Cell monitoredNode;
        // private MiniGame game;
        // Can't use Minigame, use the MiniGameData instead for knowing about playing players.
        // private Teams playerTeam;

        /*private void Start()
        {
            game = GetComponent<MiniGame>();    
            if (game != null && game.LocalPlayer != null)
            {
                playerTeam = game.LocalPlayer.Team;
            }
        }*/

        public override bool CheckForEndOfTurn()
        {
            // return monitoredNode.ControllingTeam() != playerTeam;
            return gameData.GetControllingTeamStatsBasedOnVolumeRemaining().Item1 == gameData.LocalPlayer.Domain;
        }

        /*public override void StartMonitor()
        {
            if (game != null && game.LocalPlayer != null)
            {
                playerTeam = game.LocalPlayer.Team;
            }
            base.StartMonitor();
        }*/
    }
    
}