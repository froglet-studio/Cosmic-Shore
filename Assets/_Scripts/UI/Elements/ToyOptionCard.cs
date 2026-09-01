using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One CHOICE inside a toy, on the app shell's Toy Box - the flat twin of a matrix station:
    /// a domain, a hull, a world, a canvas, a creature.
    ///
    /// <para>A view, like <see cref="ToyboxCard"/>: it draws a <see cref="ToyShellOption"/> and
    /// reports the press. <see cref="ToyboxModal"/> decides whether that press expands a layer,
    /// acts, or enters freestyle first.</para>
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ToyOptionCard : MonoBehaviour
    {
        [SerializeField] TMP_Text labelText;

        [SerializeField, Tooltip("Second line: progress, state, 'current'. Hidden when empty.")]
        TMP_Text detailText;

        [SerializeField, Tooltip("Tinted to the option's own colour - a domain's colour, a toy's " +
                                 "accent - so the flat row wears what the ring wears.")]
        Image accentFill;

        [SerializeField, Tooltip("Shown when this option is the state the player is ALREADY in: " +
                                 "the hull they fly, the cell they are in, the colour they wear.")]
        GameObject currentMarker;

        [SerializeField, Tooltip("Shown when choosing this option opens another layer rather than " +
                                 "acting (the Lifeform Matrix's kingdoms and species).")]
        GameObject expandMarker;

        [SerializeField, Tooltip("Shown when the option needs the player at the stick, so the " +
                                 "press will enter freestyle first.")]
        GameObject freestyleMarker;

        Button _button;

        /// <summary>The option this row is currently drawing, or null.</summary>
        public ToyShellOption Option { get; private set; }

        public Button Button => _button ? _button : _button = GetComponent<Button>();

        public void Bind(ToyShellOption option)
        {
            Option = option;
            if (option == null) return;

            if (labelText) labelText.text = option.Label;

            if (detailText)
            {
                detailText.text = option.Detail;
                detailText.gameObject.SetActive(!string.IsNullOrEmpty(option.Detail));
            }

            if (accentFill) accentFill.color = option.Accent;
            if (currentMarker) currentMarker.SetActive(option.IsCurrent);
            if (expandMarker) expandMarker.SetActive(option.IsBranch);
            if (freestyleMarker) freestyleMarker.SetActive(option.RequiresFreestyle);

            // A leaf with nothing to apply is the "you are already here" row every surface emits
            // deliberately - it stays visible (it is how the player reads their own state) and
            // stops responding.
            Button.interactable = option.IsBranch || option.Apply != null;
        }
    }
}
