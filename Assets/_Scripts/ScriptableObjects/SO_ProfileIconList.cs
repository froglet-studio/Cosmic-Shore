using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Profile Icon List", menuName = "ScriptableObjects/ProfileIconList", order = 20)]
    public class SO_ProfileIconList : ScriptableObject
    {
        [SerializeField] public List<ProfileIcon> profileIcons;

        [Header("Fallback")]
        [Tooltip("Shown when an avatar id cannot be resolved - an unset id (0), a profile " +
                 "that has not loaded yet, or a remote player whose avatar has not replicated. " +
                 "MUST be visually distinct from every real icon: authored ids start at 1, so " +
                 "before this existed every resolver fell back to profileIcons[0] and rendered " +
                 "'unknown' as icon #1 - indistinguishable from a player who actually picked it, " +
                 "which is why every other avatar bug was invisible. Leave unassigned and " +
                 "unresolved avatars render as nothing, which is still honest; assigning a " +
                 "distinct placeholder is better.")]
        [SerializeField] private Sprite unknownIcon;

        /// <summary>
        /// The placeholder for an unresolvable avatar id. May be <c>null</c> if the
        /// asset has not had one assigned - callers must tolerate that.
        /// </summary>
        public Sprite UnknownIcon => unknownIcon;

        /// <summary>
        /// Lazily built id → sprite map. Every consumer previously ran its own
        /// linear scan over the list (there were seven copies, with divergent
        /// fallbacks), on every row repaint.
        /// </summary>
        private Dictionary<int, Sprite> _byId;

        /// <summary>
        /// Looks up an icon by its authored id.
        /// </summary>
        /// <returns><c>true</c> when the id maps to an authored icon.</returns>
        public bool TryGetIcon(int avatarId, out Sprite sprite)
        {
            EnsureCache();
            return _byId.TryGetValue(avatarId, out sprite) && sprite != null;
        }

        /// <summary>
        /// Resolves an avatar id to a sprite, falling back to
        /// <see cref="UnknownIcon"/>.
        ///
        /// <para>
        /// The single resolution point for the whole project. Do NOT reintroduce
        /// a local scan with a <c>profileIcons[0]</c> fallback: that silently
        /// renders every unknown avatar as the first authored icon, so a missing
        /// or unreplicated avatar looks exactly like a real choice.
        /// </para>
        /// </summary>
        public Sprite Resolve(int avatarId) =>
            TryGetIcon(avatarId, out var sprite) ? sprite : unknownIcon;

        /// <summary>
        /// First authored icon id, for seeding a new profile. Skips any
        /// sentinel-valued entry so a fresh player is never handed the
        /// "unknown" id as their actual choice.
        /// </summary>
        public int FirstSelectableId()
        {
            if (profileIcons == null) return 0;
            foreach (var icon in profileIcons)
                if (icon.Id > 0) return icon.Id;
            return 0;
        }

        private void EnsureCache()
        {
            if (_byId != null) return;

            _byId = new Dictionary<int, Sprite>();
            if (profileIcons == null) return;

            foreach (var icon in profileIcons)
            {
                if (icon.Id <= 0) continue;   // 0 and below are sentinels, not choices
                _byId[icon.Id] = icon.IconSprite;
            }
        }

        private void OnValidate() => _byId = null;
        private void OnEnable()   => _byId = null;
    }

    [System.Serializable]
    public struct ProfileIcon
    {
        public string Name;
        public int Id;
        public Sprite IconSprite;

        public ProfileIcon(string name, int id, Sprite iconSprite )
        {
            Name = name;
            Id = id;
            IconSprite = iconSprite;
        }
    }
}
