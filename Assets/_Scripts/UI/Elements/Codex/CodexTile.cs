using System;
using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One tile of the Codex grid (Docs/MENU_REDESIGN_B.md section 88): the entry's picture, its name and a quiet line
    /// under it. Pressing it (or the pad landing on it) opens its page; the open tile's frame is the selection blue and
    /// glows, every other one is a quiet outline. Section 89: its picture is alive (<see cref="CodexPortrait"/>) and the
    /// tile fades and rises in when filled, a step after the one before it.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Codex Tile")]
    public class CodexTile : MonoBehaviour
    {
        [SerializeField] private Button button;
        [Tooltip("The entry's picture, alive (its Codex portrait, a pilot, a hull, an element's flower).")]
        [SerializeField] private CodexPortrait portrait;
        [SerializeField] private TMP_Text nameText;
        [Tooltip("The quiet line under the name. Hidden while empty.")]
        [SerializeField] private TMP_Text subText;
        [Tooltip("The tile's outline: the selection colour on the open tile, the rest colour otherwise.")]
        [SerializeField] private Graphic frame;
        [Tooltip("The glow behind the open tile. Optional.")]
        [SerializeField] private CanvasGroup glow;
        [Tooltip("Everything but the glow: fades and rises in when the tile is filled, a step after the tile before it.")]
        [SerializeField] private CanvasGroup body;

        [Header("Appear")]
        [SerializeField, Min(0.05f)] private float appearSeconds = 0.28f;
        [Tooltip("Delay per tile index, so a grid fills in a wave.")]
        [SerializeField, Min(0f)] private float appearStagger = 0.03f;
        [SerializeField, Min(0f)] private float appearRise = 14f;

        [Header("Colour")]
        [SerializeField] private UIThemeSO theme;
        [SerializeField] private UIColorToken restToken = UIColorToken.SurfaceLight;
        [SerializeField] private UIColorToken selectedToken = UIColorToken.Selection;

        /// <summary>The tile was pressed.</summary>
        public event Action<CodexTile> Picked;

        public int Index { get; private set; }
        float _appear = -1f;
        public Button Button => button;

        void Awake()
        {
            if (button) button.onClick.AddListener(() => Picked?.Invoke(this));
        }

        public void Show(int index, CodexItem item)
        {
            Index = index;
            if (portrait) portrait.Show(item?.Image, item?.Element ?? Element.None, item?.Palette, index * 0.37f);
            if (nameText) nameText.text = item?.Name ?? string.Empty;
            if (subText) subText.text = item?.Sub ?? string.Empty;
            IsSelected = false;
            _appear = -index * appearStagger;
            Appear(0f);
        }

        void Update()
        {
            if (_appear < 0f || _appear > appearSeconds) { if (_appear < 0f) _appear += Time.unscaledDeltaTime; return; }
            _appear += Time.unscaledDeltaTime;
            Appear(Mathf.Clamp01(_appear / appearSeconds));
        }

        void Appear(float k)
        {
            float e = 1f - (1f - k) * (1f - k);    // OutQuad
            if (body)
            {
                body.alpha = e;
                ((RectTransform)body.transform).anchoredPosition = new Vector2(0f, -appearRise * (1f - e));
            }
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
