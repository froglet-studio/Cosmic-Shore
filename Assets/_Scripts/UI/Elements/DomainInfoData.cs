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

        [Header("Avatar Strip")]
        [Tooltip("LayoutGroup container that holds one DomainAvatarChip per player " +
                 "currently picking this domain. Chips are pooled.")]
        [SerializeField] private HorizontalLayoutGroup avatarStrip;

        [Tooltip("Prefab for one avatar chip. Pooled — never destroyed at runtime.")]
        [SerializeField] private DomainAvatarChip chipPrefab;

        readonly List<DomainAvatarChip> _chipPool = new();
        bool _poolInitialized;

        public Domains Domain => domain;
        public Button Button => button;

        public void SetSelected(bool selected)
        {
            if (backgroundImage)
                backgroundImage.sprite = selected ? selectedSprite : unselectedSprite;

            if (labelText)
                labelText.color = selected ? selectedTextColor : unselectedTextColor;
        }

        /// <summary>
        /// Lazy one-shot adoption. Any DomainAvatarChip GameObjects already parented
        /// under <see cref="avatarStrip"/> in the prefab (e.g., hand-placed in the
        /// Editor for layout preview) are pulled into the managed pool so the runtime
        /// shows/hides them like instantiated ones — instead of stranding them as
        /// permanently-visible siblings while my pool grows underneath.
        /// </summary>
        void EnsurePoolInitialized()
        {
            if (_poolInitialized) return;
            _poolInitialized = true;

            if (avatarStrip == null) return;

            foreach (Transform child in avatarStrip.transform)
            {
                if (child.TryGetComponent(out DomainAvatarChip chip))
                {
                    _chipPool.Add(chip);
                    chip.Hide();
                }
            }
        }

        /// <summary>
        /// Renders the per-player avatars currently attached to this domain. Pool grows
        /// on demand; chips are never destroyed (just disabled when unused). Pass an
        /// empty list to clear.
        /// </summary>
        public void SetAvatars(IReadOnlyList<(Sprite sprite, bool isLocal)> entries)
        {
            if (!avatarStrip)
            {
                Debug.LogWarning($"[DomainInfoData '{name}' (domain={domain})] Avatar Strip " +
                                 "is NOT wired in the inspector. Cannot show chips.");
                return;
            }

            EnsurePoolInitialized();

            int needed = entries?.Count ?? 0;
            int instantiated = 0;

            // Grow pool as needed. chipPrefab is only required when we actually
            // need MORE chips than the pool already provides.
            while (_chipPool.Count < needed)
            {
                if (!chipPrefab)
                {
                    Debug.LogWarning($"[DomainInfoData '{name}' (domain={domain})] Need " +
                                     $"{needed} chips but Chip Prefab is NOT wired and pool " +
                                     $"only has {_chipPool.Count}. Wire DomainAvatarChip.prefab " +
                                     "in the inspector.");
                    break;
                }
                var chip = Instantiate(chipPrefab, avatarStrip.transform);
                chip.Hide();
                _chipPool.Add(chip);
                instantiated++;
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

            Debug.Log($"[DomainInfoData '{name}' (domain={domain})] SetAvatars: needed={needed}, " +
                      $"poolSize={_chipPool.Count}, instantiatedThisCall={instantiated}, " +
                      $"strip={avatarStrip.name}, chipPrefab={(chipPrefab ? chipPrefab.name : "NULL")}");
        }
    }
}
