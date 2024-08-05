using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class HangarGameplayParameterDisplay : MonoBehaviour
    {
        [SerializeField] TMP_Text LeftLabel;
        [SerializeField] TMP_Text RightLabel;
        [SerializeField] Image SliderRail;
        [SerializeField] Image SliderThumb;

        /// <summary>
        /// Assigns a single GameplayerParameter to be displayed in the HangarGameplayParameterDisplay UI
        /// </summary>
        /// <param name="gameplayParameter">The GameplayParameter to display in the UI</param>
        public void AssignGameParameter(GameplayParameter gameplayParameter)
        {
            LeftLabel.text = gameplayParameter.LeftHandLabel;
            RightLabel.text = gameplayParameter.RightHandLabel;
            SliderThumb.rectTransform.localPosition = new Vector3(SliderRail.rectTransform.rect.width * gameplayParameter.Value, 0, 0);
        }
    }
}