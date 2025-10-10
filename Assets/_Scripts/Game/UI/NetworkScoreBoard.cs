using UnityEngine;

namespace CosmicShore.Game
{
    public class NetworkScoreBoard : Scoreboard
    {
        protected override bool TryGetWinner(out IRoundStats roundStats, out bool localIsWinner) =>
            miniGameData.TryGetWinnerForMultiplayer(out roundStats, out localIsWinner);
    }
}