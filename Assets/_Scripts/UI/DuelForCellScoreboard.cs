using System.Linq;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using CosmicShore.UI;
using CosmicShore.Data;
namespace CosmicShore.UI
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