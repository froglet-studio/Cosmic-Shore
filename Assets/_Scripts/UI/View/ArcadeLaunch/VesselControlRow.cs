using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One line of the launch panel's controls block: what the control is, what it does, and which
    /// element upgrades it.
    ///
    /// <para>A row draws NOTHING it was handed as null — a missing chip glyph leaves the chip
    /// blank rather than substituting another control's artwork, which is the same honesty rule the
    /// ability lockup's control chip follows (<c>Docs/ABILITY_LOCKUP.md</c>): a glyph shown to a
    /// player whose device does not have that control is misinformation, and blank is the correct
    /// state, not a missing asset.</para>
    ///
    /// <para>The row owns no clock. <see cref="VesselControlsPanel"/> drives every row from one
    /// phase so the block reads as a single sweep rather than N independent loops — and so a panel
    /// that is not on screen costs nothing.</para>
    /// </summary>
    public class VesselControlRow : MonoBehaviour
    {
        [Header("Ability")]
        [SerializeField, Tooltip("The ability's own artwork. Left at the prefab's sprite when the " +
                                 "vessel authors none for this slot.")]
        Image abilityIcon;

        [SerializeField, Tooltip("Optional: the element that upgrades this ability, drawn as its " +
                                 "petal — the same mark the HUD's flower row uses, so the launch " +
                                 "panel and the in-game HUD say the same thing.")]
        Image elementPetal;

        [SerializeField, Tooltip("Ability name.")] TMP_Text nameText;
        [SerializeField, Tooltip("Optional one-line description.")] TMP_Text descriptionText;

        [Header("Control chip")]
        [SerializeField, Tooltip("Pad artwork for the control this ability rides. Its GameObject " +
                                 "is switched off when the fleet authors no glyph for the control " +
                                 "(a passive ability has no button at all).")]
        Image chipGlyph;

        [SerializeField, Tooltip("Keyboard label for the same control. Switched off when the " +
                                 "control has no keyboard equivalent.")]
        TMP_Text chipLabel;

        [Header("Animation")]
        [SerializeField, Tooltip("Scale the icon reaches at the peak of this row's turn in the " +
                                 "sweep. 1 disables the pulse.")]
        [Min(1f)] float highlightScale = 1.12f;

        [SerializeField, Tooltip("Alpha of a row that is NOT this sweep's turn. 1 disables the " +
                                 "dim, leaving the sweep as a pulse only.")]
        [Range(0f, 1f)] float restAlpha = 0.55f;

        CanvasGroup _group;
        Vector3 _iconRestScale = Vector3.one;
        Vector3 _chipRestScale = Vector3.one;
        bool _capturedRestScales;

        /// <summary>The element this row was bound to — the panel's sweep key.</summary>
        public Element Element { get; private set; } = Element.None;

        void Awake() => CaptureRestScales();

        void CaptureRestScales()
        {
            if (_capturedRestScales) return;
            if (abilityIcon) _iconRestScale = abilityIcon.rectTransform.localScale;
            if (chipGlyph) _chipRestScale = chipGlyph.rectTransform.localScale;
            _capturedRestScales = true;
        }

        /// <summary>
        /// Fill the row. Every argument is allowed to be null/empty: the row switches off the parts
        /// it was given nothing for and keeps the rest.
        /// </summary>
        public void Bind(Element element, string abilityName, string description,
                         Sprite icon, Sprite petal, Sprite padGlyph, string keyboardLabel)
        {
            CaptureRestScales();
            Element = element;

            if (abilityIcon && icon) abilityIcon.sprite = icon;

            if (elementPetal)
            {
                elementPetal.gameObject.SetActive(petal);
                if (petal) elementPetal.sprite = petal;
            }

            if (nameText) nameText.text = abilityName ?? string.Empty;

            if (descriptionText)
            {
                bool hasDescription = !string.IsNullOrWhiteSpace(description);
                descriptionText.gameObject.SetActive(hasDescription);
                if (hasDescription) descriptionText.text = description.Trim();
            }

            if (chipGlyph)
            {
                chipGlyph.gameObject.SetActive(padGlyph);
                if (padGlyph) chipGlyph.sprite = padGlyph;
            }

            if (chipLabel)
            {
                bool hasLabel = !string.IsNullOrWhiteSpace(keyboardLabel);
                chipLabel.gameObject.SetActive(hasLabel);
                if (hasLabel) chipLabel.text = keyboardLabel;
            }

            gameObject.SetActive(true);
            SetSweep(0f);
        }

        /// <summary>
        /// This row's share of the panel's sweep, 0 (at rest) to 1 (its turn). Scaling and alpha
        /// are the whole animation — nothing here moves a rect, so a row can sit inside a layout
        /// group without fighting it.
        /// </summary>
        public void SetSweep(float weight01)
        {
            float w = Mathf.Clamp01(weight01);

            if (abilityIcon)
                abilityIcon.rectTransform.localScale = _iconRestScale * Mathf.LerpUnclamped(1f, highlightScale, w);

            if (chipGlyph && chipGlyph.gameObject.activeSelf)
                chipGlyph.rectTransform.localScale = _chipRestScale * Mathf.LerpUnclamped(1f, highlightScale, w);

            var group = ResolveGroup();
            if (group) group.alpha = Mathf.Lerp(restAlpha, 1f, w);
        }

        CanvasGroup ResolveGroup()
        {
            if (_group) return _group;
            if (!TryGetComponent(out _group))
                _group = gameObject.AddComponent<CanvasGroup>();
            // The block is read-only: it must never eat a click meant for the panel behind it.
            _group.blocksRaycasts = false;
            _group.interactable = false;
            return _group;
        }
    }
}
