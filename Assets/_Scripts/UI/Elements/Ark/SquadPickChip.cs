using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One choice in the Ark's Make squad picker (a vessel tile, a pilot, an element, an artifact, who flies a rogue seat) and
    /// one domain chip: a button with a word, an optional second line and picture, and a mark while it is the one picked.
    /// <see cref="SquadsPage"/> fills a fixed pool of them and hides the ones it does not need.
    /// </summary>
    public class SquadPickChip : MonoBehaviour
    {
        [SerializeField] private KitButton button;
        [Tooltip("Hides the chip while the pool has nothing for it.")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text label;
        [Tooltip("A second line (a pilot's ability). Optional.")]
        [SerializeField] private TMP_Text line;
        [Tooltip("A picture (a hull, a pilot) or the domain's diamond. Optional.")]
        [SerializeField] private Image image;
        [Tooltip("Shown while this is the picked choice.")]
        [SerializeField] private Graphic selectedMark;

        public KitButton Button => button;

        public void Draw(bool used, string word, string secondLine, Sprite picture, bool selected)
        {
            if (group) KitVisibility.SetShown(group, used);
            if (button) button.interactable = used;
            if (!used) return;
            if (label) label.text = word;
            if (line) line.text = secondLine;
            if (image && picture)
            {
                image.sprite = picture;
                image.enabled = true;
            }
            else if (image && !picture && line) image.enabled = false;   // a pilot row with no portrait
            if (selectedMark) selectedMark.enabled = selected;
        }

        /// <summary>Colours the picture (the domain chips' diamonds).</summary>
        public void Tint(Color color)
        {
            if (image) image.color = color;
        }
    }
}
