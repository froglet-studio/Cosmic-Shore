using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A word tab inside a window (Docs/STYLE_FOUNDATION.md section 10.4 tabs; the profile window's
    /// AVATAR / DISPLAY NAME): the word, and a 2px underline that marks the tab that is showing.
    ///
    /// <para>Like a top-nav tab (<see cref="KitTopNavTab"/>, whose shown look the nav itself toggles),
    /// this is a real Selectable: the pad reaches it, the CTA ring draws on focus, and the host
    /// only sets <see cref="IsSelected"/>. Colours: the shown tab's word is TextLight over a sky-blue
    /// Selection underline with the selection light running along it (rev 7); the others read
    /// NeutralLightest and lift to TextLight on hover.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Text Tab")]
    public class KitTextTab : KitSelectableBase
    {
        [Header("Tab")]
        [Tooltip("True while this tab's page is the one showing.")]
        [SerializeField] private bool isSelected;
        [Tooltip("The tab's word (Button role).")]
        [SerializeField] private Graphic label;
        [Tooltip("The 2px underline under the word, drawn only while selected.")]
        [SerializeField] private Graphic underline;

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value) return;
                isSelected = value;
                Refresh(false);
            }
        }

        protected override void CollectColors(VisualState state, List<(Graphic graphic, Color color)> into)
        {
            bool hover = state == VisualState.Highlighted || state == VisualState.Pressed;
            if (label)
                into.Add((label, Token(isSelected || hover ? UIColorToken.TextLight : UIColorToken.NeutralLightest)));
            if (underline)
            {
                into.Add((underline, Token(UIColorToken.Selection, isSelected ? 1f : 0f)));
                SetSelectionLight(underline, isSelected, bar: true);
            }
        }
    }
}
