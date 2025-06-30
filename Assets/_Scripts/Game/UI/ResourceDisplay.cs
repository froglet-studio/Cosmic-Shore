using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public class ResourceDisplay : MonoBehaviour
    {
        [SerializeField] bool verboseLogging;
        [SerializeField] Image backgroundImage;
        [SerializeField] Sprite backgroundSprite;
        [SerializeField] TMP_Text fuelLevelText;
        [SerializeField] protected List<Sprite> fuelLevelImages;
        [SerializeField] protected Image fuelLevelImage;

        [SerializeField] private BoostFillAnimator resourceFillAnimator;

        readonly float maxLevel = 1f;
        float currentLevel;

        void Start()
        {
            backgroundImage.sprite = backgroundSprite;
            fuelLevelImage.sprite = fuelLevelImages[0];
            currentLevel = 0;
        }

        public void UpdateDisplay(float newResourceLevel)
        {
            currentLevel = Mathf.Clamp(newResourceLevel, 0, maxLevel);

            int maxIndex = fuelLevelImages.Count - 1;
            float percentOfFull = currentLevel / maxLevel;
            int index = Mathf.FloorToInt(percentOfFull * maxIndex);

            if (verboseLogging)
                Debug.Log($"ResourceDisplay.UpdateDisplay – percent:{percentOfFull:F2}, index:{index}");

            fuelLevelImage.sprite = fuelLevelImages[Mathf.Clamp(index, 0, maxIndex)];
            if (fuelLevelText) fuelLevelText.text = (currentLevel * 100f).ToString("F0");
        }

        public void AnimateFillDown(float duration, float from)
        {
            if (resourceFillAnimator == null)
            {
                Debug.LogWarning("ResourceDisplay: missing BoostFillAnimator!");
                return;
            }
            resourceFillAnimator.SetFill(from);
            resourceFillAnimator.AnimateFillDown(duration, from);
        }

        public void AnimateFillUp(float duration, float to)
        {
            if (resourceFillAnimator == null)
            {
                Debug.LogWarning("ResourceDisplay: missing BoostFillAnimator!");
                return;
            }
            resourceFillAnimator.AnimateFillUp(duration, to);
        }
    }
}
