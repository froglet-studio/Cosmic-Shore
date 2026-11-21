using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Per-domain tile inside the configure modal. Owns its own visual state
    /// (selected sprite + label color) and exposes the strip Transform that
    /// <see cref="ArcadeGameConfigureModal"/> reparents avatar chips into.
    /// Chip lifecycle and instancing are entirely the modal's responsibility.
    /// </summary>
    public class DomainInfoData : MonoBehaviour
    {
        [Header("Domain")]
        [SerializeField] private Domains domain = Domains.Blue;

        [Header("Button")]
        [SerializeField] private Button button;

        [Header("Background Sprites")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Sprite unselectedSprite;

        [Header("Label")]
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color unselectedTextColor = Color.gray;

        [Header("Avatar Strip")]
        [Tooltip("Container under which the modal reparents DomainAvatarChips for " +
                 "each player currently picking this domain. Modal owns the chip lifecycle.")]
        [SerializeField] private HorizontalOrVerticalLayoutGroup avatarStrip;

        [Header("Add AI (optional)")]
        [Tooltip("Places one AI on THIS domain (host only). The modal wires its click and shows it only " +
                 "while the match has a spare seat. Leave empty on a tile with no per-domain Add AI.")]
        [SerializeField] private Button addAiButton;

        public Domains Domain => domain;
        public Button Button => button;
        public Button AddAIButton => addAiButton;
        public Transform AvatarStripTransform => avatarStrip ? avatarStrip.transform : null;

        /// <summary>Whether this is the local player's domain, as last set by the modal.</summary>
        public bool IsSelected { get; private set; }

        /// <summary>Whether the tile takes taps, as last set by the modal.</summary>
        public bool IsInteractable { get; private set; } = true;

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            DrawSelected(selected);
        }

        public void SetInteractable(bool interactable)
        {
            IsInteractable = interactable;
            if (button) button.interactable = interactable;
            DrawInteractable(interactable);
        }

        /// <summary>
        /// The domain's open seat: <paramref name="open"/> while the match can still seat someone
        /// here, and <paramref name="canAddAI"/> while the local player may fill it with an AI (the
        /// host, outside a weekly challenge). A tile without an open-seat row shows only the button.
        /// </summary>
        public virtual void SetOpenSeat(bool open, bool canAddAI)
        {
            if (addAiButton) addAiButton.gameObject.SetActive(open && canAddAI);
        }

        // ── Drawing hooks (the style-B tile, KitDomainTile, overrides both) ─────────────

        protected virtual void DrawSelected(bool selected)
        {
            if (backgroundImage)
                backgroundImage.sprite = selected ? selectedSprite : unselectedSprite;

            if (labelText)
                labelText.color = selected ? selectedTextColor : unselectedTextColor;
        }

        protected virtual void DrawInteractable(bool interactable)
        {
            if (backgroundImage)
            {
                var c = backgroundImage.color;
                backgroundImage.color = new Color(c.r, c.g, c.b, interactable ? 1f : 0.4f);
            }
        }

        void Awake()
        {
            ApplyDomainLabel();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Defer the write - Unity warns against mutating serialized refs
            // (Image.color, TMP_Text.text) during OnValidate itself.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                ApplyDomainLabel();
            };
        }
#endif

        void ApplyDomainLabel()
        {
            if (labelText == null) return;
            var target = domain.ToString().ToUpperInvariant();
            if (labelText.text == target) return;
            labelText.text = target;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(labelText);
#endif
        }
    }
}
