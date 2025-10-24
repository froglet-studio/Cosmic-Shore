using TMPro;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class DuelForCellMiniGameHUDView : MiniGameHUDView
    {
        [SerializeField] private TMP_Text otherScoreDisplay;
     
        
        
        public void UpdateOpponentScoreUI(string message) 
            => otherScoreDisplay.text = message;
    }
}