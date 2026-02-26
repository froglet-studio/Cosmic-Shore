using System.Linq;
using CosmicShore.Game.Arcade;
using TMPro;
using UnityEngine;
using CosmicShore.Game.Ship;
using CosmicShore.Game.UI;
using CosmicShore.MinigameHUD.View;
using CosmicShore.Models.Enums;
namespace CosmicShore.Game.UI
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