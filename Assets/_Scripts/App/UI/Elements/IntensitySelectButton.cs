using Obvious.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class IntensitySelectButton : MonoBehaviour
    {
        // TODO: Modify to match player count implementation -> use two lists of sprites
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
        [SerializeField] Color32 IntensityColorLocked = new(120, 60, 60, 255);

        [SerializeField] private IntVariable selectedIntensityCount;

        Sprite IntensitySpriteActive;
        Sprite IntensitySpriteInactive;
        public int Intensity { get; private set; }
        bool Selected;
        bool _isLocked;

        public delegate void SelectDelegate(int intensity);
        public event SelectDelegate OnSelect;
        public event SelectDelegate OnLockedSelect;

        public void Select()
        {
            if (_isLocked)
            {
                OnLockedSelect?.Invoke(Intensity);
                return;
            }

            OnSelect?.Invoke(Intensity);
        }

        public void SetIntensityLevel(int intensity)
        {
            Intensity = intensity;
            switch (intensity)
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
            if (_isLocked) return;

            Selected = selected;
            if (selected)
            {
                BorderImage.sprite = BorderSpriteSelected;
                IntensityImage.sprite = IntensitySpriteActive;
                IntensityText.color = IntensityColorSelected;
                selectedIntensityCount.Value = Intensity;
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
            GetComponent<Button>().enabled = active;
            _isLocked = false;
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

        /// <summary>
        /// Sets the button to a locked state. The button remains clickable
        /// but fires OnLockedSelect instead of OnSelect and uses locked visuals.
        /// </summary>
        public void SetLocked(bool locked)
        {
            _isLocked = locked;
            if (!locked) return;

            // Keep the button enabled so clicks fire OnLockedSelect
            GetComponent<Button>().enabled = true;
            Selected = false;

            Color32 lockColor = IntensityColorLocked.a > 0 ? IntensityColorLocked : IntensityColorInactive;
            BorderImage.sprite = BorderSpriteUnselected;
            BorderImage.color = lockColor;
            IntensityImage.sprite = IntensitySpriteInactive;
            IntensityImage.color = lockColor;
            IntensityText.color = lockColor;
        }
    }
}
