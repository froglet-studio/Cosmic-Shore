using CosmicShore.Game.Arcade;
using TMPro;
using UnityEngine;
using CosmicShore.Game.Ship;
using CosmicShore.Game.UI;
using CosmicShore.MinigameHUD.View;
namespace CosmicShore.Game.UI
{
    public class CoOpScoreBoard : Scoreboard
    {
        [SerializeField] TMP_Text OppponentScoreTextField;
    }
}