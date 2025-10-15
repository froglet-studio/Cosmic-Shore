using UnityEngine;

namespace CosmicShore.Game
{
    public class NetworkScoreBoard : Scoreboard
    {
        protected override bool TryGetWinner(out IRoundStats roundStats, out bool localIsWinner) =>
            gameData.TryGetWinnerForMultiplayer(out roundStats, out localIsWinner);
    }
}