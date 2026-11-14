using CosmicShore.ScriptableObjects;
using CosmicShore.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Gameplay;
namespace CosmicShore.UI
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
        bool _isActive = true;

        /// <summary>Whether this level is the one picked. Never true while locked.</summary>
        public bool IsSelected => Selected;

        /// <summary>Whether the level is still to be earned. A locked button stays pressable and
        /// raises <see cref="OnLockedSelect"/> so its owner can say why.</summary>
        public bool IsLocked => _isLocked;

        /// <summary>Whether the open card offers this level at all (inside its Min..Max range).</summary>
        public bool IsActiveLevel => _isActive;

        /// <summary>What earns this level while it is locked ("Play Intensity 2 to unlock"), or null.</summary>
        public string LockedGoal { get; private set; }

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
            DrawLevel(intensity);
        }

        public void SetSelected(bool selected)
        {
            if (_isLocked) return;

            Selected = selected;
            if (selected)
                selectedIntensityCount.Value = Intensity;
            DrawSelected(selected);
        }

        public void SetActive(bool active)
        {
            GetComponent<Button>().enabled = active;
            _isLocked = false;
            _isActive = active;
            DrawActive(active);
        }

        /// <summary>
        /// Sets the button to a locked state. The button remains clickable
        /// but fires OnLockedSelect instead of OnSelect and uses locked visuals.
        /// </summary>
        public void SetLocked(bool locked)
        {
            _isLocked = locked;
            if (locked)
            {
                // Keep the button enabled so clicks fire OnLockedSelect
                GetComponent<Button>().enabled = true;
                Selected = false;
            }
            DrawLocked(locked);
        }

        /// <summary>What earns this level, shown by a button that has room to say it.</summary>
        public void SetLockedGoal(string goal)
        {
            LockedGoal = goal;
            DrawLocked(_isLocked);
        }

        // ── Drawing hooks ───────────────────────────────────────────────────────
        // The state above is the contract the modal drives; how a state LOOKS is these hooks. The
        // legacy button draws through its sprite pairs and Color32 fields; the style-B node
        // (KitIntensityNode) overrides every hook and leaves those fields empty.

        protected virtual void DrawLevel(int intensity)
        {
            switch (intensity)
            {
                case 1:
                    IntensitySpriteActive = IntensityOneSpriteSelected;
                    IntensitySpriteInactive = IntensityOneSpriteUnselected;
                    break;
                case 2:
                    IntensitySpriteActive = IntensityTwoSpriteSelected;
                    IntensitySpriteInactive = IntensityTwoSpriteUnselected;
                    break;
                case 3:
                    IntensitySpriteActive = IntensityThreeSpriteSelected;
                    IntensitySpriteInactive = IntensityThreeSpriteUnselected;
                    break;
                case 4:
                    IntensitySpriteActive = IntensityFourSpriteSelected;
                    IntensitySpriteInactive = IntensityFourSpriteUnselected;
                    break;
                default:
                    return;
            }
            IntensityText.text = intensity.ToString();
        }

        protected virtual void DrawSelected(bool selected)
        {
            if (selected)
            {
                BorderImage.sprite = BorderSpriteSelected;
                IntensityImage.sprite = IntensitySpriteActive;
                IntensityText.color = ColorTrace.Retrace(ColorTrace.Ui, IntensityColorSelected);
            }
            else
            {
                BorderImage.sprite = BorderSpriteUnselected;
                IntensityImage.sprite = IntensitySpriteInactive;
                IntensityText.color = ColorTrace.Retrace(ColorTrace.Ui, IntensityColorUnselected);
            }
        }

        protected virtual void DrawActive(bool active)
        {
            // Every state's colour in the worn colour set (ColorTrace).
            if (active)
            {
                Color level = ColorTrace.Retrace(ColorTrace.Ui, IntensityColorActive);
                BorderImage.color = level;
                IntensityImage.color = level;
                IntensityText.color = ColorTrace.Retrace(ColorTrace.Ui, Selected ? IntensityColorSelected : IntensityColorUnselected);
            }
            else
            {
                Color inactive = ColorTrace.Retrace(ColorTrace.Ui, IntensityColorInactive);
                BorderImage.color = inactive;
                IntensityImage.color = inactive;
                IntensityText.color = inactive;
            }
        }

        protected virtual void DrawLocked(bool locked)
        {
            // An unlock is drawn by the SetActive / SetSelected that always follow it.
            if (!locked) return;

            Color lockColor = ColorTrace.Retrace(ColorTrace.Ui, IntensityColorLocked.a > 0 ? IntensityColorLocked : IntensityColorInactive);
            BorderImage.sprite = BorderSpriteUnselected;
            BorderImage.color = lockColor;
            IntensityImage.sprite = IntensitySpriteInactive;
            IntensityImage.color = lockColor;
            IntensityText.color = lockColor;
        }
    }
}
