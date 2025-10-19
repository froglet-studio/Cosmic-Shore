namespace CosmicShore.Game.Arcade
{
    public class CellularDuelController : SinglePlayerMiniGameControllerBase 
    {
        protected override void SetupNewRound()
        {
            ToggleReadyButton(true);
            
            if (roundsPlayed > 0)
                SwapVessels();
            
            base.SetupNewRound();
        }

        void SwapVessels()
        {
            var player0 = gameData.Players[0];
            var player1 = gameData.Players[1];
            
            player0.SetAsAI(!player0.IsInitializedAsAI);
            player1.SetAsAI(!player1.IsInitializedAsAI);
            
            var vessel0 = player0.Vessel;
            var vessel1 = player1.Vessel;
            
            player0.ChangeVessel(vessel1);
            player1.ChangeVessel(vessel0);
            
            vessel0.ChangePlayer(player1);
            vessel1.ChangePlayer(player0);
        }
    }
}