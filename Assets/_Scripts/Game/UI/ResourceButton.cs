using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class ResourceButton : MonoBehaviour
    {
        [SerializeField] Image middleImageContainer;
        public Sprite middleSprite;
        
        [SerializeField] Image gaugeLevelImageContainer;
        [SerializeField] List<Sprite> gaugeLevelImages;

        readonly float maxLevel = 1f;
        float currentLevel;

        void Start()
        {
            middleImageContainer.sprite = middleSprite;
            gaugeLevelImageContainer.sprite = gaugeLevelImages[0];
            currentLevel = 0;
        }

        public void UpdateDisplay(float newChargeLevel)
        {
            currentLevel = Mathf.Clamp(newChargeLevel, 0, maxLevel);

            // bucket the percent of full and use it as an index into the sprite list
            int maxIndex = gaugeLevelImages.Count - 1;
            float percentOfFull = currentLevel / maxLevel;
            int index = (int)Mathf.Floor(percentOfFull * maxIndex);

            gaugeLevelImageContainer.sprite = gaugeLevelImages[index];
        }
    }
}
