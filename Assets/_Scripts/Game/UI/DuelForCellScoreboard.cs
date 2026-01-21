using System.Linq;
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
            DomainStats opponentStats = new();
            
            foreach (var stat in gameData.DomainStatsList.Where(stat => !gameData.IsLocalDomain(stat.Domain)))
            {
                opponentStats = stat;
            }
            
            OppponentScoreTextField.text = ((int)opponentStats.Score).ToString();
        }
    }
}