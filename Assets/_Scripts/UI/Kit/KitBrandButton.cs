using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A button whose face is a provider's own artwork - Google's and Apple's sign-in buttons (Docs/MENU_REDESIGN_B.md
    /// §55 / §57, the approved R9b board). Their brand rules forbid recolouring or restyling the button, so unlike
    /// <see cref="KitButton"/> nothing here tints the face: it is drawn exactly as the provider ships it, in every state.
    /// The kit's only mark is the sky-blue focus ring AROUND the button (<see cref="KitSelectableBase"/>'s
    /// <c>focusRing</c>, placed outside the artwork by the generator), never on it.
    /// </summary>
    public class KitBrandButton : KitSelectableBase
    {
        [Tooltip("The provider's button artwork. Always drawn white (untinted), whatever the state.")]
        [SerializeField] Graphic face;

        protected override void CollectColors(VisualState state, List<(Graphic graphic, Color color)> into)
        {
            if (face) into.Add((face, Color.white));
        }
    }
}
