using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One row on the Ark's Squads tab: a squad in the player's list (its name, whose it is, the active mark), or a
    /// friend's squad they can join. <see cref="SquadsPage"/> writes it; the row only draws.
    /// </summary>
    public class SquadRowView : MonoBehaviour
    {
        [SerializeField] private KitButton button;
        [Tooltip("Shown while the row has something in it; an unused row hides.")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text lineText;
        [Tooltip("The mark on the squad PLAY uses (the active one). Optional.")]
        [SerializeField] private Graphic activeMark;

        public KitButton Button => button;

        /// <summary>Draws the row, or hides it when <paramref name="used"/> is false.</summary>
        public void Draw(bool used, string title, string line, bool active)
        {
            if (group) KitVisibility.SetShown(group, used);
            if (button) button.interactable = used;
            if (!used) return;
            if (titleText) titleText.text = title;
            if (lineText) lineText.text = line;
            if (activeMark) activeMark.enabled = active;
        }
    }
}
