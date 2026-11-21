using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Sets a TMP text in one §4 type role and one §11 colour token (Docs/STYLE_FOUNDATION.md),
    /// from <see cref="UITypeRolesSO"/> and <see cref="UIThemeSO"/>. Every label the style-B kit
    /// authors carries one, so a label cannot drift off the type scale and a re-theme is one asset
    /// edit. Runs in edit mode so the prefab shows the real face.
    ///
    /// Code that writes a LIVE number (a balance, a timer, a count) calls <see cref="SetText"/>
    /// rather than <c>text.text =</c>: for a tabular role it wraps the digits in the face's
    /// <c>&lt;mspace&gt;</c> (<see cref="TabularText"/>), which is what stops the number reflowing.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Text")]
    public class KitText : MonoBehaviour
    {
        [Header("Type")]
        [Tooltip("The §4 type scale. Unwired falls back to §4's defaults (but cannot set a font).")]
        [SerializeField] private UITypeRolesSO roles;
        [Tooltip("Which §4 role this text sets in.")]
        [SerializeField] private UITypeRole role = UITypeRole.Body;

        [Header("Colour")]
        [Tooltip("The chrome theme (§11). Unwired falls back to the authored §11 values.")]
        [SerializeField] private UIThemeSO theme;
        [Tooltip("Which §11 colour role the text wears. Ignored when a KitButton or KitTile drives this text's colour.")]
        [SerializeField] private UIColorToken colorToken = UIColorToken.TextLight;
        [Tooltip("Leave the colour alone - a KitButton / KitTile on a parent owns it.")]
        [SerializeField] private bool colorDrivenExternally;

        [Header("Fit")]
        [Tooltip("Shrink the text to fit its rect when it would spill out (TMP auto-size, capped at the role's size, so " +
                 "text that fits is drawn exactly at the role size). Off draws at the role size whatever the rect.")]
        [SerializeField] private bool shrinkToFit = true;
        [Tooltip("The smallest the text may shrink to, as a fraction of the role's size.")]
        [SerializeField, Range(0.3f, 1f)] private float minSizeScale = MinSizeScale;

        /// <summary>The default floor for <see cref="shrinkToFit"/>; the kit generator writes the same number.</summary>
        public const float MinSizeScale = 0.6f;

        private TMP_Text _text;

        public UITypeRole Role => role;

        private TMP_Text Text => _text ? _text : (_text = GetComponent<TMP_Text>());

        /// <summary>Set the text, wrapping live numerals for a tabular role.</summary>
        public void SetText(string value)
        {
            var spec = roles ? roles.Spec(role) : UITypeRolesSO.DefaultSpec(role);
            Text.text = spec.tabular ? TabularText.Wrap(value, Text.font) : value;
        }

        public void SetColorToken(UIColorToken token)
        {
            colorToken = token;
            Apply();
        }

        private void OnEnable() => Apply();

        private void OnValidate() => KitEditorDefer.Run(this, Apply);

        public void Apply()
        {
            var t = Text;
            if (!t) return;

            var spec = roles ? roles.Spec(role) : UITypeRolesSO.DefaultSpec(role);
            if (roles)
            {
                var font = roles.FontFor(spec.face);
                if (font && t.font != font) t.font = font;
            }
            t.fontSize = spec.size;
            // Auto-size starts at its maximum and only searches smaller sizes when the text overflows, so a label
            // that fits costs one layout pass and looks the same as a fixed size.
            t.enableAutoSizing = shrinkToFit;
            if (shrinkToFit)
            {
                t.fontSizeMax = spec.size;
                t.fontSizeMin = Mathf.Max(8f, Mathf.Round(spec.size * minSizeScale));
            }
            t.characterSpacing = spec.characterSpacing;
            // §4: weight comes from the face (no faux bold) and emphasis is colour only (no italic).
            var style = t.fontStyle & ~(FontStyles.UpperCase | FontStyles.LowerCase | FontStyles.SmallCaps |
                                        FontStyles.Bold | FontStyles.Italic);
            t.fontStyle = spec.upperCase ? style | FontStyles.UpperCase : style;

            if (!colorDrivenExternally)
                t.color = UITheme.Resolve(theme, colorToken);
        }
    }
}
