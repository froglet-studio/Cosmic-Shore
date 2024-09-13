using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game.Arcade;

namespace CosmicShore
{
    public class NodeControlTurnMonitor : TurnMonitor
    {
        [SerializeField] private Node monitoredNode;
        private MiniGame game;

        private Teams playerTeam;

        private void Start()
        {
            game = GetComponent<MiniGame>();    
            if (game != null && game.ActivePlayer != null)
            {
                playerTeam = game.ActivePlayer.Team;
            }
        }

        public override bool CheckForEndOfTurn()
        {
            if (paused) return false;

            if (monitoredNode.ControllingTeam != playerTeam)
            {
                return true;
            }

            return false;
        }

        public override void NewTurn(string playerName)
        {
            if (game != null && game.ActivePlayer != null)
            {
                playerTeam = game.ActivePlayer.Team;
            }
        }

    }
    
}