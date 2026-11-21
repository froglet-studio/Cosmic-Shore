using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// One incoming party invite or friend request in the style-B party panel (Docs/MENU_REDESIGN_B.md,
    /// item 3): the same accept, decline and expiry behaviour as <see cref="RequestInfoEntry"/>, drawn
    /// in the kit - the kind of request in Control Active, warming towards Danger as it runs out, with
    /// accept (CTA) and decline (Cancel) icon buttons.
    /// </summary>
    public class KitRequestRow : RequestInfoEntry
    {
        [Header("Style-B")]
        [Tooltip("The kind line's KitText (colour driven here, so it is marked driven externally).")]
        [SerializeField] private KitText kindText;
        [Tooltip("The chrome theme, for the kind line's colours.")]
        [SerializeField] private UIThemeSO theme;

        private string _drawn;
        private TMPro.TMP_Text _kindLabel;

        // The kit row's buttons are always shown while the row exists.
        protected override void SetPartShown(Component part, bool shown) { }

        protected override void DrawLabel(Kind kind, string text, float expiry)
        {
            if (!kindText) return;
            if (text != _drawn)
            {
                _drawn = text;
                kindText.SetText(text);
            }

            if (!_kindLabel) _kindLabel = kindText.GetComponent<TMPro.TMP_Text>();
            _kindLabel.color = Color.Lerp(UITheme.Resolve(theme, UIColorToken.ControlActive),
                                     UITheme.Resolve(theme, UIColorToken.Danger), expiry);
        }
    }
}
