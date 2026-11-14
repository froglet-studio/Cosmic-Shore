using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One medallion on the Artifacts tab's rail (Rev 105 B, Docs/MENU_REDESIGN_B.md section 193): a held artifact's
    /// mark in a round glass with its kind's name under it, or an empty slot for one still to be found.
    /// <see cref="ArkArtifactsPage"/> writes it; the medallion only draws.
    ///
    /// <para>Picked and resting are two authored rings switched on and off, not a colour written here: the theme pass
    /// (<c>UIThemeLive.RepaintKit</c>) re-applies every themed graphic's colour, so a colour set in code would not hold.</para>
    /// </summary>
    public class ArtifactMedallion : MonoBehaviour
    {
        [SerializeField] private KitButton button;
        [Tooltip("Shown while the medallion holds an artifact.")]
        [SerializeField] private CanvasGroup heldGroup;
        [Tooltip("Shown while the slot is empty: an artifact still to be found.")]
        [SerializeField] private CanvasGroup emptyGroup;
        [Tooltip("The artifact's mark (its kind's glyph).")]
        [SerializeField] private Image glyph;
        [SerializeField] private TMP_Text nameText;
        [Tooltip("The ring while not picked.")]
        [SerializeField] private Graphic restRing;
        [Tooltip("The ring while picked: the selection's thin line.")]
        [SerializeField] private Graphic pickedRing;
        [Tooltip("The name's group: full while picked or while nothing is picked, dimmed otherwise.")]
        [SerializeField] private CanvasGroup nameGroup;
        [Tooltip("The name's alpha while another medallion is picked.")]
        [Range(0f, 1f)]
        [SerializeField] private float dimmedNameAlpha = 0.6f;

        public KitButton Button => button;

        /// <summary>Draws a held artifact (<paramref name="held"/>) or an empty slot. <paramref name="picked"/> rings it;
        /// <paramref name="anyPicked"/> dims the names of the others.</summary>
        public void Draw(bool held, Sprite mark, string name, bool picked, bool anyPicked)
        {
            if (heldGroup) KitVisibility.SetShown(heldGroup, held);
            if (emptyGroup) KitVisibility.SetShown(emptyGroup, !held);
            if (button) button.interactable = held;
            if (!held) return;
            if (glyph)
            {
                glyph.sprite = mark;
                glyph.enabled = mark;
            }
            if (nameText) nameText.text = name;
            if (restRing) restRing.enabled = !picked;
            if (pickedRing) pickedRing.enabled = picked;
            if (nameGroup) nameGroup.alpha = picked || !anyPicked ? 1f : dimmedNameAlpha;
        }
    }
}
