using CosmicShore.Game.Arcade;
using TMPro;
using UnityEngine;
using CosmicShore.Game.Ship;
using CosmicShore.Game.UI.Animations;
using CosmicShore.Game.UI.GameEventFeed;
using CosmicShore.Game.UI.NotificationSystem.Payload;
using CosmicShore.Game.UI.PreGameCinematic;
using CosmicShore.MinigameHUD.View;
namespace CosmicShore.Game.UI
{
    public class CoOpScoreBoard : Scoreboard
    {
        [SerializeField] TMP_Text OppponentScoreTextField;
    }
}