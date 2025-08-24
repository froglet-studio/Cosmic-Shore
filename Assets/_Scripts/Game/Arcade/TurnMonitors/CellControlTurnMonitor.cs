using CosmicShore.Game;
using UnityEngine;
using CosmicShore.Game.Arcade;

namespace CosmicShore
{
    public class CellControlTurnMonitor : TurnMonitor
    {
        [SerializeField] private Cell monitoredNode;
        // private MiniGame game;
        // Can't use Minigame, use the MiniGameData instead for knowing about playing players.

        private Teams playerTeam;

        /*private void Start()
        {
            game = GetComponent<MiniGame>();    
            if (game != null && game.ActivePlayer != null)
            {
                playerTeam = game.ActivePlayer.Team;
            }
        }*/

        public override bool CheckForEndOfTurn()
        {
            return monitoredNode.ControllingTeam != playerTeam;
        }

        protected override void StartTurn()
        {
            /*if (game != null && game.ActivePlayer != null)
            {
                playerTeam = game.ActivePlayer.Team;
            }*/
        }
    }
    
}