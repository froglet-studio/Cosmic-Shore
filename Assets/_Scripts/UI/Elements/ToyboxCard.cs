using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One toy on the app shell's Toy Box grid - the flat twin of the station the player would fly
    /// at in freestyle. Portrait, name, tagline, and the fundamental it changes.
    ///
    /// <para>Like the arcade's game card, this is a VIEW: it holds no toy state and makes no
    /// decision. <see cref="ToyboxModal"/> binds it and owns what a press does, so the card cannot
    /// become a second authority on a toy's effect.</para>
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ToyboxCard : MonoBehaviour
    {
        [SerializeField, Tooltip("The toy's baked codex portrait. Hidden when the toy has none.")]
        Image portrait;

        [SerializeField, Tooltip("Flat accent fill shown behind the portrait - and INSTEAD of it " +
                                 "when the toy has no baked art, so a card is never blank.")]
        Image accentFill;

        [SerializeField] TMP_Text nameText;

        [SerializeField, Tooltip("One line: what the toy does.")]
        TMP_Text taglineText;

        [SerializeField, Tooltip("Which fundamental this toy changes - Pilot / World / Creation.")]
        TMP_Text sectionText;

        [SerializeField, Tooltip("Shown while the toy cannot answer right now (mid cell-swap, " +
                                 "mid vessel-swap). The card stays visible and stops responding.")]
        GameObject unavailableOverlay;

        Button _button;

        /// <summary>The surface this card is currently drawing, or null.</summary>
        public IToyShellSurface Surface { get; private set; }

        public Button Button => _button ? _button : _button = GetComponent<Button>();

        public void Bind(IToyShellSurface surface)
        {
            Surface = surface;

            var definition = surface?.ShellDefinition;
            bool available = surface != null && surface.ShellAvailable;

            if (nameText) nameText.text = definition ? definition.DisplayName : "";
            if (taglineText) taglineText.text = ToyPortraitLibrary.Tagline(definition);
            if (sectionText) sectionText.text = ToyPortraitLibrary.Section(definition);

            Color accent = definition ? definition.AccentColor : Color.white;
            if (accentFill) accentFill.color = accent;

            var sprite = ToyPortraitLibrary.Portrait(definition);
            if (portrait)
            {
                portrait.sprite = sprite;
                // Disabled rather than left holding a stale sprite: a card recycled from another
                // toy would otherwise show that toy's picture under this toy's name.
                portrait.enabled = sprite;
            }

            if (unavailableOverlay) unavailableOverlay.SetActive(!available);
            Button.interactable = available;
        }
    }
}
