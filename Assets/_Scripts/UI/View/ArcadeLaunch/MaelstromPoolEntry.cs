using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One mode in the Maelstrom launch panel's pool list. Two states: <b>in the pool</b> at the
    /// chosen intensity, or <b>locked</b> behind a higher one.
    ///
    /// <para>A locked mode is shown, not hidden. The list's whole job is to say what raising the
    /// intensity buys, and a list that only grows tells the player nothing about what they are
    /// missing — the modes have to be visible for the ladder to read as a ladder.</para>
    /// </summary>
    public class MaelstromPoolEntry : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField, Tooltip("The mode's card icon.")] Image icon;
        [SerializeField, Tooltip("The mode's DisplayName.")] TMP_Text nameText;

        [SerializeField, Tooltip("Written on a mode that is not in the pool yet. {0} is the " +
                                 "intensity it unlocks at. Switched off for an unlocked mode.")]
        TMP_Text lockedText;

        [Header("Locked look")]
        [SerializeField, Tooltip("Copy for the locked line. {0} = unlock intensity.")]
        string lockedFormat = "Intensity {0}";

        [SerializeField, Tooltip("Copy when the mode is in no tier at all, so no intensity ever " +
                                 "draws it. An authoring slip - shown rather than hidden so it " +
                                 "is noticed.")]
        string neverUnlockedText = "Not in pool";

        [SerializeField, Tooltip("Alpha applied to the whole row while locked.")]
        [Range(0f, 1f)] float lockedAlpha = 0.4f;

        CanvasGroup _group;

        /// <summary>The card this row stands for.</summary>
        public SO_ArcadeGame Game { get; private set; }

        /// <summary>
        /// Draw a mode. <paramref name="unlockIntensity"/> is 0 when no tier lists it.
        /// </summary>
        public void Bind(SO_ArcadeGame game, int unlockIntensity, bool unlocked)
        {
            Game = game;

            if (icon)
            {
                var sprite = game ? (game.IconActive ? game.IconActive : game.IconInactive) : null;
                icon.gameObject.SetActive(sprite);
                if (sprite) icon.sprite = sprite;
            }

            if (nameText)
                nameText.text = game ? game.DisplayName : string.Empty;

            if (lockedText)
            {
                lockedText.gameObject.SetActive(!unlocked);
                if (!unlocked)
                    lockedText.text = unlockIntensity > 0
                        ? string.Format(lockedFormat, unlockIntensity)
                        : neverUnlockedText;
            }

            ResolveGroup().alpha = unlocked ? 1f : lockedAlpha;
            gameObject.SetActive(true);
        }

        CanvasGroup ResolveGroup()
        {
            if (_group) return _group;
            if (!TryGetComponent(out _group))
                _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;   // the list is a readout, never a control
            _group.interactable = false;
            return _group;
        }
    }
}
