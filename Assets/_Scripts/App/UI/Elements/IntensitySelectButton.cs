using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore
{
    public class IntensitySelectButton : MonoBehaviour
    {
        [SerializeField] Image BorderImage;
        [SerializeField] Image IntensityImage;
        [SerializeField] TMP_Text IntensityText;
        [SerializeField] Sprite BorderSpriteSelected;
        [SerializeField] Sprite BorderSpriteUnselected;
        [SerializeField] Sprite IntensityOneSpriteSelected;
        [SerializeField] Sprite IntensityOneSpriteUnselected;
        [SerializeField] Sprite IntensityTwoSpriteSelected;
        [SerializeField] Sprite IntensityTwoSpriteUnselected;
        [SerializeField] Sprite IntensityThreeSpriteSelected;
        [SerializeField] Sprite IntensityThreeSpriteUnselected;
        [SerializeField] Sprite IntensityFourSpriteSelected;
        [SerializeField] Sprite IntensityFourSpriteUnselected;
        [SerializeField] Color32 IntensityColorSelected;
        [SerializeField] Color32 IntensityColorUnselected;
        [SerializeField] Color32 IntensityColorActive;
        [SerializeField] Color32 IntensityColorInactive;

        Sprite IntensitySpriteActive;
        Sprite IntensitySpriteInactive;
        bool Selected;

        public void SetIntensityLevel(int level)
        {
            switch (level)
            {
                case 1:
                    IntensitySpriteActive = IntensityOneSpriteSelected;
                    IntensitySpriteInactive = IntensityOneSpriteUnselected;
                    IntensityText.text = "1";
                    break;
                case 2:
                    IntensitySpriteActive = IntensityTwoSpriteSelected;
                    IntensitySpriteInactive = IntensityTwoSpriteUnselected;
                    IntensityText.text = "2";
                    break;
                case 3:
                    IntensitySpriteActive = IntensityThreeSpriteSelected;
                    IntensitySpriteInactive = IntensityThreeSpriteUnselected;
                    IntensityText.text = "3";
                    break;
                case 4:
                    IntensitySpriteActive = IntensityFourSpriteSelected;
                    IntensitySpriteInactive = IntensityFourSpriteUnselected;
                    IntensityText.text = "4";
                    break;
            }
        }

        public void SetSelected(bool selected)
        {
            Selected = selected;
            if (selected)
            {
                BorderImage.sprite = BorderSpriteSelected;
                IntensityImage.sprite = IntensitySpriteActive;
                IntensityText.color = IntensityColorSelected;
            }
            else
            {
                BorderImage.sprite = BorderSpriteUnselected;
                IntensityImage.sprite = IntensitySpriteInactive;
                IntensityText.color = IntensityColorUnselected;
            }
        }

        public void SetActive(bool active)
        {
            if (active)
            {
                BorderImage.color = IntensityColorActive;
                IntensityImage.color = IntensityColorActive;
                IntensityText.color = Selected ? IntensityColorSelected : IntensityColorUnselected;
            }
            else
            {
                BorderImage.color = IntensityColorInactive;
                IntensityImage.color = IntensityColorInactive;
                IntensityText.color = IntensityColorInactive;
            }
        }
    }
}