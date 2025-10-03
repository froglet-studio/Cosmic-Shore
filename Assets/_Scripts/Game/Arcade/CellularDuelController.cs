namespace CosmicShore.Game.Arcade
{
    public class CellularDuelController : MiniGameControllerBase 
    {
        private void OnEnable()
        {
            miniGameData.OnMiniGameTurnEnd += EndTurn;
        }
        
        private void OnDisable() 
        {
            miniGameData.OnMiniGameTurnEnd -= EndTurn;
        }
    }
}