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

            // bucket the percent of full and use it as an index into the sprite list
            int maxIndex = fuelLevelImages.Count - 1;
            float percentOfFull = currentLevel / maxLevel;
            int index = (int)Mathf.Floor(percentOfFull * maxIndex);

            if (verboseLogging)
                Debug.Log($"FuelBar.UpdateFuelBarDisplay - percentOfFull:{percentOfFull}, maxIndex:{maxIndex}, index:{index}");

            fuelLevelImage.sprite = fuelLevelImages[index];
            if (fuelLevelText) fuelLevelText.text = (currentLevel * 100f).ToString("F0");
        }
    }
}