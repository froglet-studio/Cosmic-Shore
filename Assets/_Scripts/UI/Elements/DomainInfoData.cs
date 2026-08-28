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
        [SerializeField] private HorizontalLayoutGroup avatarStrip;

        public Domains Domain => domain;
        public Button Button => button;
        public Transform AvatarStripTransform => avatarStrip ? avatarStrip.transform : null;

        public void SetSelected(bool selected)
        {
            if (backgroundImage)
                backgroundImage.sprite = selected ? selectedSprite : unselectedSprite;

            if (labelText)
                labelText.color = selected ? selectedTextColor : unselectedTextColor;
        }

        public void SetInteractable(bool interactable)
        {
            if (button) button.interactable = interactable;
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
