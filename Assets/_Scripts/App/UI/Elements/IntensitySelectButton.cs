using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class IntensitySelectButton : MonoBehaviour
    {
        [SerializeField] Image BorderImage;
        [SerializeField] Image IntensityImage;
        [SerializeField] TMP_Text IntensityText;
        [SerializeField] Sprite BorderSpriteActive;
        [SerializeField] Sprite BorderSpriteInactive;
        [SerializeField] Sprite IntensityOneSpriteActive;
        [SerializeField] Sprite IntensityOneSpriteInactive;
        [SerializeField] Sprite IntensityTwoSpriteActive;
        [SerializeField] Sprite IntensityTwoSpriteInactive;
        [SerializeField] Sprite IntensityThreeSpriteActive;
        [SerializeField] Sprite IntensityThreeSpriteInactive;
        [SerializeField] Sprite IntensityFourSpriteActive;
        [SerializeField] Sprite IntensityFourSpriteInactive;
        [SerializeField] Color32 IntensityTextColorActive;
        [SerializeField] Color32 IntensityTextColorInactive;

        Sprite IntensitySpriteActive;
        Sprite IntensitySpriteInactive;

        public void SetIntensityLevel(int level)
        {
            switch (level)
            {
                case 1:
                    IntensitySpriteActive = IntensityOneSpriteActive;
                    IntensitySpriteInactive = IntensityOneSpriteInactive;
                    break;
                case 2:
                    IntensitySpriteActive = IntensityTwoSpriteActive;
                    IntensitySpriteInactive = IntensityTwoSpriteInactive;
                    break;
                case 3:
                    IntensitySpriteActive = IntensityThreeSpriteActive;
                    IntensitySpriteInactive = IntensityThreeSpriteInactive;
                    break;
                case 4:
                    IntensitySpriteActive = IntensityFourSpriteActive;
                    IntensitySpriteInactive = IntensityFourSpriteInactive;
                    break;
            }
        }

        public void SetActive(bool active)
        {
            if (active)
            {
                BorderImage.sprite = BorderSpriteActive;
                IntensityImage.sprite = IntensitySpriteActive;
                IntensityText.color = IntensityTextColorActive;
            }
            else
            {
                BorderImage.sprite = BorderSpriteInactive;
                IntensityImage.sprite = IntensitySpriteInactive;
                IntensityText.color = IntensityTextColorInactive;
            }
        }
    }
}