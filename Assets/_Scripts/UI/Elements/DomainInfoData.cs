using System.Collections.Generic;
using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
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

        [Header("Avatar Icon (legacy single-avatar slot, kept for back-compat)")]
        [Tooltip("The AvatarIcon container GameObject. Enabled only on the selected domain.")]
        [SerializeField] private GameObject avatarIconContainer;

        [Tooltip("The child Image inside avatarIconContainer that displays the avatar sprite.")]
        [SerializeField] private Image avatarIconImage;

        [Header("Avatar Strip (multi-player live display)")]
        [Tooltip("LayoutGroup container for per-player avatar chips. Leave null on screens that " +
                 "still use the legacy single-avatar slot above.")]
        [SerializeField] private HorizontalLayoutGroup avatarStrip;

        [Tooltip("Prefab for one avatar chip. Pooled — never destroyed at runtime.")]
        [SerializeField] private DomainAvatarChip chipPrefab;

        readonly List<DomainAvatarChip> _chipPool = new();

        public Domains Domain => domain;
        public Button Button => button;

        public void SetSelected(bool selected)
        {
            if (backgroundImage)
                backgroundImage.sprite = selected ? selectedSprite : unselectedSprite;

            if (labelText)
                labelText.color = selected ? selectedTextColor : unselectedTextColor;

            if (avatarIconContainer)
                avatarIconContainer.SetActive(selected);
        }

        /// <summary>
        /// Legacy single-avatar setter. Still used by menu-side code paths that haven't
        /// migrated to the avatar strip (vessel selection, single-player flows). New
        /// code should prefer <see cref="SetAvatars"/>.
        /// </summary>
        public void SetAvatarSprite(Sprite sprite)
        {
            if (!avatarIconImage) return;

            if (sprite != null)
            {
                avatarIconImage.sprite = sprite;
                avatarIconImage.enabled = true;
            }
            else
            {
                avatarIconImage.enabled = false;
            }
        }

        /// <summary>
        /// Renders the per-player avatars currently attached to this domain. Pool grows
        /// on demand; chips are never destroyed (just disabled when unused). Pass an
        /// empty list to clear.
        /// </summary>
        public void SetAvatars(IReadOnlyList<(Sprite sprite, bool isLocal)> entries)
        {
            if (!avatarStrip || !chipPrefab)
                return;

            int needed = entries?.Count ?? 0;

            // Grow pool as needed.
            while (_chipPool.Count < needed)
            {
                var chip = Instantiate(chipPrefab, avatarStrip.transform);
                chip.Hide();
                _chipPool.Add(chip);
            }

            // Populate first N, hide the rest.
            for (int i = 0; i < _chipPool.Count; i++)
            {
                if (i < needed)
                {
                    var entry = entries[i];
                    _chipPool[i].Set(entry.sprite, entry.isLocal);
                }
                else
                {
                    _chipPool[i].Hide();
                }
            }
        }
    }
}
