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
    ///
    /// <para><b>The row is a Toy Box VARIANT ROW, not a card.</b> It wears the same two chamfered
    /// plate sprites, drawn SLICED, with the name bottom-left and one detail line above it — the
    /// shape <c>author_toybox_layout.py</c> settled on for a compact list inside a panel. It is
    /// deliberately not the Toy Box's big 400×250 card: that card's upper two thirds are a baked
    /// PORTRAIT, and a mode has no equivalent. What it has is <c>IconActive</c>, which is the
    /// ARCADE GRID's card art — legacy, and wrong on several of the pool's modes today (Salvo
    /// carries Rampage's picture, Joust carries Duel for the Cell's). Putting that on this row
    /// would be inventing art out of a field that does not mean what the slot wants, which is the
    /// same mistake as the earlier pass that wrote it over the row BACKGROUND and turned every row
    /// into a cyan slab.</para>
    /// </summary>
    public class MaelstromPoolEntry : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField, Tooltip("OPTIONAL, and deliberately left EMPTY unless a row authors a " +
                                 "dedicated icon slot. Wiring it at a row's BACKGROUND writes the " +
                                 "mode's card sprite over that background - which is what turned " +
                                 "every pool row into a cyan slab. A row's own art is the row's; " +
                                 "this entry only fills what it was explicitly given.")]
        Image icon;
        [SerializeField, Tooltip("The mode's DisplayName.")] TMP_Text nameText;

        [SerializeField, Tooltip("The one line above the name: which rung of the ladder the mode " +
                                 "enters on and which hull it locks to, or the lock copy when the " +
                                 "row carries no dedicated lockedText. The Toy Box variant row's " +
                                 "detail line, doing the same job.")]
        TMP_Text detailText;

        [SerializeField, Tooltip("Written on a mode that is not in the pool yet. {0} is the " +
                                 "intensity it unlocks at. Switched off for an unlocked mode. " +
                                 "A row with no slot of its own sends this copy to detailText " +
                                 "instead, so the lock state is never silent.")]
        TMP_Text lockedText;

        [Header("Locked look")]
        [SerializeField, Tooltip("Copy for the locked line. {0} = unlock intensity.")]
        string lockedFormat = "Intensity {0}";

        [SerializeField, Tooltip("Copy when the mode is in no tier at all, so no intensity ever " +
                                 "draws it. An authoring slip - shown rather than hidden so it " +
                                 "is noticed.")]
        string neverUnlockedText = "Not in pool";

        [SerializeField, Tooltip("Copy for the detail line of a mode that IS in the pool. " +
                                 "{0} = the intensity it enters on, {1} = the hull it locks to " +
                                 "(blank when the mode takes any vessel).")]
        string detailFormat = "TIER {0}  ·  {1}";

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

            // Anything else the row draws is the row's own art and is left exactly as authored.

            if (nameText)
                nameText.text = game ? game.DisplayName.ToUpperInvariant() : string.Empty;

            string lockCopy = unlockIntensity > 0
                ? string.Format(lockedFormat, unlockIntensity)
                : neverUnlockedText;

            if (lockedText)
            {
                lockedText.gameObject.SetActive(!unlocked);
                if (!unlocked) lockedText.text = lockCopy;
            }

            if (detailText)
                detailText.text = unlocked
                    ? string.Format(detailFormat, Mathf.Max(1, unlockIntensity), HullOf(game)).Trim()
                    // A row with its own lockedText already says this, so the detail line keeps the
                    // hull rather than repeating the lock.
                    : lockedText ? HullOf(game) : lockCopy.ToUpperInvariant();

            // Alpha only - never a colour. A locked row is the same row, dimmed; tinting it would
            // be this component deciding what the row looks like, which is the mistake above.
            ResolveGroup().alpha = unlocked ? 1f : lockedAlpha;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// The hull a mode locks to, or empty when it takes more than one. Fifteen of the pool's
        /// sixteen modes are single-hull, so this is the one fact about a pool mode that reliably
        /// differs from its neighbour and is worth the line.
        /// </summary>
        static string HullOf(SO_ArcadeGame game)
        {
            if (game == null || game.Vessels == null || game.Vessels.Count != 1) return string.Empty;
            var vessel = game.Vessels[0];
            if (!vessel) return string.Empty;
            return (string.IsNullOrEmpty(vessel.Name) ? vessel.Class.ToString() : vessel.Name)
                .ToUpperInvariant();
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
