using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One tile of the Codex grid (Docs/MENU_REDESIGN_B.md section 88): the entry's picture, its name and a quiet line
    /// under it. Pressing it (or the pad landing on it) opens its page; the open tile's frame is the selection blue and
    /// glows, every other one is a quiet outline.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Codex Tile")]
    public class CodexTile : MonoBehaviour
    {
        [SerializeField] private Button button;
        [Tooltip("The entry's picture (its Codex portrait, a pilot's portrait, a vessel's icon, an element's petal).")]
        [SerializeField] private Image art;
        [SerializeField] private TMP_Text nameText;
        [Tooltip("The quiet line under the name. Hidden while empty.")]
        [SerializeField] private TMP_Text subText;
        [Tooltip("The tile's outline: the selection colour on the open tile, the rest colour otherwise.")]
        [SerializeField] private Graphic frame;
        [Tooltip("The glow behind the open tile. Optional.")]
        [SerializeField] private CanvasGroup glow;

        [Header("Colour")]
        [SerializeField] private UIThemeSO theme;
        [SerializeField] private UIColorToken restToken = UIColorToken.SurfaceLight;
        [SerializeField] private UIColorToken selectedToken = UIColorToken.Selection;

        /// <summary>The tile was pressed.</summary>
        public event Action<CodexTile> Picked;

        public int Index { get; private set; }
        public Button Button => button;

        void Awake()
        {
            if (button) button.onClick.AddListener(() => Picked?.Invoke(this));
        }

        public void Show(int index, CodexItem item)
        {
            Index = index;
            if (art)
            {
                art.sprite = item?.Image;
                art.enabled = item?.Image;
            }
            if (nameText) nameText.text = item?.Name ?? string.Empty;
            if (subText) subText.text = item?.Sub ?? string.Empty;
            IsSelected = false;
        }

        public bool IsSelected
        {
            set
            {
                if (frame) frame.color = UITheme.Resolve(theme, value ? selectedToken : restToken);
                KitVisibility.SetShown(glow, value);
            }
        }
    }
}
