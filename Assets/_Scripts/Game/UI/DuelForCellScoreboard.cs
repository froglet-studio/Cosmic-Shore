using TMPro;
using UnityEngine;

namespace CosmicShore.Game
{
    public class DuelForCellScoreboard : Scoreboard
    {
        [SerializeField] TMP_Text OppponentScoreTextField;

        protected override void ShowSinglePlayerView()
        {
            base.ShowSinglePlayerView();
            if (gameData.Players.Count > 2)
            {
                Debug.LogError("Cannot have more than two players in Duel Cell");
                return;
            }

            string opponent = null;
            foreach (var player in gameData.Players)
            {
                if (player.IsLocalPlayer)
                    continue;
                opponent = player.Name;
            }

            if (!gameData.TryGetRoundStats(opponent, out IRoundStats stats))
                return;
            
            OppponentScoreTextField.text = ((int)stats.Score).ToString();
        }
    }
}