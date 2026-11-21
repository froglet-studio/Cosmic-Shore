using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A word tab inside a window (Docs/STYLE_FOUNDATION.md section 10.4 tabs; the profile window's
    /// AVATAR / DISPLAY NAME): the word, and a 2px underline that marks the tab that is showing.
    ///
    /// <para>Unlike the top-nav tabs (KitTopNavTab.prefab, toggled by the nav itself),
    /// this is a real Selectable: the pad reaches it, the CTA ring draws on focus, and the host
    /// only sets <see cref="IsSelected"/>. Colours: the shown tab's word (TextLight by default) over a
    /// still sky-blue Selection underline; the others read NeutralLightest and lift to TextLight on
    /// hover. The three word tokens are serialized.</para>
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

        [Header("Colours")]
        [Tooltip("The word while its page is not showing.")]
        [SerializeField] private UIColorToken restLabelToken = UIColorToken.NeutralLightest;
        [Tooltip("The word while its page is showing. Set it to the rest token for a row of words that should all read " +
                 "in one colour, with the underline alone marking the shown one (the hub strip).")]
        [SerializeField] private UIColorToken selectedLabelToken = UIColorToken.TextLight;
        [Tooltip("The word under the pointer or the pad's focus.")]
        [SerializeField] private UIColorToken hoverLabelToken = UIColorToken.TextLight;

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
                into.Add((label, Token(hover ? hoverLabelToken : isSelected ? selectedLabelToken : restLabelToken)));
            // A plain, still bar: no light runs along it (Docs/UI_COLOR_RULES.md, attention economy).
            if (underline) into.Add((underline, Token(UIColorToken.Selection, isSelected ? 1f : 0f)));
        }
    }
}
